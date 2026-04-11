using System.Collections.ObjectModel;
using GovRegistry.Core;
using GovRegistry.Wpf.Commands;
using Microsoft.Win32;

namespace GovRegistry.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly AppUser _user;

    public string CurrentUser => $"{_user.Username} ({_user.Role})";
    public int ActiveDrivers { get => _activeDrivers; private set { _activeDrivers = value; OnPropertyChanged(); } }
    public int ActiveVisitors { get => _activeVisitors; private set { _activeVisitors = value; OnPropertyChanged(); } }
    private int _activeDrivers;
    private int _activeVisitors;

    public ObservableCollection<DriverRecord> Drivers { get; } = [];
    public ObservableCollection<VisitorRecord> Visitors { get; } = [];
    public ObservableCollection<AuditEvent> Activities { get; } = [];
    public DriverRecord? SelectedDriver { get; set; }
    public VisitorRecord? SelectedVisitor { get; set; }

    public string DriverName { get; set; } = string.Empty;
    public string CarType { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string DriverNotes { get; set; } = string.Empty;

    public string VisitorName { get; set; } = string.Empty;
    public string IdPassport { get; set; } = string.Empty;
    public string PersonToVisit { get; set; } = string.Empty;
    public string VisitPurpose { get; set; } = string.Empty;
    public string VisitorNotes { get; set; } = string.Empty;
    public string DriverSearchName { get; set; } = string.Empty;
    public string DriverSearchPlate { get; set; } = string.Empty;
    public string VisitorSearchName { get; set; } = string.Empty;
    public string VisitorSearchIdentity { get; set; } = string.Empty;
    public DateTime ReportFromDate { get; set; } = DateTime.Today;
    public DateTime ReportToDate { get; set; } = DateTime.Today;

    public RelayCommand RegisterDriverExitCommand { get; }
    public RelayCommand RegisterVisitorEntryCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand RegisterDriverReturnCommand { get; }
    public RelayCommand RegisterVisitorExitCommand { get; }
    public RelayCommand ExportDriversCommand { get; }
    public RelayCommand ExportVisitorsCommand { get; }
    public RelayCommand BackupCommand { get; }
    public RelayCommand RestoreCommand { get; }
    public RelayCommand SearchDriversCommand { get; }
    public RelayCommand SearchVisitorsCommand { get; }
    public RelayCommand DailyDriversReportCommand { get; }
    public RelayCommand DriversDateRangeReportCommand { get; }
    public RelayCommand DailyVisitorsReportCommand { get; }
    public RelayCommand VisitorsDateRangeReportCommand { get; }
    public RelayCommand ActiveStatusReportCommand { get; }

    public MainViewModel(AppServices services, AppUser user)
    {
        _services = services;
        _user = user;
        RegisterDriverExitCommand = new RelayCommand(RegisterDriverExit, () => _user.Role != UserRole.Viewer);
        RegisterVisitorEntryCommand = new RelayCommand(RegisterVisitorEntry, () => _user.Role != UserRole.Viewer);
        RefreshCommand = new RelayCommand(Refresh);
        RegisterDriverReturnCommand = new RelayCommand(RegisterDriverReturn, () => _user.Role != UserRole.Viewer);
        RegisterVisitorExitCommand = new RelayCommand(RegisterVisitorExit, () => _user.Role != UserRole.Viewer);
        ExportDriversCommand = new RelayCommand(ExportDrivers);
        ExportVisitorsCommand = new RelayCommand(ExportVisitors);
        BackupCommand = new RelayCommand(Backup);
        RestoreCommand = new RelayCommand(Restore, () => _user.Role is UserRole.Admin or UserRole.Supervisor);
        SearchDriversCommand = new RelayCommand(SearchDrivers);
        SearchVisitorsCommand = new RelayCommand(SearchVisitors);
        DailyDriversReportCommand = new RelayCommand(ExportDailyDrivers);
        DriversDateRangeReportCommand = new RelayCommand(ExportDriversByRange);
        DailyVisitorsReportCommand = new RelayCommand(ExportDailyVisitors);
        VisitorsDateRangeReportCommand = new RelayCommand(ExportVisitorsByRange);
        ActiveStatusReportCommand = new RelayCommand(ExportActiveStatus);
        Refresh();
    }

    private void RegisterDriverExit()
    {
        var d = new DriverRecord(0, DriverName, CarType, PlateNumber, DateTime.Now, null, Destination, DriverStatus.Out, DriverNotes, _user.Username, DateTime.UtcNow, null);
        _services.DriverService.RegisterExit(d, _user.Username);
        Refresh();
    }

    private void RegisterVisitorEntry()
    {
        var v = new VisitorRecord(0, VisitorName, IdPassport, PersonToVisit, DateTime.Now, null, VisitPurpose, VisitorNotes, _user.Username, DateTime.UtcNow, null);
        _services.VisitorService.RegisterEntry(v, _user.Username);
        Refresh();
    }

    private void Refresh()
    {
        Drivers.Clear();
        foreach (var d in _services.DriverService.Search(null, null, DateTime.Today.AddDays(-7), DateTime.Today.AddDays(1))) Drivers.Add(d);

        Visitors.Clear();
        foreach (var v in _services.VisitorService.Search(null, null, DateTime.Today.AddDays(-7), DateTime.Today.AddDays(1))) Visitors.Add(v);

        Activities.Clear();
        foreach (var a in _services.Repository.RecentAudits(20)) Activities.Add(a);

        var c = _services.Repository.GetCounters();
        ActiveDrivers = c.activeDrivers;
        ActiveVisitors = c.activeVisitors;
    }

    private void RegisterDriverReturn()
    {
        if (_user.Role is UserRole.DataEntry or UserRole.Viewer) return;
        if (SelectedDriver is null) return;
        _services.DriverService.RegisterReturn(SelectedDriver.Id, DateTime.Now, _user.Username);
        Refresh();
    }

    private void RegisterVisitorExit()
    {
        if (_user.Role is UserRole.DataEntry or UserRole.Viewer) return;
        if (SelectedVisitor is null) return;
        _services.VisitorService.RegisterExit(SelectedVisitor.Id, DateTime.Now, _user.Username);
        Refresh();
    }

    private void ExportDrivers()
    {
        var sfd = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "drivers_report.csv" };
        if (sfd.ShowDialog() == true)
            _services.ReportingService.ExportDriversCsv(Drivers, sfd.FileName);
    }

    private void ExportVisitors()
    {
        var sfd = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "visitors_report.csv" };
        if (sfd.ShowDialog() == true)
            _services.ReportingService.ExportVisitorsCsv(Visitors, sfd.FileName);
    }

    private void Backup()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GovRegistryBackups");
        _services.BackupService.Backup(dir);
    }

    private void Restore()
    {
        var ofd = new OpenFileDialog { Filter = "SQLite DB|*.db" };
        if (ofd.ShowDialog() == true)
        {
            _services.BackupService.Restore(ofd.FileName);
            Refresh();
        }
    }

    private void SearchDrivers()
    {
        Drivers.Clear();
        foreach (var d in _services.DriverService.Search(DriverSearchName, DriverSearchPlate, ReportFromDate.Date, ReportToDate.Date.AddDays(1).AddTicks(-1)))
            Drivers.Add(d);
    }

    private void SearchVisitors()
    {
        Visitors.Clear();
        foreach (var v in _services.VisitorService.Search(VisitorSearchName, VisitorSearchIdentity, ReportFromDate.Date, ReportToDate.Date.AddDays(1).AddTicks(-1)))
            Visitors.Add(v);
    }

    private void ExportDailyDrivers() => ExportDriversSet(_services.DriverService.Search(null, null, DateTime.Today, DateTime.Today.AddDays(1).AddTicks(-1)), "drivers_daily.csv");
    private void ExportDriversByRange() => ExportDriversSet(_services.DriverService.Search(null, null, ReportFromDate.Date, ReportToDate.Date.AddDays(1).AddTicks(-1)), "drivers_range.csv");
    private void ExportDailyVisitors() => ExportVisitorsSet(_services.VisitorService.Search(null, null, DateTime.Today, DateTime.Today.AddDays(1).AddTicks(-1)), "visitors_daily.csv");
    private void ExportVisitorsByRange() => ExportVisitorsSet(_services.VisitorService.Search(null, null, ReportFromDate.Date, ReportToDate.Date.AddDays(1).AddTicks(-1)), "visitors_range.csv");

    private void ExportActiveStatus()
    {
        var activeDrivers = _services.DriverService.GetActive();
        var activeVisitors = _services.VisitorService.GetInsideNow();
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        _services.ReportingService.ExportDriversCsv(activeDrivers, Path.Combine(folder, "active_drivers.csv"));
        _services.ReportingService.ExportVisitorsCsv(activeVisitors, Path.Combine(folder, "active_visitors.csv"));
    }

    private void ExportDriversSet(IEnumerable<DriverRecord> records, string defaultName)
    {
        var sfd = new SaveFileDialog { Filter = "CSV|*.csv", FileName = defaultName };
        if (sfd.ShowDialog() == true)
            _services.ReportingService.ExportDriversCsv(records, sfd.FileName);
    }

    private void ExportVisitorsSet(IEnumerable<VisitorRecord> records, string defaultName)
    {
        var sfd = new SaveFileDialog { Filter = "CSV|*.csv", FileName = defaultName };
        if (sfd.ShowDialog() == true)
            _services.ReportingService.ExportVisitorsCsv(records, sfd.FileName);
    }
}
