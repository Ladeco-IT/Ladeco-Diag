using System.Text.Json;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Ladeco.Diag.Infrastructure.Persistence;

public sealed class ScanHistoryRepository : IScanHistoryRepository
{
    private readonly LadecoDiagDbContext _dbContext;

    public ScanHistoryRepository(LadecoDiagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveAsync(string customerName, ScanReport report, CancellationToken cancellationToken = default)
    {
        var entry = new ScanHistoryEntry
        {
            Id = report.Id,
            CustomerName = customerName,
            ComputerName = report.ComputerName,
            UserName = report.UserName,
            ScannedAt = report.ScannedAt,
            ReportJson = JsonSerializer.Serialize(report)
        };

        _dbContext.ScanHistory.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScanReportSummary>> SearchAsync(string? customerName, string? computerName, int take = 100, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ScanHistory.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            query = query.Where(x => x.CustomerName.Contains(customerName));
        }

        if (!string.IsNullOrWhiteSpace(computerName))
        {
            query = query.Where(x => x.ComputerName.Contains(computerName));
        }

        var entries = await query
            .OrderByDescending(x => x.ScannedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        var result = new List<ScanReportSummary>(entries.Count);
        foreach (var entry in entries)
        {
            try
            {
                var report = JsonSerializer.Deserialize<ScanReport>(entry.ReportJson);
                result.Add(new ScanReportSummary(
                    entry.Id,
                    entry.CustomerName,
                    entry.ComputerName,
                    entry.UserName,
                    entry.ScannedAt,
                    report?.AllFindings.Count ?? 0,
                    report?.HighPriorityFindings.Count ?? 0));
            }
            catch
            {
                result.Add(new ScanReportSummary(
                    entry.Id,
                    entry.CustomerName,
                    entry.ComputerName,
                    entry.UserName,
                    entry.ScannedAt,
                    0,
                    0));
            }
        }

        return result;
    }
}
