using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Management;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class PerformanceAndReliabilityModule : IDiagnosticModule
{
    private const int RecentEventWindowMilliseconds = 86_400_000;
    private readonly ISystemSnapshotProvider _snapshotProvider;

    public PerformanceAndReliabilityModule(ISystemSnapshotProvider snapshotProvider)
    {
        _snapshotProvider = snapshotProvider;
    }

    public string Name => "PerformanceAndReliability";

    public async Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();
        var measurements = new Dictionary<string, string>();
        var snapshot = await _snapshotProvider.GetSnapshotAsync(cancellationToken);

        measurements["CpuUsagePercent"] = snapshot.CpuUsagePercent.ToString("F2");
        measurements["RamUsagePercent"] = snapshot.RamUsagePercent.ToString("F2");

        AddResourcePressureFindings(snapshot, findings);
        ReadStartupItemFinding(findings, measurements);
        ReadRecentSystemFailures(findings, measurements);

        return new ModuleResult(Name, started, DateTimeOffset.UtcNow, findings, measurements);
    }

    private static void AddResourcePressureFindings(
        Domain.Hardware.RuntimeSnapshot snapshot,
        ICollection<Finding> findings)
    {
        if (snapshot.CpuUsagePercent >= 90)
        {
            findings.Add(new Finding(
                "PERFORMANCE_CPU_SATURATED",
                "CPU-belasting vertraagt de computer",
                $"De totale CPU-belasting is {snapshot.CpuUsagePercent:F0}% tijdens de scan.",
                "Een actief proces, achtergrondtaak of Windows-service gebruikt langdurig te veel processortijd.",
                "Controleer Taakbeheer op processen met hoog CPU-gebruik en werk of herstart de betrokken toepassing.",
                DiagnosticSeverity.High,
                85,
                false,
                null,
                15,
                "Gemiddeld"));
        }

        if (snapshot.RamUsagePercent >= 85)
        {
            var largestProcesses = GetLargestProcesses();
            findings.Add(new Finding(
                "PERFORMANCE_MEMORY_PRESSURE",
                "Hoog geheugenverbruik kan vertraging veroorzaken",
                $"Het RAM-gebruik is {snapshot.RamUsagePercent:F0}% tijdens de scan.",
                largestProcesses.Count == 0
                    ? "Beschikbaar geheugen is laag, waardoor Windows vaker naar de schijf moet wisselen."
                    : $"Beschikbaar geheugen is laag. Grootste processen: {string.Join(", ", largestProcesses)}.",
                "Sluit ongebruikte zware toepassingen, controleer browser-tabbladen en overweeg extra RAM bij terugkerende belasting.",
                DiagnosticSeverity.Medium,
                75,
                false,
                null,
                15,
                "Makkelijk"));
        }
    }

    private static IReadOnlyList<string> GetLargestProcesses()
    {
        try
        {
            return Process.GetProcesses()
                .OrderByDescending(process => TryGetWorkingSet(process))
                .Take(3)
                .Select(process => $"{process.ProcessName} ({TryGetWorkingSet(process) / 1024 / 1024} MB)")
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static long TryGetWorkingSet(Process process)
    {
        try
        {
            return process.WorkingSet64;
        }
        catch
        {
            return 0;
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void ReadStartupItemFinding(ICollection<Finding> findings, IDictionary<string, string> measurements)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_StartupCommand");
            var startupItems = searcher.Get().Cast<ManagementObject>().ToList();
            measurements["StartupItemCount"] = startupItems.Count.ToString();

            if (startupItems.Count >= 15)
            {
                var examples = startupItems
                    .Take(5)
                    .Select(item => item["Name"]?.ToString())
                    .Where(name => !string.IsNullOrWhiteSpace(name));

                findings.Add(new Finding(
                    "PERFORMANCE_STARTUP_OVERLOAD",
                    "Veel programma's starten met Windows",
                    $"Er zijn {startupItems.Count} opstartitems gevonden.",
                    $"Veel opstartitems verlengen de aanmeldtijd. Voorbeelden: {string.Join(", ", examples)}.",
                    "Schakel niet-essentiele opstartprogramma's uit via Taakbeheer en behoud beveiligings- en hardwaredrivers.",
                    DiagnosticSeverity.Medium,
                    65,
                    false,
                    null,
                    20,
                    "Gemiddeld"));
            }
        }
        catch (Exception exception)
        {
            measurements["StartupItemScanError"] = exception.GetType().Name;
        }
    }

    private static void ReadRecentSystemFailures(ICollection<Finding> findings, IDictionary<string, string> measurements)
    {
        try
        {
            var query = new EventLogQuery(
                "System",
                PathType.LogName,
                $"*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) <= {RecentEventWindowMilliseconds}]]] ");
            using var reader = new EventLogReader(query);
            var failures = new List<(string Provider, int? EventId)>();

            while (failures.Count < 50 && reader.ReadEvent() is { } logEvent)
            {
                using (logEvent)
                {
                    failures.Add((logEvent.ProviderName ?? "Onbekend", logEvent.Id));
                }
            }

            measurements["RecentCriticalOrErrorEvents"] = failures.Count.ToString();
            if (failures.Count > 0)
            {
                var summaries = failures
                    .GroupBy(failure => $"{failure.Provider} ({failure.EventId?.ToString() ?? "onbekend"})")
                    .OrderByDescending(group => group.Count())
                    .Take(3)
                    .Select(group => $"{group.Key}: {group.Count()}x");

                findings.Add(new Finding(
                    "RELIABILITY_RECENT_SYSTEM_FAILURES",
                    "Recente Windows-systeemfouten gevonden",
                    $"Er zijn {failures.Count} kritieke of fout-events in het systeemlogboek van de laatste 24 uur gevonden.",
                    string.Join("; ", summaries),
                    "Open Event Viewer voor de genoemde bron en Event ID; update de bijbehorende driver of software wanneer de fout terugkomt.",
                    failures.Count >= 10 ? DiagnosticSeverity.High : DiagnosticSeverity.Medium,
                    failures.Count >= 10 ? 80 : 60,
                    false,
                    null,
                    30,
                    "Gemiddeld"));
            }
        }
        catch (Exception exception)
        {
            measurements["SystemEventLogScanError"] = exception.GetType().Name;
        }
    }
}