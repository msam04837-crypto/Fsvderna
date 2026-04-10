using GovRegistry.Core;
using GovRegistry.Infrastructure;

namespace GovRegistry.Wpf;

public sealed class AppServices
{
    public IRepository Repository { get; }
    public IAuthService AuthService { get; }
    public IDriverService DriverService { get; }
    public IVisitorService VisitorService { get; }
    public IReportingService ReportingService { get; }
    public IBackupService BackupService { get; }

    public AppServices()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GovRegistry");
        string db = Path.Combine(root, "registry.db");
        Repository = new SqliteRepository(db);
        Repository.Initialize();
        AuthService = new AuthService(Repository);
        DriverService = new DriverService(Repository);
        VisitorService = new VisitorService(Repository);
        ReportingService = new ReportingService();
        BackupService = new BackupService(Repository);
    }
}
