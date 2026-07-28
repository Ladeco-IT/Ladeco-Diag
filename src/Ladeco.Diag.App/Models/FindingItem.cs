namespace Ladeco.Diag.App.Models;

public sealed record FindingItem(
    string Title,
    string Severity,
    string Cause,
    string Resolution,
    string Difficulty,
    int Priority,
    bool AutoFixAvailable,
    string? AutoFixCommand
);
