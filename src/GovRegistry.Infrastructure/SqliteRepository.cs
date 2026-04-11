using GovRegistry.Core;
using Microsoft.Data.Sqlite;

namespace GovRegistry.Infrastructure;

public sealed class SqliteRepository : IRepository
{
    public string DatabasePath { get; }
    private readonly string _connectionString;

    public SqliteRepository(string databasePath)
    {
        DatabasePath = databasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public void Initialize()
    {
        using var c = new SqliteConnection(_connectionString);
        c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = @"
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS Users(Id INTEGER PRIMARY KEY, Username TEXT UNIQUE NOT NULL, PasswordHash TEXT NOT NULL, Role TEXT NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1);
CREATE TABLE IF NOT EXISTS LoginAttempts(Id INTEGER PRIMARY KEY, Username TEXT NOT NULL, Success INTEGER NOT NULL, FailureReason TEXT NULL, Timestamp TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS Drivers(Id INTEGER PRIMARY KEY, DriverName TEXT, CarType TEXT, PlateNumber TEXT, ExitTime TEXT, ReturnTime TEXT NULL, Destination TEXT, Status TEXT, Notes TEXT NULL, CreatedBy TEXT, CreatedAt TEXT, UpdatedAt TEXT NULL);
CREATE TABLE IF NOT EXISTS Visitors(Id INTEGER PRIMARY KEY, VisitorName TEXT, IdPassport TEXT, PersonToVisit TEXT, EntryTime TEXT, ExitTime TEXT NULL, VisitPurpose TEXT, Notes TEXT NULL, RegisteredBy TEXT, CreatedAt TEXT, UpdatedAt TEXT NULL);
CREATE TABLE IF NOT EXISTS Audit(Id INTEGER PRIMARY KEY, EntityName TEXT, EntityId INTEGER, Action TEXT, Data TEXT, PerformedBy TEXT, Timestamp TEXT);
";
        cmd.ExecuteNonQuery();

        var check = c.CreateCommand();
        check.CommandText = "SELECT COUNT(1) FROM Users;";
        if (Convert.ToInt32(check.ExecuteScalar()) == 0)
        {
            var seed = c.CreateCommand();
            seed.CommandText = "INSERT INTO Users(Username, PasswordHash, Role, IsActive) VALUES (@u,@p,@r,1);";
            seed.Parameters.AddWithValue("@u", "admin");
            seed.Parameters.AddWithValue("@p", PasswordHasher.Hash("Admin@123"));
            seed.Parameters.AddWithValue("@r", UserRole.Admin.ToString());
            seed.ExecuteNonQuery();
        }
    }

    public AppUser? GetUser(string username)
    {
        using var c = new SqliteConnection(_connectionString);
        c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id, Username, PasswordHash, Role, IsActive FROM Users WHERE Username=@u";
        cmd.Parameters.AddWithValue("@u", username);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new AppUser(r.GetInt32(0), r.GetString(1), r.GetString(2), Enum.Parse<UserRole>(r.GetString(3)), r.GetInt32(4) == 1);
    }

    public void SaveLoginAttempt(LoginAttempt a)
    {
        using var c = new SqliteConnection(_connectionString); c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO LoginAttempts(Username, Success, FailureReason, Timestamp) VALUES (@u,@s,@f,@t)";
        cmd.Parameters.AddWithValue("@u", a.Username);
        cmd.Parameters.AddWithValue("@s", a.Success ? 1 : 0);
        cmd.Parameters.AddWithValue("@f", (object?)a.FailureReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@t", a.Timestamp.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public int InsertDriver(DriverRecord d)
    {
        using var c = new SqliteConnection(_connectionString); c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT INTO Drivers(DriverName,CarType,PlateNumber,ExitTime,ReturnTime,Destination,Status,Notes,CreatedBy,CreatedAt,UpdatedAt)
VALUES(@n,@c,@p,@e,@r,@d,@s,@o,@by,@ca,@ua); SELECT last_insert_rowid();";
        MapDriverParams(cmd, d);
        return Convert.ToInt32((long)cmd.ExecuteScalar()!);
    }

    public void UpdateDriver(DriverRecord d)
    {
        using var c = new SqliteConnection(_connectionString); c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = @"UPDATE Drivers SET DriverName=@n,CarType=@c,PlateNumber=@p,ExitTime=@e,ReturnTime=@r,Destination=@d,Status=@s,Notes=@o,CreatedBy=@by,CreatedAt=@ca,UpdatedAt=@ua WHERE Id=@id";
        MapDriverParams(cmd, d);
        cmd.Parameters.AddWithValue("@id", d.Id);
        cmd.ExecuteNonQuery();
    }

    public DriverRecord? GetDriver(int id) => QueryDrivers(null, null, null, null).FirstOrDefault(x => x.Id == id);

    public IReadOnlyList<DriverRecord> QueryDrivers(string? name, string? plate, DateTime? from, DateTime? to, DriverStatus? status = null)
    {
        using var c = new SqliteConnection(_connectionString); c.Open();
        var cmd = c.CreateCommand();
        var filters = new List<string> { "1=1" };
        if (!string.IsNullOrWhiteSpace(name)) { filters.Add("DriverName LIKE @n"); cmd.Parameters.AddWithValue("@n", $"%{name}%"); }
        if (!string.IsNullOrWhiteSpace(plate)) { filters.Add("PlateNumber LIKE @p"); cmd.Parameters.AddWithValue("@p", $"%{plate}%"); }
        if (from.HasValue) { filters.Add("ExitTime >= @f"); cmd.Parameters.AddWithValue("@f", from.Value.ToString("O")); }
        if (to.HasValue) { filters.Add("ExitTime <= @t"); cmd.Parameters.AddWithValue("@t", to.Value.ToString("O")); }
        if (status.HasValue) { filters.Add("Status=@s"); cmd.Parameters.AddWithValue("@s", status.Value.ToString()); }
        cmd.CommandText = $"SELECT Id,DriverName,CarType,PlateNumber,ExitTime,ReturnTime,Destination,Status,Notes,CreatedBy,CreatedAt,UpdatedAt FROM Drivers WHERE {string.Join(" AND ", filters)} ORDER BY ExitTime DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<DriverRecord>();
        while (r.Read())
        {
            list.Add(new DriverRecord(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), DateTime.Parse(r.GetString(4)),
                r.IsDBNull(5) ? null : DateTime.Parse(r.GetString(5)), r.GetString(6), Enum.Parse<DriverStatus>(r.GetString(7)),
                r.IsDBNull(8) ? null : LocalProtector.Unprotect(r.GetString(8)), r.GetString(9), DateTime.Parse(r.GetString(10)),
                r.IsDBNull(11) ? null : DateTime.Parse(r.GetString(11))));
        }
        return list;
    }

    public int InsertVisitor(VisitorRecord v)
    {
        using var c = new SqliteConnection(_connectionString); c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT INTO Visitors(VisitorName,IdPassport,PersonToVisit,EntryTime,ExitTime,VisitPurpose,Notes,RegisteredBy,CreatedAt,UpdatedAt)
VALUES(@n,@i,@p,@e,@x,@vp,@o,@by,@ca,@ua); SELECT last_insert_rowid();";
        MapVisitorParams(cmd, v);
        return Convert.ToInt32((long)cmd.ExecuteScalar()!);
    }

    public void UpdateVisitor(VisitorRecord v)
    {
        using var c = new SqliteConnection(_connectionString); c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = @"UPDATE Visitors SET VisitorName=@n,IdPassport=@i,PersonToVisit=@p,EntryTime=@e,ExitTime=@x,VisitPurpose=@vp,Notes=@o,RegisteredBy=@by,CreatedAt=@ca,UpdatedAt=@ua WHERE Id=@id";
        MapVisitorParams(cmd, v); cmd.Parameters.AddWithValue("@id", v.Id); cmd.ExecuteNonQuery();
    }

    public VisitorRecord? GetVisitor(int id) => QueryVisitors(null, null, null, null).FirstOrDefault(x => x.Id == id);

    public IReadOnlyList<VisitorRecord> QueryVisitors(string? name, string? identity, DateTime? from, DateTime? to, bool insideOnly = false)
    {
        using var c = new SqliteConnection(_connectionString); c.Open();
        var cmd = c.CreateCommand();
        var filters = new List<string> { "1=1" };
        if (!string.IsNullOrWhiteSpace(name)) { filters.Add("VisitorName LIKE @n"); cmd.Parameters.AddWithValue("@n", $"%{name}%"); }
        if (!string.IsNullOrWhiteSpace(identity)) { filters.Add("IdPassport LIKE @i"); cmd.Parameters.AddWithValue("@i", $"%{identity}%"); }
        if (from.HasValue) { filters.Add("EntryTime >= @f"); cmd.Parameters.AddWithValue("@f", from.Value.ToString("O")); }
        if (to.HasValue) { filters.Add("EntryTime <= @t"); cmd.Parameters.AddWithValue("@t", to.Value.ToString("O")); }
        if (insideOnly) filters.Add("ExitTime IS NULL");
        cmd.CommandText = $"SELECT Id,VisitorName,IdPassport,PersonToVisit,EntryTime,ExitTime,VisitPurpose,Notes,RegisteredBy,CreatedAt,UpdatedAt FROM Visitors WHERE {string.Join(" AND ", filters)} ORDER BY EntryTime DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<VisitorRecord>();
        while (r.Read())
        {
            list.Add(new VisitorRecord(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), DateTime.Parse(r.GetString(4)),
                r.IsDBNull(5) ? null : DateTime.Parse(r.GetString(5)), r.GetString(6), r.IsDBNull(7) ? null : LocalProtector.Unprotect(r.GetString(7)),
                r.GetString(8), DateTime.Parse(r.GetString(9)), r.IsDBNull(10) ? null : DateTime.Parse(r.GetString(10))));
        }
        return list;
    }

    public (int activeDrivers, int activeVisitors) GetCounters()
    {
        using var c = new SqliteConnection(_connectionString); c.Open();
        var d = c.CreateCommand(); d.CommandText = "SELECT COUNT(1) FROM Drivers WHERE Status='Out'";
        var v = c.CreateCommand(); v.CommandText = "SELECT COUNT(1) FROM Visitors WHERE ExitTime IS NULL";
        return (Convert.ToInt32(d.ExecuteScalar()), Convert.ToInt32(v.ExecuteScalar()));
    }

    public void AddAudit(AuditEvent a)
    {
        using var c = new SqliteConnection(_connectionString); c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO Audit(EntityName,EntityId,Action,Data,PerformedBy,Timestamp) VALUES(@e,@i,@a,@d,@p,@t)";
        cmd.Parameters.AddWithValue("@e", a.EntityName); cmd.Parameters.AddWithValue("@i", a.EntityId); cmd.Parameters.AddWithValue("@a", a.Action);
        cmd.Parameters.AddWithValue("@d", a.Data); cmd.Parameters.AddWithValue("@p", a.PerformedBy); cmd.Parameters.AddWithValue("@t", a.Timestamp.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<AuditEvent> RecentAudits(int count = 30)
    {
        using var c = new SqliteConnection(_connectionString); c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id,EntityName,EntityId,Action,Data,PerformedBy,Timestamp FROM Audit ORDER BY Timestamp DESC LIMIT @c";
        cmd.Parameters.AddWithValue("@c", count);
        using var r = cmd.ExecuteReader();
        var list = new List<AuditEvent>();
        while (r.Read()) list.Add(new AuditEvent(r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetString(4), r.GetString(5), DateTime.Parse(r.GetString(6))));
        return list;
    }

    private static void MapDriverParams(SqliteCommand cmd, DriverRecord d)
    {
        cmd.Parameters.AddWithValue("@n", d.DriverName);
        cmd.Parameters.AddWithValue("@c", d.CarType);
        cmd.Parameters.AddWithValue("@p", d.PlateNumber);
        cmd.Parameters.AddWithValue("@e", d.ExitTime.ToString("O"));
        cmd.Parameters.AddWithValue("@r", (object?)d.ReturnTime?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@d", d.Destination);
        cmd.Parameters.AddWithValue("@s", d.Status.ToString());
        cmd.Parameters.AddWithValue("@o", (object?)d.Notes is null ? DBNull.Value : LocalProtector.Protect(d.Notes));
        cmd.Parameters.AddWithValue("@by", d.CreatedBy);
        cmd.Parameters.AddWithValue("@ca", d.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@ua", (object?)d.UpdatedAt?.ToString("O") ?? DBNull.Value);
    }

    private static void MapVisitorParams(SqliteCommand cmd, VisitorRecord v)
    {
        cmd.Parameters.AddWithValue("@n", v.VisitorName);
        cmd.Parameters.AddWithValue("@i", v.IdPassport);
        cmd.Parameters.AddWithValue("@p", v.PersonToVisit);
        cmd.Parameters.AddWithValue("@e", v.EntryTime.ToString("O"));
        cmd.Parameters.AddWithValue("@x", (object?)v.ExitTime?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@vp", v.VisitPurpose);
        cmd.Parameters.AddWithValue("@o", (object?)v.Notes is null ? DBNull.Value : LocalProtector.Protect(v.Notes));
        cmd.Parameters.AddWithValue("@by", v.RegisteredBy);
        cmd.Parameters.AddWithValue("@ca", v.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@ua", (object?)v.UpdatedAt?.ToString("O") ?? DBNull.Value);
    }
}
