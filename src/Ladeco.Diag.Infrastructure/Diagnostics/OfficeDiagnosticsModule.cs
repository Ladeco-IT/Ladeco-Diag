using Microsoft.Win32;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class OfficeDiagnosticsModule : IDiagnosticModule
{
    public string Name => "Office";

    public Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();
        var measurements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var officeVersion = ReadOfficeVersion();
        measurements["OfficeVersion"] = officeVersion ?? "Not Installed";

        if (officeVersion is null)
        {
            findings.Add(new Finding(
                "OFFICE_NOT_FOUND",
                "Microsoft Office niet gevonden",
                "Geen ondersteunde Office-installatie gedetecteerd.",
                "Office is niet geinstalleerd of klik-en-klaar pad ontbreekt.",
                "Installeer of herstel Microsoft 365 Apps.",
                DiagnosticSeverity.Low,
                25,
                false,
                null,
                20,
                "Makkelijk"));
        }

        return Task.FromResult(new ModuleResult(Name, started, DateTimeOffset.UtcNow, findings, measurements));
    }

    private static string? ReadOfficeVersion()
    {
        using var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Office\\ClickToRun\\Configuration");
        return key?.GetValue("VersionToReport")?.ToString();
    }
}
