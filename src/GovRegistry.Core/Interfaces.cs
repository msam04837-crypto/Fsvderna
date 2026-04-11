namespace GovRegistry.Core;

public interface IAuthService
{
    AppUser? Authenticate(string username, string password);
    void LogAttempt(string username, bool success, string? reason = null);
}

public interface IDriverService
{
    int RegisterExit(DriverRecord record, string actor);
    void RegisterReturn(int id, DateTime returnTime, string actor, string? notes = null);
    void Update(DriverRecord record, string actor);
    IReadOnlyList<DriverRecord> Search(string? name, string? plate, DateTime? from, DateTime? to);
    IReadOnlyList<DriverRecord> GetActive();
    IReadOnlyList<DriverRecord> GetReturned(DateTime? day = null);
}

public interface IVisitorService
{
    int RegisterEntry(VisitorRecord record, string actor);
    void RegisterExit(int id, DateTime exitTime, string actor, string? notes = null);
    void Update(VisitorRecord record, string actor);
    IReadOnlyList<VisitorRecord> Search(string? name, string? identity, DateTime? from, DateTime? to);
    IReadOnlyList<VisitorRecord> GetInsideNow();
}

public interface IReportingService
{
    string ExportDriversCsv(IEnumerable<DriverRecord> records, string filePath);
    string ExportVisitorsCsv(IEnumerable<VisitorRecord> records, string filePath);
}

public interface IBackupService
{
    string Backup(string outputDirectory);
    void Restore(string backupFile);
}

public interface IRepository
{
    void Initialize();
    AppUser? GetUser(string username);
    void SaveLoginAttempt(LoginAttempt attempt);
    int InsertDriver(DriverRecord record);
    void UpdateDriver(DriverRecord record);
    DriverRecord? GetDriver(int id);
    IReadOnlyList<DriverRecord> QueryDrivers(string? name, string? plate, DateTime? from, DateTime? to, DriverStatus? status = null);
    int InsertVisitor(VisitorRecord record);
    void UpdateVisitor(VisitorRecord record);
    VisitorRecord? GetVisitor(int id);
    IReadOnlyList<VisitorRecord> QueryVisitors(string? name, string? identity, DateTime? from, DateTime? to, bool insideOnly = false);
    (int activeDrivers, int activeVisitors) GetCounters();
    void AddAudit(AuditEvent audit);
    IReadOnlyList<AuditEvent> RecentAudits(int count = 30);
    string DatabasePath { get; }
}
