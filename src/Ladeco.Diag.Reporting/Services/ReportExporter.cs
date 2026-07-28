using System.Text;
using System.Text.Json;
using Ladeco.Diag.Domain.Diagnostics;
using Ladeco.Diag.Reporting.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ladeco.Diag.Reporting.Services;

public sealed class ReportExporter : IReportExporter
{
    static ReportExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<string> ExportPdfAsync(ScanReport report, string customerName, string technicianName, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, $"LadecoDiag_{report.ComputerName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(32);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Ladeco IT Diagnostisch Rapport").SemiBold().FontSize(18).FontColor("#0066FF");
                        col.Item().Text($"Klant: {customerName}");
                        col.Item().Text($"Technieker: {technicianName}");
                        col.Item().Text($"Datum: {report.ScannedAt:dd/MM/yyyy HH:mm}");
                    });
                });

                page.Content().Column(col =>
                {
                    col.Item().Text("Samenvatting").SemiBold().FontSize(14);
                    col.Item().Text($"Computer: {report.ComputerName}");
                    col.Item().Text($"Gebruiker: {report.UserName}");
                    col.Item().Text($"Aantal issues: {report.AllFindings.Count}");
                    col.Item().Text($"Kritieke/Hoge issues: {report.HighPriorityFindings.Count}");

                    col.Item().PaddingTop(12).Text("Gevonden problemen").SemiBold().FontSize(14);
                    foreach (var finding in report.AllFindings.OrderByDescending(x => x.Priority))
                    {
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(fCol =>
                        {
                            fCol.Item().Text($"[{finding.Severity}] {finding.Title}").SemiBold();
                            fCol.Item().Text($"Oorzaak: {finding.ProbableCause}");
                            fCol.Item().Text($"Risico: {finding.Description}");
                            fCol.Item().Text($"Oplossing: {finding.Resolution}");
                            fCol.Item().Text($"Tijd: {finding.EstimatedMinutes} min | Moeilijkheid: {finding.Difficulty}");
                        });
                    }
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Pagina ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf(path);

        return Task.FromResult(path);
    }

    public async Task<string> ExportJsonAsync(ScanReport report, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, $"LadecoDiag_{report.ComputerName}_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, report, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        return path;
    }

    public async Task<string> ExportCsvAsync(ScanReport report, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, $"LadecoDiag_{report.ComputerName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Code,Title,Severity,Priority,Cause,Resolution,AutoFix");
        foreach (var finding in report.AllFindings.OrderByDescending(x => x.Priority))
        {
            sb.AppendLine($"\"{finding.Code}\",\"{Escape(finding.Title)}\",\"{finding.Severity}\",{finding.Priority},\"{Escape(finding.ProbableCause)}\",\"{Escape(finding.Resolution)}\",\"{Escape(finding.AutoFixCommand ?? string.Empty)}\"");
        }

        await File.WriteAllTextAsync(path, sb.ToString(), cancellationToken);
        return path;
    }

    public async Task<string> ExportHtmlAsync(ScanReport report, string customerName, string technicianName, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, $"LadecoDiag_{report.ComputerName}_{DateTime.Now:yyyyMMdd_HHmmss}.html");

        var rows = string.Join(Environment.NewLine, report.AllFindings.OrderByDescending(x => x.Priority).Select(x =>
            $"<tr><td>{x.Code}</td><td>{Escape(x.Title)}</td><td>{x.Severity}</td><td>{x.Priority}</td><td>{Escape(x.ProbableCause)}</td><td>{Escape(x.Resolution)}</td></tr>"));

        var html = $$"""
<!doctype html>
<html lang="nl">
<head>
  <meta charset="utf-8" />
  <title>Ladeco IT Rapport</title>
  <style>
    body { font-family: 'Segoe UI', sans-serif; margin: 24px; color: #1f2937; }
    h1 { color: #0066FF; }
    table { width: 100%; border-collapse: collapse; margin-top: 16px; }
    th, td { border: 1px solid #d1d5db; padding: 8px; text-align: left; }
    th { background: #f9fafb; }
  </style>
</head>
<body>
  <h1>Ladeco IT Diagnostisch Rapport</h1>
  <p><strong>Klant:</strong> {Escape(customerName)} | <strong>Technieker:</strong> {Escape(technicianName)} | <strong>Datum:</strong> {report.ScannedAt:dd/MM/yyyy HH:mm}</p>
  <p><strong>Computer:</strong> {Escape(report.ComputerName)} | <strong>Gebruiker:</strong> {Escape(report.UserName)}</p>
  <table>
    <thead>
      <tr><th>Code</th><th>Titel</th><th>Severity</th><th>Prioriteit</th><th>Oorzaak</th><th>Oplossing</th></tr>
    </thead>
    <tbody>
      {rows}
    </tbody>
  </table>
</body>
</html>
""";

        await File.WriteAllTextAsync(path, html, cancellationToken);
        return path;
    }

    private static string Escape(string input) => input.Replace("\"", "\"\"");
}
