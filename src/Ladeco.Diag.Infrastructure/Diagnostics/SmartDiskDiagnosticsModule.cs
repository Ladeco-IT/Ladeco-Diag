using System.Management;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class SmartDiskDiagnosticsModule : IDiagnosticModule
{
    public string Name => "SmartDisk";

    public Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();
        var measurements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var statusSearcher = new ManagementObjectSearcher(@"root\WMI", "SELECT InstanceName, PredictFailure FROM MSStorageDriver_FailurePredictStatus");
            var rows = statusSearcher.Get().Cast<ManagementObject>().ToList();

            measurements["SmartDeviceCount"] = rows.Count.ToString();
            measurements["SmartDevices"] = string.Join("; ", rows.Select(x => x["InstanceName"]?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)));

            foreach (var row in rows)
            {
                var device = row["InstanceName"]?.ToString() ?? "Onbekend device";
                var willFail = Convert.ToBoolean(row["PredictFailure"] ?? false);
                if (willFail)
                {
                    findings.Add(new Finding(
                        "SMART_PREDICT_FAILURE",
                        $"SMART waarschuwing: {device}",
                        "SMART voorspelt mogelijk schijffalen.",
                        "Reallocated sectors of hardware degradation.",
                        "Maak onmiddellijk backup en vervang de schijf.",
                        DiagnosticSeverity.Critical,
                        98,
                        false,
                        null,
                        60,
                        "Gemiddeld"));
                }
            }
        }
        catch (Exception ex)
        {
            findings.Add(new Finding(
                "SMART_READ_FAILED",
                "SMART-data niet beschikbaar",
                "SMART-informatie kon niet worden uitgelezen.",
                ex.Message,
                "Controleer storage-controller drivers en WMI-provider.",
                DiagnosticSeverity.Low,
                30,
                false,
                null,
                10,
                "Gemiddeld"));
        }

        return Task.FromResult(new ModuleResult(Name, started, DateTimeOffset.UtcNow, findings, measurements));
    }
}
