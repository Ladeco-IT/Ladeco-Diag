namespace Ladeco.Diag.Domain.Diagnostics;

public sealed record ModuleResult(
    string ModuleName,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<Finding> Findings,
    IReadOnlyDictionary<string, string> Measurements
);
