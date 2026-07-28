using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ladeco.Diag.App.Models;
using Ladeco.Diag.App.Services;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;
using Ladeco.Diag.Reporting.Abstractions;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Ladeco.Diag.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDiagnosticsOrchestrator _orchestrator;
    private readonly ISystemSnapshotProvider _snapshotProvider;
    private readonly IReportExporter _reportExporter;
    private readonly IThemeService _themeService;
    private readonly IConfirmationDialogService _confirmationDialog;
    private readonly IRemediationService _remediationService;
    private readonly ILocalizationService _localization;
    private readonly IScanHistoryRepository _scanHistoryRepository;
    private readonly IFindingExplanationService _findingExplanationService;
    private readonly StorageOptions _storageOptions;
    private readonly AppOptions _appOptions;

    private ScanReport? _latestReport;

    public MainViewModel(
        IDiagnosticsOrchestrator orchestrator,
        ISystemSnapshotProvider snapshotProvider,
        IReportExporter reportExporter,
        IThemeService themeService,
        IConfirmationDialogService confirmationDialog,
        IRemediationService remediationService,
        ILocalizationService localization,
        IScanHistoryRepository scanHistoryRepository,
        IFindingExplanationService findingExplanationService,
        StorageOptions storageOptions,
        AppOptions appOptions)
    {
        _orchestrator = orchestrator;
        _snapshotProvider = snapshotProvider;
        _reportExporter = reportExporter;
        _themeService = themeService;
        _confirmationDialog = confirmationDialog;
        _remediationService = remediationService;
        _localization = localization;
        _scanHistoryRepository = scanHistoryRepository;
        _findingExplanationService = findingExplanationService;
        _storageOptions = storageOptions;
        _appOptions = appOptions;

        RunScanLabel = _localization["Dashboard.RunScan"];
        GeneratePdfLabel = _localization["Dashboard.GenerateReport"];
        StatusText = _localization["Dashboard.Status.Ready"];
        RecentScanSummary = _localization["History.None"];
        AiAssistantSummary = _localization["AI.NoScan"];

        Findings = new ObservableCollection<FindingItem>();

        var cpu = new ObservableCollection<double>();
        var ram = new ObservableCollection<double>();
        var disk = new ObservableCollection<double>();

        ResourceSeries = new ObservableCollection<ISeries>
        {
            new LineSeries<double> { Values = cpu, Name = "CPU", Fill = null, Stroke = new SolidColorPaint(new SKColor(0, 102, 255), 3) },
            new LineSeries<double> { Values = ram, Name = "RAM", Fill = null, Stroke = new SolidColorPaint(new SKColor(16, 185, 129), 3) },
            new LineSeries<double> { Values = disk, Name = "Disk", Fill = null, Stroke = new SolidColorPaint(new SKColor(249, 115, 22), 3) }
        };

        XAxes = [new Axis { Name = "Samples" }];
        YAxes = [new Axis { Name = "%", MinLimit = 0, MaxLimit = 100 }];

        IsBusy = true;
        _ = Task.Run(async () =>
        {
            await RefreshRuntimeSnapshotAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = false);
        });
    }

    public ObservableCollection<FindingItem> Findings { get; }

    public ObservableCollection<ISeries> ResourceSeries { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    [ObservableProperty] private string runScanLabel = string.Empty;
    [ObservableProperty] private string generatePdfLabel = string.Empty;
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private string actionOutput = string.Empty;

    [ObservableProperty] private string computerName = "-";
    [ObservableProperty] private string userName = "-";
    [ObservableProperty] private string windowsVersion = "-";
    [ObservableProperty] private string windowsBuild = "-";
    [ObservableProperty] private string windowsEdition = "-";
    [ObservableProperty] private string lastUpdateDisplay = "-";
    [ObservableProperty] private string ipAddress = "-";
    [ObservableProperty] private string publicIpAddress = "-";
    [ObservableProperty] private string macAddress = "-";
    [ObservableProperty] private string uptime = "-";
    [ObservableProperty] private string cpuUsageDisplay = "-";
    [ObservableProperty] private string ramUsageDisplay = "-";
    [ObservableProperty] private string storageUsageDisplay = "-";
    [ObservableProperty] private string cpuTemperatureDisplay = "-";
    [ObservableProperty] private string internetStatus = "-";
    [ObservableProperty] private string defenderStatus = "-";
    [ObservableProperty] private string bitLockerStatus = "-";
    [ObservableProperty] private string batteryStatus = "-";
    [ObservableProperty] private string networkType = "-";
    [ObservableProperty] private string wifiSignalStrength = "-";
    [ObservableProperty] private string networkSpeed = "-";
    [ObservableProperty] private string cpuName = "-";
    [ObservableProperty] private string gpuName = "-";
    [ObservableProperty] private string totalRam = "-";
    [ObservableProperty] private string recentScanSummary = string.Empty;
    [ObservableProperty] private string aiAssistantSummary = string.Empty;
    [ObservableProperty] private bool isBusy = false;
    [ObservableProperty] private bool isActionRunning = false;
    [ObservableProperty] private string lastPdfPath = string.Empty;
    [ObservableProperty] private bool canOpenPdf = false;
    [ObservableProperty] private string currentActionName = string.Empty;
    
    private CancellationTokenSource? _actionCts;

    [RelayCommand]
    private async Task RunScanAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = _localization["Dashboard.Status.Scanning"] + "... (Even geduld a.u.b.)";
        Findings.Clear();

        try
        {
            _latestReport = await Task.Run(() => _orchestrator.RunFullScanAsync());

            foreach (var finding in _latestReport.AllFindings.OrderByDescending(x => x.Priority))
            {
                Findings.Add(new FindingItem(
                    finding.Title,
                    finding.Severity.ToString(),
                    finding.ProbableCause,
                    finding.Resolution,
                    finding.Difficulty,
                    finding.Priority,
                    finding.AutoFixAvailable,
                    finding.AutoFixCommand));
            }

            var topFinding = _latestReport.AllFindings.OrderByDescending(x => x.Priority).FirstOrDefault();
            AiAssistantSummary = topFinding is null
                ? _localization["AI.NoCritical"]
                : _findingExplanationService.BuildExplanation(topFinding);

            await _scanHistoryRepository.SaveAsync(_appOptions.DefaultCustomerName, _latestReport);
            var history = await _scanHistoryRepository.SearchAsync(_appOptions.DefaultCustomerName, _latestReport.ComputerName, take: 1);
            var latest = history.FirstOrDefault();
            if (latest is not null)
            {
                RecentScanSummary = $"Laatste scan: {latest.ScannedAt:dd/MM/yyyy HH:mm} | Issues: {latest.FindingCount} | Hoog/Kritiek: {latest.HighPriorityFindingCount}";
            }

            StatusText = _localization["Dashboard.Status.Completed"];
        }
        catch (Exception ex)
        {
            StatusText = "Er is een fout opgetreden: " + ex.Message;
        }
        finally
        {
            await RefreshRuntimeSnapshotAsync();
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GeneratePdfAsync()
    {
        if (_latestReport is null)
        {
            ActionOutput = _localization["Action.ScanFirst"];
            return;
        }

        var path = await _reportExporter.ExportPdfAsync(
            _latestReport,
            _appOptions.DefaultCustomerName,
            _appOptions.DefaultTechnicianName,
            _storageOptions.ReportOutputDirectory);

        _ = await _reportExporter.ExportJsonAsync(_latestReport, _storageOptions.ReportOutputDirectory);
        _ = await _reportExporter.ExportCsvAsync(_latestReport, _storageOptions.ReportOutputDirectory);
        _ = await _reportExporter.ExportHtmlAsync(_latestReport, _appOptions.DefaultCustomerName, _appOptions.DefaultTechnicianName, _storageOptions.ReportOutputDirectory);

        LastPdfPath = System.IO.Path.GetFullPath(path);
        CanOpenPdf = true;

        ActionOutput = $"Rapporten opgeslagen in: {System.IO.Path.GetFullPath(_storageOptions.ReportOutputDirectory)} (PDF: {System.IO.Path.GetFileName(path)})";
    }

    [RelayCommand]
    private void OpenPdf()
    {
        if (!string.IsNullOrEmpty(LastPdfPath) && System.IO.File.Exists(LastPdfPath))
        {
            var p = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo(LastPdfPath) { UseShellExecute = true }
            };
            p.Start();
        }
    }

    [RelayCommand]
    private void LightTheme() => _themeService.ApplyLightTheme();

    [RelayCommand]
    private void DarkTheme() => _themeService.ApplyDarkTheme();

    [RelayCommand]
    private Task FlushDnsAsync() => ExecuteRemediationAsync("FlushDns", _localization["Action.FlushDns"]);

    [RelayCommand]
    private Task RenewIpAsync() => ExecuteRemediationAsync("RenewIp", _localization["Action.RenewIp"]);

    [RelayCommand]
    private Task DnsResetAsync() => ExecuteRemediationAsync("DnsReset", _localization["Action.DnsReset"]);

    [RelayCommand]
    private Task DismAsync() => ExecuteRemediationAsync("DismRestoreHealth", _localization["Action.Dism"]);

    [RelayCommand]
    private Task SfcAsync() => ExecuteRemediationAsync("SfcScannow", _localization["Action.Sfc"]);

    [RelayCommand]
    private Task ChkdskAsync() => ExecuteRemediationAsync("CheckDisk", _localization["Action.Chkdsk"]);

    [RelayCommand]
    private Task ResetWindowsUpdateAsync() => ExecuteRemediationAsync("ResetWindowsUpdate", _localization["Action.ResetWU"]);

    [RelayCommand]
    private Task RestartSpoolerAsync() => ExecuteRemediationAsync("RestartPrintSpooler", _localization["Action.Spooler"]);

    [RelayCommand]
    private Task WinsockAsync() => ExecuteRemediationAsync("ResetWinsock", _localization["Action.Winsock"]);

    [RelayCommand]
    private Task NetworkResetAsync() => ExecuteRemediationAsync("ResetNetwork", _localization["Action.NetworkReset"]);

    [RelayCommand]
    private Task RestartExplorerAsync() => ExecuteRemediationAsync("RestartExplorer", _localization["Action.RestartExplorer"]);

    [RelayCommand]
    private Task CleanTempFilesAsync() => ExecuteRemediationAsync("CleanTempFiles", _localization["Action.CleanTemp"]);

    [RelayCommand]
    private Task CleanWindowsCacheAsync() => ExecuteRemediationAsync("CleanWindowsCache", _localization["Action.CleanCache"]);

    [RelayCommand]
    private Task RunDeepDefenderScanAsync() => ExecuteRemediationAsync("RunDeepDefenderScan", "Weet u zeker dat u een volledige malware/virus scan wilt uitvoeren? (Dit kan lang duren)");

    [RelayCommand]
    private Task RunDefenderScanAsync() => ExecuteRemediationAsync("RunDefenderScan", "Weet u zeker dat u een quick scan van Windows Defender wilt starten?");

    [RelayCommand]
    private Task ClearEventLogsAsync() => ExecuteRemediationAsync("ClearEventLogs", "Dit wist alle Windows Event Viewer logs. Doorgaan?");

    [RelayCommand]
    private Task DefragDriveAsync() => ExecuteRemediationAsync("DefragDrive", "Weet u zeker dat u de systeemschijf wilt optimaliseren/defragmenteren?");

    [RelayCommand]
    private Task FullDebloatAsync() => ExecuteRemediationAsync("FullDebloat", "Waarschuwing: Hiermee worden alle bloatware en voorgeïnstalleerde Windows apps (behalve essentials) verwijderd. Doorgaan?");

    [RelayCommand]
    private Task RunSpeedtestAsync() => ExecuteRemediationAsync("RunSpeedtest", "Wil je een speedtest starten? Dit kan enkele seconden duren.");

    [RelayCommand]
    private Task RebootSystemAsync() => ExecuteRemediationAsync("RebootSystem", _localization["Action.Reboot"]);

    [RelayCommand]
    private void CancelAction()
    {
        if (_actionCts != null && !_actionCts.IsCancellationRequested)
        {
            _actionCts.Cancel();
        }
    }

    private async Task ExecuteRemediationAsync(string actionKey, string confirmationText)
    {
        if (!_confirmationDialog.Confirm(_localization["Action.ConfirmTitle"], confirmationText))
        {
            return;
        }

        CurrentActionName = $"Actie '{actionKey}' wordt uitgevoerd...";
        IsActionRunning = true;
        ActionOutput = $"Laden van PowerShell/Command module voor {actionKey}...";
        
        _actionCts = new CancellationTokenSource();
        
        try
        {
            var (success, output) = await Task.Run(() => _remediationService.RunSafeActionAsync(actionKey, _actionCts.Token), _actionCts.Token);
            ActionOutput = success ? $"{_localization["Action.Success"]}:\n{output}" : $"{_localization["Action.Failed"]}:\n{output}";
        }
        catch (OperationCanceledException)
        {
             ActionOutput = "Actie werd afgebroken door de gebruiker.";
        }
        catch (Exception ex)
        {
             ActionOutput = $"Fout: {ex.Message}";
        }
        finally
        {
             IsActionRunning = false;
             _actionCts.Dispose();
             _actionCts = null;
        }
    }

    private async Task RefreshRuntimeSnapshotAsync()
    {
        var snapshot = await _snapshotProvider.GetSnapshotAsync();

        ComputerName = snapshot.ComputerName;
        UserName = snapshot.UserName;
        WindowsVersion = snapshot.WindowsVersion;
        WindowsBuild = snapshot.WindowsBuild;
        WindowsEdition = snapshot.WindowsEdition;
        LastUpdateDisplay = snapshot.LastUpdate == DateTimeOffset.MinValue ? "Onbekend" : snapshot.LastUpdate.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        IpAddress = snapshot.IpAddress;
        PublicIpAddress = snapshot.PublicIpAddress;
        MacAddress = snapshot.MacAddress;
        Uptime = snapshot.Uptime;
        CpuUsageDisplay = $"{snapshot.CpuUsagePercent:F1}%";
        RamUsageDisplay = $"{snapshot.RamUsagePercent:F1}%";
        StorageUsageDisplay = $"{snapshot.StorageUsagePercent:F1}%";
        CpuTemperatureDisplay = snapshot.CpuTemperatureCelsius.HasValue ? $"{snapshot.CpuTemperatureCelsius:F1} C" : "N/A";
        InternetStatus = snapshot.InternetAvailable ? "Online" : "Offline";
        DefenderStatus = snapshot.DefenderStatus;
        BitLockerStatus = snapshot.BitLockerStatus;
        BatteryStatus = snapshot.BatteryStatus;
        NetworkType = snapshot.NetworkType;
        WifiSignalStrength = snapshot.WifiSignalStrength;
        NetworkSpeed = snapshot.NetworkSpeed;
        CpuName = snapshot.CpuName;
        GpuName = snapshot.GpuName;
        TotalRam = snapshot.TotalRam;

        AppendSample(0, snapshot.CpuUsagePercent);
        AppendSample(1, snapshot.RamUsagePercent);
        AppendSample(2, snapshot.StorageUsagePercent);
    }

    private void AppendSample(int seriesIndex, double value)
    {
        if (ResourceSeries[seriesIndex] is not LineSeries<double> line || line.Values is not ObservableCollection<double> values)
        {
            return;
        }

        values.Add(Math.Clamp(value, 0, 100));
        if (values.Count > 24)
        {
            values.RemoveAt(0);
        }
    }
}
