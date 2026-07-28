using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Reporting.Abstractions;

public interface IReportExporter
{
    Task<string> ExportPdfAsync(ScanReport report, string customerName, string technicianName, string outputDirectory, CancellationToken cancellationToken = default);
    Task<string> ExportJsonAsync(ScanReport report, string outputDirectory, CancellationToken cancellationToken = default);
    Task<string> ExportCsvAsync(ScanReport report, string outputDirectory, CancellationToken cancellationToken = default);
    Task<string> ExportHtmlAsync(ScanReport report, string customerName, string technicianName, string outputDirectory, CancellationToken cancellationToken = default);
}
