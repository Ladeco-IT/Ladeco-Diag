using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Application.Abstractions;

public interface IScanHistoryRepository
{
    Task SaveAsync(string customerName, ScanReport report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScanReportSummary>> SearchAsync(string? customerName, string? computerName, int take = 100, CancellationToken cancellationToken = default);
}

public sealed record ScanReportSummary(
    Guid Id,
    string CustomerName,
    string ComputerName,
    string UserName,
    DateTimeOffset ScannedAt,
    int FindingCount,
    int HighPriorityFindingCount
);
