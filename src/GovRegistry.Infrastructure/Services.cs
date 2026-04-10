using System.Text;
using GovRegistry.Core;

namespace GovRegistry.Infrastructure;

public sealed class AuthService(IRepository repository) : IAuthService
{
    public AppUser? Authenticate(string username, string password)
    {
        var user = repository.GetUser(username);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(password, user.PasswordHash))
        {
            LogAttempt(username, false, "Invalid credentials");
            return null;
        }
        LogAttempt(username, true);
        return user;
    }

    public void LogAttempt(string username, bool success, string? reason = null) =>
        repository.SaveLoginAttempt(new LoginAttempt(0, username, success, reason, DateTime.UtcNow));
}

public sealed class DriverService(IRepository repo) : IDriverService
{
    public int RegisterExit(DriverRecord record, string actor)
    {
        int id = repo.InsertDriver(record with { Status = DriverStatus.Out, CreatedAt = DateTime.UtcNow });
        repo.AddAudit(new AuditEvent(0, "Driver", id, "CreateExit", record.DriverName, actor, DateTime.UtcNow));
        return id;
    }

    public void RegisterReturn(int id, DateTime returnTime, string actor, string? notes = null)
    {
        var current = repo.GetDriver(id) ?? throw new InvalidOperationException("Driver record not found.");
        var updated = current with { ReturnTime = returnTime, Status = DriverStatus.Returned, Notes = notes ?? current.Notes, UpdatedAt = DateTime.UtcNow };
        repo.UpdateDriver(updated);
        repo.AddAudit(new AuditEvent(0, "Driver", id, "RegisterReturn", updated.DriverName, actor, DateTime.UtcNow));
    }

    public void Update(DriverRecord record, string actor)
    {
        repo.UpdateDriver(record with { UpdatedAt = DateTime.UtcNow });
        repo.AddAudit(new AuditEvent(0, "Driver", record.Id, "Update", record.DriverName, actor, DateTime.UtcNow));
    }

    public IReadOnlyList<DriverRecord> Search(string? name, string? plate, DateTime? from, DateTime? to) => repo.QueryDrivers(name, plate, from, to);
    public IReadOnlyList<DriverRecord> GetActive() => repo.QueryDrivers(null, null, null, null, DriverStatus.Out);
    public IReadOnlyList<DriverRecord> GetReturned(DateTime? day = null)
    {
        if (day is null) return repo.QueryDrivers(null, null, null, null, DriverStatus.Returned);
        var start = day.Value.Date;
        var end = start.AddDays(1).AddTicks(-1);
        return repo.QueryDrivers(null, null, start, end, DriverStatus.Returned);
    }
}

public sealed class VisitorService(IRepository repo) : IVisitorService
{
    public int RegisterEntry(VisitorRecord record, string actor)
    {
        int id = repo.InsertVisitor(record with { CreatedAt = DateTime.UtcNow });
        repo.AddAudit(new AuditEvent(0, "Visitor", id, "CreateEntry", record.VisitorName, actor, DateTime.UtcNow));
        return id;
    }

    public void RegisterExit(int id, DateTime exitTime, string actor, string? notes = null)
    {
        var current = repo.GetVisitor(id) ?? throw new InvalidOperationException("Visitor not found.");
        var updated = current with { ExitTime = exitTime, Notes = notes ?? current.Notes, UpdatedAt = DateTime.UtcNow };
        repo.UpdateVisitor(updated);
        repo.AddAudit(new AuditEvent(0, "Visitor", id, "RegisterExit", updated.VisitorName, actor, DateTime.UtcNow));
    }

    public void Update(VisitorRecord record, string actor)
    {
        repo.UpdateVisitor(record with { UpdatedAt = DateTime.UtcNow });
        repo.AddAudit(new AuditEvent(0, "Visitor", record.Id, "Update", record.VisitorName, actor, DateTime.UtcNow));
    }

    public IReadOnlyList<VisitorRecord> Search(string? name, string? identity, DateTime? from, DateTime? to) => repo.QueryVisitors(name, identity, from, to);
    public IReadOnlyList<VisitorRecord> GetInsideNow() => repo.QueryVisitors(null, null, null, null, insideOnly: true);
}

public sealed class ReportingService : IReportingService
{
    public string ExportDriversCsv(IEnumerable<DriverRecord> records, string filePath)
    {
        var sb = new StringBuilder("اسم السائق,نوع السيارة,رقم اللوحة,وقت الخروج,وقت العودة,الجهة,الحالة,أنشأ بواسطة\n");
        foreach (var d in records) sb.AppendLine($"{d.DriverName},{d.CarType},{d.PlateNumber},{d.ExitTime:u},{d.ReturnTime:u},{d.Destination},{d.Status},{d.CreatedBy}");
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        return filePath;
    }

    public string ExportVisitorsCsv(IEnumerable<VisitorRecord> records, string filePath)
    {
        var sb = new StringBuilder("اسم الزائر,رقم الهوية,الشخص المراد زيارته,وقت الدخول,وقت الخروج,سبب الزيارة,مسجل بواسطة\n");
        foreach (var v in records) sb.AppendLine($"{v.VisitorName},{v.IdPassport},{v.PersonToVisit},{v.EntryTime:u},{v.ExitTime:u},{v.VisitPurpose},{v.RegisteredBy}");
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        return filePath;
    }
}

public sealed class BackupService(IRepository repository) : IBackupService
{
    public string Backup(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        string file = Path.Combine(outputDirectory, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        File.Copy(repository.DatabasePath, file, overwrite: true);
        return file;
    }

    public void Restore(string backupFile)
    {
        File.Copy(backupFile, repository.DatabasePath, overwrite: true);
    }
}
