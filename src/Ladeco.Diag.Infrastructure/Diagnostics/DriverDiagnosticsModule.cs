using System.Management;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class DriverDiagnosticsModule : IDiagnosticModule
{
    public string Name => "Drivers";

    public Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();
        var measurements = new Dictionary<string, string>();

        try
        {
            using var signedDriverSearcher = new ManagementObjectSearcher("SELECT DeviceName, DriverVersion, DriverDate FROM Win32_PnPSignedDriver");
            var drivers = signedDriverSearcher.Get().Cast<ManagementObject>().ToList();
            measurements["DriverCount"] = drivers.Count.ToString();

            var oldDrivers = drivers
                .Where(x => DateTime.TryParse(x["DriverDate"]?.ToString(), out var dt) && dt < DateTime.UtcNow.AddYears(-5))
                .Take(10)
                .ToList();

            if (oldDrivers.Count > 0)
            {
                findings.Add(new Finding(
                    "DRIVER_OLD",
                    "Mogelijk verouderde drivers",
                    "Er zijn drivers gevonden met oude driverdatum.",
                    string.Join(", ", oldDrivers.Select(x => x["DeviceName"]?.ToString() ?? "Onbekend")),
                    "Update de device drivers via OEM support tools.",
                    DiagnosticSeverity.Medium,
                    60,
                    false,
                    null,
                    30,
                    "Gemiddeld"));
            }

            using var unknownDeviceSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");
            var faultyDevices = unknownDeviceSearcher.Get().Cast<ManagementObject>().ToList();
            measurements["DevicesWithErrors"] = faultyDevices.Count.ToString();

            if (faultyDevices.Count > 0)
            {
                findings.Add(new Finding(
                    "DRIVER_DEVICE_ERRORS",
                    "Apparaatbeheer meldt fouten",
                    "Een of meer apparaten rapporteren een ConfigManagerErrorCode.",
                    string.Join(", ", faultyDevices.Select(x => x["Name"]?.ToString() ?? "Onbekend")),
                    "Herinstalleer de juiste drivers of controleer hardwareverbinding.",
                    DiagnosticSeverity.High,
                    80,
                    false,
                    null,
                    25,
                    "Gemiddeld"));
            }
        }
        catch (Exception ex)
        {
            findings.Add(new Finding(
                "DRIVER_SCAN_FAILED",
                "Driveranalyse mislukt",
                "Driverinformatie kon niet volledig worden uitgelezen.",
                ex.Message,
                "Controleer WMI-health en rechten.",
                DiagnosticSeverity.Low,
                20,
                false,
                null,
                10,
                "Makkelijk"));
        }

        return Task.FromResult(new ModuleResult(Name, started, DateTimeOffset.UtcNow, findings, measurements));
    }
}
