using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class SecurityDiagnosticsModule : IDiagnosticModule
{
    private readonly ISystemSnapshotProvider _snapshotProvider;

    public SecurityDiagnosticsModule(ISystemSnapshotProvider snapshotProvider)
    {
        _snapshotProvider = snapshotProvider;
    }

    public string Name => "Security";

    public async Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var snapshot = await _snapshotProvider.GetSnapshotAsync(cancellationToken);
        var findings = new List<Finding>();

        if (!string.Equals(snapshot.DefenderStatus, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new Finding(
                Code: "DEFENDER_DISABLED",
                Title: "Windows Defender uitgeschakeld",
                Description: "Real-time bescherming is niet actief.",
                ProbableCause: "Uitgeschakeld door policy of gebruiker.",
                Resolution: "Activeer real-time bescherming of installeer beheerde AV-oplossing.",
                Severity: DiagnosticSeverity.High,
                Priority: 85,
                AutoFixAvailable: false,
                AutoFixCommand: null,
                EstimatedMinutes: 15,
                Difficulty: "Gemiddeld"));
        }

        if (snapshot.BitLockerStatus.Contains("Partially", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new Finding(
                Code: "BITLOCKER_PARTIAL",
                Title: "BitLocker niet volledig actief",
                Description: "Niet alle volumes hebben actieve encryptiebescherming.",
                ProbableCause: "Onvolledige uitrol of handmatige configuratie.",
                Resolution: "Beveilig alle bedrijfsvolumes met BitLocker.",
                Severity: DiagnosticSeverity.Medium,
                Priority: 60,
                AutoFixAvailable: false,
                AutoFixCommand: null,
                EstimatedMinutes: 20,
                Difficulty: "Gemiddeld"));
        }

        var measurements = new Dictionary<string, string>
        {
            ["DefenderStatus"] = snapshot.DefenderStatus,
            ["BitLockerStatus"] = snapshot.BitLockerStatus
        };

        return new ModuleResult(Name, started, DateTimeOffset.UtcNow, findings, measurements);
    }
}
