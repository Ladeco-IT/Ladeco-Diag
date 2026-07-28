namespace Ladeco.Diag.App.Models;

public sealed class StorageOptions
{
    public string SqliteConnectionString { get; init; } = "Data Source=ladeco-diag.db";
    public string ReportOutputDirectory { get; init; } = "reports";
}
