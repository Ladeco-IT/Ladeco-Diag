using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
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

using System.Collections.ObjectModel;
namespace Ladeco.Diag.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDiagnosticsOrchestrator _orchestrator;
    private readonly ISystemSnapshotProvider _snapshotProvider;
    private readonly IHardwareInventoryProvider _hardwareInventoryProvider;
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


   [ObservableProperty] private ObservableCollection<string> startupServices = new();
    
    [RelayCommand]
    private void LoadStartupServices()
    {
        try
        {
            StartupServices.Clear();
            var services = System.ServiceProcess.ServiceController.GetServices().Where(s => s.StartType == System.ServiceProcess.ServiceStartMode.Automatic).Take(20);
            foreach (var s in services)
            {
                StartupServices.Add($"{s.DisplayName} ({s.Status})");
            }
        }
        catch { /* Must run as admin to see all, ignore errors for now */ }
    }

   [ObservableProperty] private ObservableCollection<string> runningProcesses = new();

    private void UpdateProcesses()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcesses()
                .OrderByDescending(p => p.WorkingSet64)
                .Take(15)
                .Select(p => $"{p.ProcessName} ({Math.Round(p.WorkingSet64 / 1024.0 / 1024.0, 1)} MB)");
            
            App.Current.Dispatcher.Invoke(() =>
            {
                RunningProcesses.Clear();
                foreach (var p in processes) RunningProcesses.Add(p);
            });
        }
        catch { }
    }

    private System.Timers.Timer? _snapshotTimer;

    public MainViewModel(
        IDiagnosticsOrchestrator orchestrator,
        ISystemSnapshotProvider snapshotProvider,
        IHardwareInventoryProvider hardwareInventoryProvider,
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
        _hardwareInventoryProvider = hardwareInventoryProvider;
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

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        AppVersionString = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "Onbekend";

        IsBusy = true;
        _ = Task.Run(async () =>
        {
            await RefreshRuntimeSnapshotAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = false);
            
            _snapshotTimer = new System.Timers.Timer(5000);
            _snapshotTimer.Elapsed += async (s, e) => {
                try {
                    await RefreshRuntimeSnapshotAsync();
                } catch { }
            };
            _snapshotTimer.Start();
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
    [ObservableProperty] private string internetLatency = "-";
    [ObservableProperty] private string defenderStatus = "-";
    [ObservableProperty] private string bitLockerStatus = "-";
    [ObservableProperty] private string batteryStatus = "-";
    [ObservableProperty] private string networkType = "-";
    [ObservableProperty] private string wifiSignalStrength = "-";
    [ObservableProperty] private string networkSpeed = "-";
    [ObservableProperty] private string cpuName = "-";
    [ObservableProperty] private string gpuName = "-";
    [ObservableProperty] private string totalRam = "-";
    [ObservableProperty] private string hardwareMotherboard = "-";
    [ObservableProperty] private string hardwareBios = "-";
    [ObservableProperty] private string hardwareStorage = "-";
    [ObservableProperty] private string hardwareMonitors = "-";
    [ObservableProperty] private string hardwareResolution = "-";
    [ObservableProperty] private string hardwareNetworkAdapters = "-";
    [ObservableProperty] private string hardwareBluetooth = "-";
    [ObservableProperty] private string hardwareAudio = "-";
    [ObservableProperty] private string hardwareUsb = "-";
    [ObservableProperty] private string hardwarePrinters = "-";
    [ObservableProperty] private string hardwareCameras = "-";
    [ObservableProperty] private string hardwareMics = "-";
    [ObservableProperty] private string hardwareSerials = "-";
    [ObservableProperty] private string recentScanSummary = string.Empty;
    [ObservableProperty] private string aiAssistantSummary = string.Empty;
    [ObservableProperty] private bool isBusy = false;
    [ObservableProperty] private bool isActionRunning = false;
    [ObservableProperty] private bool isActionOverlayVisible = false;
    [ObservableProperty] private string lastPdfPath = string.Empty;
    [ObservableProperty] private bool canOpenPdf = false;
    [ObservableProperty] private string currentActionName = string.Empty;
    [ObservableProperty] private string appVersionString = string.Empty;
    
        [RelayCommand]
    private async Task LoadHardwareAsync()
    {
        try
        {
            var hw = await _hardwareInventoryProvider.GetInventoryAsync();
            HardwareMotherboard = hw.Motherboard;
            HardwareBios = hw.BiosVersion;
            HardwareStorage = hw.StorageDevices;
            HardwareMonitors = hw.Monitors;
            HardwareResolution = hw.Resolution;
            HardwareNetworkAdapters = hw.NetworkAdapters;
            HardwareBluetooth = hw.BluetoothAdapters;
            HardwareAudio = hw.AudioDevices;
            HardwareUsb = hw.UsbDevices;
            HardwarePrinters = hw.Printers;
            HardwareCameras = hw.CameraDevices;
            HardwareMics = hw.Microphones;
            HardwareSerials = hw.SerialNumbers;
        }
        catch { /* ignored */ }
    }

    private CancellationTokenSource? _actionCts;

    [RelayCommand]
    private async Task AutoFixAsync(FindingItem item)
    {
        if (string.IsNullOrWhiteSpace(item.AutoFixCommand)) return;
        await ExecuteRemediationAsync(item.AutoFixCommand, $"Wilt u het probleem '{item.Title}' automatisch oplossen?");
    }

    [RelayCommand]
    private async Task AppUpdateAsync()
    {
        CurrentActionName = "Controleren op updates (GitHub)...";
        IsActionRunning = true;
        IsActionOverlayVisible = true;
        ActionOutput = "Zoeken naar de nieuwste release op GitHub...";

        try
        {
            using var client = new HttpClient();
            // GitHub API required a User-Agent header
            client.DefaultRequestHeaders.Add("User-Agent", "LadecoDiag-Updater");
            
            // NOTE: Using /releases instead of /releases/latest because the release is a "pre-release"
            var response = await client.GetAsync("https://api.github.com/repos/Ladeco-IT/Ladeco-Diag/releases");
            if (!response.IsSuccessStatusCode)
            {
                ActionOutput += $"\n\nKan geen updates vinden. (Status {response.StatusCode}) \nZorg dat de repository public is of dat het pad klopt.";
                CurrentActionName = "Fout bij controleren.";
                IsActionRunning = false;
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.GetArrayLength() == 0)
            {
                ActionOutput += $"\n\nGeen releases gevonden op GitHub.";
                CurrentActionName = "Controle voltooid.";
                IsActionRunning = false;
                return;
            }

            var latestRelease = root[0]; // the newest release is traditionally first in the list
            var latestTag = latestRelease.GetProperty("tag_name").GetString()?.Replace("v", "") ?? "0.0.0";
            
            // Extract the creation or published date of the latest release for comparison
            DateTime onlineDate = DateTime.MinValue;
            if (latestRelease.TryGetProperty("published_at", out var publishedAtElement))
            {
                DateTime.TryParse(publishedAtElement.GetString(), out onlineDate);
            }
            
            ActionOutput += $"\nNieuwste versie online: {latestTag} ({onlineDate:dd/MM/yyyy HH:mm})\nHuidige versie: {AppVersionString.Replace("v", "")}";

            // Check if online date is newer than local build date by scanning the local exe timestamp
            var localExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var localCreateDate = File.GetCreationTime(localExePath);
            
            // If the online Github Release date is later than our local build timestamp (with a small 1 hour wiggle room for timezone differences)
            if (onlineDate > localCreateDate.AddHours(1))
            {
                ActionOutput += "\n\nNieuwe update gevonden! Downloaden...";
                
                // Find installer asset
                string? downloadUrl = null;
                var assets = latestRelease.GetProperty("assets");
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe") && name.Contains("Setup"))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    ActionOutput += "\nKan geen setup .exe vinden in deze release.";
                    IsActionRunning = false;
                    return;
                }

                var tempFile = Path.Combine(Path.GetTempPath(), "LadecoDiagUpdate.exe");
                
                var fileBytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(tempFile, fileBytes);
                
                ActionOutput += "\nDownload voltooid! De installer wordt nu gestart. LadecoDiag wordt afgesloten.";
                
                // Start installer with Silent flag and wait for a second to close current process
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempFile,
                    Arguments = "/SILENT",
                    UseShellExecute = true
                });
                
                System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
                return;
            }
            else
            {
                ActionOutput += "\n\nU heeft al de nieuwste versie!";
            }
        }
        catch (Exception ex)
        {
            ActionOutput += $"\n\nFout tijdens updaten: {ex.Message}";
        }

        CurrentActionName = "Controle voltooid.";
        IsActionRunning = false;
    }

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
    private void CloseActionOverlay()
    {
        IsActionOverlayVisible = false;
    }

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
        IsActionOverlayVisible = true;
        ActionOutput = $"Laden van PowerShell/Command module voor {actionKey}...";
        
        _actionCts = new CancellationTokenSource();
        
        try
        {
            var (success, output) = await Task.Run(() => _remediationService.RunSafeActionAsync(actionKey, _actionCts.Token), _actionCts.Token);
            ActionOutput = success ? $"{_localization["Action.Success"]}:\n{output}\n\n[VOLTOOID]" : $"{_localization["Action.Failed"]}:\n{output}\n\n[VOLTOOID MET FOUTEN]";
        }
        catch (OperationCanceledException)
        {
             ActionOutput = "Actie werd afgebroken door de gebruiker.\n\n[GEANNULEERD]";
        }
        catch (Exception ex)
        {
             ActionOutput = $"Fout: {ex.Message}\n\n[FOUT]";
        }
        finally
        {
             CurrentActionName = $"Actie '{actionKey}' is gestopt.";
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
        InternetLatency = snapshot.LatencyMs >= 0 ? $"{snapshot.LatencyMs} ms" : "N/A";
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
        UpdateProcesses();
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
