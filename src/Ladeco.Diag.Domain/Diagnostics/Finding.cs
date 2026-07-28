namespace Ladeco.Diag.Domain.Diagnostics;

public sealed record Finding(
    string Code,
    string Title,
    string Description,
    string ProbableCause,
    string Resolution,
    DiagnosticSeverity Severity,
    int Priority,
    bool AutoFixAvailable,
    string? AutoFixCommand,
    int EstimatedMinutes,
    string Difficulty
);
