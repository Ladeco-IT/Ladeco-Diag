using System.Management;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class PrinterDiagnosticsModule : IDiagnosticModule
{
    public string Name => "Printers";

    public Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();
        var measurements = new Dictionary<string, string>();

        try
        {
            using var printerSearcher = new ManagementObjectSearcher("SELECT Name,WorkOffline,PrinterStatus,DriverName FROM Win32_Printer");
            var printers = printerSearcher.Get().Cast<ManagementObject>().ToList();
            measurements["PrinterCount"] = printers.Count.ToString();
            measurements["Printers"] = string.Join("; ", printers.Select(x => x["Name"]?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)));

            foreach (var printer in printers)
            {
                var name = printer["Name"]?.ToString() ?? "Onbekend";
                var offline = Convert.ToBoolean(printer["WorkOffline"] ?? false);
                var status = Convert.ToInt32(printer["PrinterStatus"] ?? 0);
                var driver = printer["DriverName"]?.ToString() ?? "N/A";

                if (offline || status is 7)
                {
                    findings.Add(new Finding(
                        "PRINTER_OFFLINE",
                        $"Printer offline: {name}",
                        "Printer is offline of niet beschikbaar.",
                        "Netwerkverbinding of spoolerprobleem.",
                        "Controleer printerverbinding en herstart spooler.",
                        DiagnosticSeverity.Medium,
                        65,
                        true,
                        "powershell -NoProfile -Command \"Restart-Service spooler -Force\"",
                        10,
                        "Makkelijk"));
                }

                if (driver.Contains("Class Driver", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new Finding(
                        "PRINTER_GENERIC_DRIVER",
                        $"Generieke driver: {name}",
                        "Printer gebruikt een generieke class driver.",
                        "OEM-driver niet geinstalleerd.",
                        "Installeer de nieuwste OEM printerdriver.",
                        DiagnosticSeverity.Low,
                        40,
                        false,
                        null,
                        15,
                        "Makkelijk"));
                }
            }
        }
        catch (Exception ex)
        {
            findings.Add(new Finding(
                "PRINTER_SCAN_FAILED",
                "Printerdiagnose mislukt",
                "Printerinformatie kon niet worden uitgelezen.",
                ex.Message,
                "Controleer WMI-service en rechten.",
                DiagnosticSeverity.Low,
                20,
                false,
                null,
                5,
                "Makkelijk"));
        }

        return Task.FromResult(new ModuleResult(Name, started, DateTimeOffset.UtcNow, findings, measurements));
    }
}
