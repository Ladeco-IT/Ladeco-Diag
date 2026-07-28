namespace Ladeco.Diag.Domain.Diagnostics;

public sealed class ScanReport
{
    public required Guid Id { get; init; }
    public required string ComputerName { get; init; }
    public required string UserName { get; init; }
    public required DateTimeOffset ScannedAt { get; init; }
    public required IReadOnlyList<ModuleResult> ModuleResults { get; init; }
    public IReadOnlyList<Finding> AllFindings => ModuleResults.SelectMany(x => x.Findings).ToList();

    public IReadOnlyList<Finding> HighPriorityFindings => AllFindings
        .Where(x => x.Severity >= DiagnosticSeverity.High)
        .OrderByDescending(x => x.Priority)
        .ToList();
}
