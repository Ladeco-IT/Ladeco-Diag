using Microsoft.Win32;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class SoftwareInventoryModule : IDiagnosticModule
{
    public string Name => "SoftwareInventory";

    public Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var installed = ReadInstalledSoftware();
        var findings = new List<Finding>();

        var duplicates = installed
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .Take(10)
            .ToList();

        if (duplicates.Count > 0)
        {
            findings.Add(new Finding(
                "SOFTWARE_DUPLICATE",
                "Dubbele software-installaties",
                "Er zijn meerdere installaties met dezelfde productnaam gedetecteerd.",
                string.Join(", ", duplicates),
                "Verwijder dubbele of verouderde versies om conflicten te vermijden.",
                DiagnosticSeverity.Low,
                45,
                false,
                null,
                20,
                "Makkelijk"));
        }

        var oldCandidates = installed
            .Where(x => x.InstallDate is not null && x.InstallDate < DateTime.UtcNow.AddYears(-5))
            .OrderBy(x => x.InstallDate)
            .Take(8)
            .ToList();

        if (oldCandidates.Count > 0)
        {
            findings.Add(new Finding(
                "SOFTWARE_OLD",
                "Mogelijk verouderde software",
                "Er is software gevonden met zeer oude installatiedatum.",
                string.Join(", ", oldCandidates.Select(x => x.Name)),
                "Controleer op recente versies of verwijder legacy software.",
                DiagnosticSeverity.Medium,
                55,
                false,
                null,
                30,
                "Gemiddeld"));
        }

        var measurements = new Dictionary<string, string>
        {
            ["InstalledSoftwareCount"] = installed.Count.ToString(),
            ["TopSoftware"] = string.Join("; ", installed.Take(15).Select(x => $"{x.Name} {x.Version}"))
        };

        return Task.FromResult(new ModuleResult(Name, started, DateTimeOffset.UtcNow, findings, measurements));
    }

    private static List<SoftwareItem> ReadInstalledSoftware()
    {
        var list = new List<SoftwareItem>();
        var hives = new[]
        {
            "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
            "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"
        };

        foreach (var hive in hives)
        {
            using var root = Registry.LocalMachine.OpenSubKey(hive);
            if (root is null)
            {
                continue;
            }

            foreach (var sub in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(sub);
                var name = key?.GetValue("DisplayName")?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var version = key?.GetValue("DisplayVersion")?.ToString() ?? "?";
                var publisher = key?.GetValue("Publisher")?.ToString() ?? "Unknown";
                var installDate = ParseInstallDate(key?.GetValue("InstallDate")?.ToString());

                list.Add(new SoftwareItem(name, version, publisher, installDate));
            }
        }

        return list
            .DistinctBy(x => $"{x.Name}|{x.Version}|{x.Publisher}")
            .OrderBy(x => x.Name)
            .ToList();
    }

    private static DateTime? ParseInstallDate(string? dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText) || dateText.Length != 8)
        {
            return null;
        }

        if (DateTime.TryParseExact(dateText, "yyyyMMdd", null, global::System.Globalization.DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private sealed record SoftwareItem(string Name, string Version, string Publisher, DateTime? InstallDate);
}
