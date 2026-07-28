namespace Ladeco.Diag.Infrastructure.Persistence;

public sealed class ScanHistoryEntry
{
    public Guid Id { get; set; }
    public required string CustomerName { get; set; }
    public required string ComputerName { get; set; }
    public required string UserName { get; set; }
    public DateTimeOffset ScannedAt { get; set; }
    public required string ReportJson { get; set; }
}
