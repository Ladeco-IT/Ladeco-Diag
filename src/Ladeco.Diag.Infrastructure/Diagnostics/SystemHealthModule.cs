using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class SystemHealthModule : IDiagnosticModule
{
    private readonly ISystemSnapshotProvider _snapshotProvider;

    public SystemHealthModule(ISystemSnapshotProvider snapshotProvider)
    {
        _snapshotProvider = snapshotProvider;
    }

    public string Name => "SystemHealth";

    public async Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var snapshot = await _snapshotProvider.GetSnapshotAsync(cancellationToken);

        var findings = new List<Finding>();
        if (snapshot.StorageUsagePercent >= 90)
        {
            findings.Add(new Finding(
                Code: "STORAGE_CRITICAL",
                Title: "Opslag bijna vol",
                Description: "Systeemschijf heeft minder dan 10% vrije ruimte.",
                ProbableCause: "Grote tijdelijke bestanden of langdurige software-installaties.",
                Resolution: "Ruim tijdelijke bestanden op en verplaats grote data.",
                Severity: DiagnosticSeverity.High,
                Priority: 90,
                AutoFixAvailable: true,
                AutoFixCommand: "cleanmgr /sagerun:1",
                EstimatedMinutes: 10,
                Difficulty: "Makkelijk"));
        }

        if (snapshot.RamUsagePercent >= 90)
        {
            findings.Add(new Finding(
                Code: "RAM_HIGH",
                Title: "RAM-gebruik zeer hoog",
                Description: "RAM-belasting is hoger dan 90%.",
                ProbableCause: "Te veel actieve processen of memory leak.",
                Resolution: "Analyseer processen en herstart zware applicaties.",
                Severity: DiagnosticSeverity.Medium,
                Priority: 75,
                AutoFixAvailable: false,
                AutoFixCommand: null,
                EstimatedMinutes: 15,
                Difficulty: "Gemiddeld"));
        }

        if (snapshot.CpuTemperatureCelsius is >= 88)
        {
            findings.Add(new Finding(
                Code: "CPU_OVERHEAT",
                Title: "CPU oververhit",
                Description: "CPU-temperatuur is hoger dan 88C.",
                ProbableCause: "Stof, onvoldoende airflow of defecte koeling.",
                Resolution: "Controleer ventilatie, koelprofielen en thermische pasta.",
                Severity: DiagnosticSeverity.Critical,
                Priority: 95,
                AutoFixAvailable: false,
                AutoFixCommand: null,
                EstimatedMinutes: 30,
                Difficulty: "Moeilijk"));
        }

        var measurements = new Dictionary<string, string>
        {
            ["CpuUsagePercent"] = snapshot.CpuUsagePercent.ToString("F2"),
            ["RamUsagePercent"] = snapshot.RamUsagePercent.ToString("F2"),
            ["StorageUsagePercent"] = snapshot.StorageUsagePercent.ToString("F2"),
            ["CpuTemperatureCelsius"] = snapshot.CpuTemperatureCelsius?.ToString("F1") ?? "N/A"
        };

        return new ModuleResult(Name, started, DateTimeOffset.UtcNow, findings, measurements);
    }
}
