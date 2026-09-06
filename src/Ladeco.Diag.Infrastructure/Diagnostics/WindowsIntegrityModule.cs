using System.Management;
using Microsoft.Win32;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class WindowsIntegrityModule : IDiagnosticModule
{
    public string Name => "WindowsIntegrity";

    public Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();
        var measurements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var updateServiceRunning = ReadServiceRunning("wuauserv");
        measurements["WindowsUpdateService"] = updateServiceRunning ? "Running" : "Stopped";
        if (!updateServiceRunning)
        {
            findings.Add(new Finding(
                "WINDOWS_UPDATE_SERVICE_STOPPED",
                "Windows Update service gestopt",
                "De Windows Update service draait niet.",
                "Serviceconfiguratie of policy.",
                "Start de service en herstel updatecomponenten.",
                DiagnosticSeverity.High,
                82,
                true,
                "sc start wuauserv",
                10,
                "Makkelijk"));
        }

            var restartRequired = IsWindowsUpdateRestartRequired();
            measurements["WindowsUpdateRestartRequired"] = restartRequired ? "Yes" : "No";
            if (restartRequired)
            {
                findings.Add(new Finding(
                "WINDOWS_UPDATE_RESTART_REQUIRED",
                "Windows Update wacht op herstart",
                "Een geïnstalleerde Windows-update is pas volledig actief na een herstart.",
                "Windows Update heeft bestanden of systeemcomponenten gepland voor vervanging.",
                "Sla uw werk op en herstart de computer om de update af te ronden.",
                DiagnosticSeverity.Medium,
                70,
                true,
                "shutdown /r /t 15",
                10,
                "Makkelijk"));
            }

        var firewallEnabled = ReadFirewallEnabled();
        measurements["FirewallEnabled"] = firewallEnabled ? "Yes" : "No";
        if (!firewallEnabled)
        {
            findings.Add(new Finding(
                "FIREWALL_DISABLED",
                "Windows Firewall uitgeschakeld",
                "Firewall-profielen lijken niet actief.",
                "Lokale wijziging of policy.",
                "Activeer firewall voor domain/private/public profielen.",
                DiagnosticSeverity.Critical,
                92,
                false,
                null,
                10,
                "Makkelijk"));
        }

        var activationStatus = ReadWindowsActivationStatus();
        measurements["WindowsActivation"] = activationStatus;
        if (!activationStatus.Equals("Licensed", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new Finding(
                "WINDOWS_NOT_ACTIVATED",
                "Windows activatieprobleem",
                "Windows lijkt niet correct geactiveerd.",
                "Licentie- of KMS-probleem.",
                "Controleer productlicentie en activatieserver.",
                DiagnosticSeverity.Medium,
                58,
                false,
                null,
                15,
                "Gemiddeld"));
        }

        return Task.FromResult(new ModuleResult(Name, started, DateTimeOffset.UtcNow, findings, measurements));
    }

    private static bool ReadServiceRunning(string serviceName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT State FROM Win32_Service WHERE Name = '{serviceName}'");
            var state = searcher.Get().Cast<ManagementObject>().FirstOrDefault()?["State"]?.ToString();
            return state?.Equals("Running", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ReadFirewallEnabled()
    {
        try
        {
            const string path = "SYSTEM\\CurrentControlSet\\Services\\SharedAccess\\Parameters\\FirewallPolicy\\StandardProfile";
            using var key = Registry.LocalMachine.OpenSubKey(path);
            var enabled = key?.GetValue("EnableFirewall");
            return Convert.ToInt32(enabled ?? 0) == 1;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWindowsUpdateRestartRequired()
    {
        try
        {
            const string path = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate\\Auto Update\\RebootRequired";
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string ReadWindowsActivationStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL");
            var statuses = searcher.Get().Cast<ManagementObject>().Select(x => Convert.ToInt32(x["LicenseStatus"])).ToList();
            if (statuses.Count == 0)
            {
                return "Unknown";
            }

            return statuses.Any(x => x == 1) ? "Licensed" : "Unlicensed";
        }
        catch
        {
            return "Unknown";
        }
    }
}
