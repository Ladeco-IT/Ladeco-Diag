using System.Management;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class TemperatureDiagnosticsModule : IDiagnosticModule
{
    public string Name => "Temperatures";

    public Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();
        var measurements = new Dictionary<string, string>();

        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            var rows = searcher.Get().Cast<ManagementObject>().ToList();

            foreach (var row in rows)
            {
                var instance = row["InstanceName"]?.ToString() ?? "ThermalZone";
                var celsius = Math.Round((Convert.ToDouble(row["CurrentTemperature"]) / 10) - 273.15, 1);
                measurements[instance] = celsius.ToString("F1");

                if (celsius >= 88)
                {
                    findings.Add(new Finding(
                        "TEMP_OVERHEAT",
                        $"Oververhitting gedetecteerd: {instance}",
                        "Thermische zone rapporteert kritieke temperatuur.",
                        "Koelingsprobleem, stof of hoge belasting.",
                        "Controleer koeling en reinig airflow.",
                        DiagnosticSeverity.Critical,
                        94,
                        false,
                        null,
                        25,
                        "Gemiddeld"));
                }
            }
        }
        catch (Exception ex)
        {
            findings.Add(new Finding(
                "TEMP_READ_FAILED",
                "Temperatuursensoren niet beschikbaar",
                "Thermische waarden konden niet worden gelezen.",
                ex.Message,
                "Controleer ACPI/WMI ondersteuning of OEM tooling.",
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
