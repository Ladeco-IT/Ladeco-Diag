using System.Collections.ObjectModel;
using Ladeco.Diag.Application.Abstractions;

namespace Ladeco.Diag.Application.Localization;

public sealed class InMemoryLocalizationService : ILocalizationService
{
    private readonly IReadOnlyDictionary<string, string> _translations;

    public InMemoryLocalizationService(IDictionary<string, string> translations)
    {
        _translations = new ReadOnlyDictionary<string, string>(translations);
    }

    public string this[string key] => _translations.TryGetValue(key, out var value) ? value : key;

    public static InMemoryLocalizationService DutchDefaults() => new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard.RunScan"] = "Volledige scan starten",
            ["Dashboard.GenerateReport"] = "PDF rapport genereren",
            ["Dashboard.Status.Ready"] = "Klaar",
            ["Dashboard.Status.Scanning"] = "Scan bezig",
            ["Dashboard.Status.Completed"] = "Scan voltooid",
            ["Dashboard.Card.System"] = "Systeem",
            ["Dashboard.Card.Security"] = "Beveiliging",
            ["Dashboard.Card.Network"] = "Netwerk",
            ["Dashboard.Card.Resources"] = "Resources",
            ["Action.ConfirmTitle"] = "Bevestiging",
            ["Dialog.Yes"] = "Ja",
            ["Dialog.No"] = "Nee",
            ["Action.ScanFirst"] = "Voer eerst een scan uit.",
            ["Action.Success"] = "Actie geslaagd",
            ["Action.Failed"] = "Actie mislukt",
            ["Action.FlushDns"] = "Flush DNS uitvoeren?",
            ["Action.RenewIp"] = "IP-adres vernieuwen?",
            ["Action.DnsReset"] = "DNS-instellingen en cache resetten?",
            ["Action.Dism"] = "DISM herstel uitvoeren? Dit kan langer duren.",
            ["Action.Sfc"] = "SFC scan uitvoeren?",
            ["Action.Chkdsk"] = "CHKDSK scan uitvoeren?",
            ["Action.ResetWU"] = "Windows Update componenten resetten?",
            ["Action.Spooler"] = "Print spooler herstarten?",
            ["Action.Winsock"] = "Winsock reset uitvoeren?",
            ["Action.NetworkReset"] = "Volledige netwerkreset uitvoeren?",
            ["Action.RestartExplorer"] = "Windows Explorer herstarten?",
            ["Action.CleanTemp"] = "Tijdelijke bestanden verwijderen?",
            ["Action.CleanCache"] = "Windows cache opruimen?",
            ["Action.CpuStressTest"] = "CPU-test starten? Deze begrensde Windows-beoordeling belast de processor tijdelijk.",
            ["Action.GpuStressTest"] = "GPU-test starten? Deze begrensde Windows-beoordeling belast de grafische kaart tijdelijk.",
            ["Action.MemoryTest"] = "Geheugentest starten? Deze begrensde Windows-beoordeling meet de geheugenprestatie.",
            ["Action.DiskReadTest"] = "Schijfleestest starten? Deze test leest de systeemschijf en belast deze tijdelijk.",
            ["Action.Reboot"] = "Systeem opnieuw opstarten in 15 seconden?",
            ["History.None"] = "Nog geen scans opgeslagen.",
            ["AI.NoScan"] = "AI-assistent: voer een scan uit voor advies.",
            ["AI.NoCritical"] = "AI-assistent: geen kritieke problemen gedetecteerd."
        });
}
