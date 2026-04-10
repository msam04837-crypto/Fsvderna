namespace GovRegistry.Core;

public enum UserRole { Admin, Supervisor, DataEntry, Viewer }
public enum DriverStatus { Out, Returned }

public record AppUser(int Id, string Username, string PasswordHash, UserRole Role, bool IsActive = true);

public record DriverRecord(
    int Id,
    string DriverName,
    string CarType,
    string PlateNumber,
    DateTime ExitTime,
    DateTime? ReturnTime,
    string Destination,
    DriverStatus Status,
    string? Notes,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record VisitorRecord(
    int Id,
    string VisitorName,
    string IdPassport,
    string PersonToVisit,
    DateTime EntryTime,
    DateTime? ExitTime,
    string VisitPurpose,
    string? Notes,
    string RegisteredBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AuditEvent(int Id, string EntityName, int EntityId, string Action, string Data, string PerformedBy, DateTime Timestamp);
public record LoginAttempt(int Id, string Username, bool Success, string? FailureReason, DateTime Timestamp);
public record DashboardStats(int ActiveDrivers, int ActiveVisitors, IReadOnlyList<AuditEvent> RecentActivities);
