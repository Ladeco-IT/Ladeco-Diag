using Microsoft.Win32;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class BrowserDiagnosticsModule : IDiagnosticModule
{
    public string Name => "Browsers";

    public Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();

        var measurements = new Dictionary<string, string>
        {
            ["Chrome"] = ReadUninstallVersion("Google Chrome") ?? "Not Installed",
            ["Edge"] = ReadUninstallVersion("Microsoft Edge") ?? "Not Installed",
            ["Firefox"] = ReadUninstallVersion("Mozilla Firefox") ?? "Not Installed",
            ["Opera"] = ReadUninstallVersion("Opera") ?? "Not Installed"
        };

        var installedCount = measurements.Values.Count(x => !x.Equals("Not Installed", StringComparison.OrdinalIgnoreCase));
        if (installedCount == 0)
        {
            findings.Add(new Finding(
                "BROWSER_NONE",
                "Geen ondersteunde browser gevonden",
                "Er werd geen Chrome, Edge, Firefox of Opera gevonden.",
                "Browser niet geinstalleerd of inventarisatiekey ontbreekt.",
                "Installeer een ondersteunde browser voor supporttaken.",
                DiagnosticSeverity.Low,
                20,
                false,
                null,
                10,
                "Makkelijk"));
        }

        return Task.FromResult(new ModuleResult(Name, started, DateTimeOffset.UtcNow, findings, measurements));
    }

    private static string? ReadUninstallVersion(string displayNameContains)
    {
        var keys = new[]
        {
            "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
            "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"
        };

        foreach (var keyPath in keys)
        {
            using var root = Registry.LocalMachine.OpenSubKey(keyPath);
            if (root is null)
            {
                continue;
            }

            foreach (var sub in root.GetSubKeyNames())
            {
                using var app = root.OpenSubKey(sub);
                var name = app?.GetValue("DisplayName")?.ToString();
                if (string.IsNullOrWhiteSpace(name) || !name.Contains(displayNameContains, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return app?.GetValue("DisplayVersion")?.ToString() ?? "Unknown";
            }
        }

        return null;
    }
}
