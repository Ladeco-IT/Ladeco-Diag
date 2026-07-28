using System.Net.NetworkInformation;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class InternetDiagnosticsModule : IDiagnosticModule
{
    public string Name => "Internet";

    public async Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();
        var measurements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var google = await PingAsync("8.8.8.8");
        var cloudflare = await PingAsync("1.1.1.1");

        measurements["PingGoogleMs"] = google.RoundtripTime.ToString();
        measurements["PingCloudflareMs"] = cloudflare.RoundtripTime.ToString();

        if (google.Status != IPStatus.Success && cloudflare.Status != IPStatus.Success)
        {
            findings.Add(new Finding(
                Code: "NO_INTERNET",
                Title: "Geen internetverbinding",
                Description: "Ping naar publieke DNS-hosts mislukt.",
                ProbableCause: "Gateway, DNS of providerstoring.",
                Resolution: "Voer netwerkreset uit en controleer gateway/DNS.",
                Severity: DiagnosticSeverity.Critical,
                Priority: 100,
                AutoFixAvailable: true,
                AutoFixCommand: "netsh int ip reset && netsh winsock reset",
                EstimatedMinutes: 10,
                Difficulty: "Gemiddeld"));
        }

        if (google.Status == IPStatus.Success && google.RoundtripTime > 120)
        {
            findings.Add(new Finding(
                Code: "HIGH_LATENCY",
                Title: "Hoge netwerklatency",
                Description: "Ping naar Google hoger dan 120ms.",
                ProbableCause: "WiFi-interferentie of WAN-congestie.",
                Resolution: "Test bekabeld en optimaliseer WiFi-kanaal.",
                Severity: DiagnosticSeverity.Low,
                Priority: 40,
                AutoFixAvailable: false,
                AutoFixCommand: null,
                EstimatedMinutes: 20,
                Difficulty: "Gemiddeld"));
        }

        return new ModuleResult(Name, started, DateTimeOffset.UtcNow, findings, measurements);
    }

    private static async Task<(IPStatus Status, long RoundtripTime)> PingAsync(string host)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 2000);
            return (reply.Status, reply.RoundtripTime);
        }
        catch
        {
            return (IPStatus.Unknown, -1);
        }
    }
}
