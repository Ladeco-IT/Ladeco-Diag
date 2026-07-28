using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.App.Services;

public sealed class FindingExplanationService : IFindingExplanationService
{
    public string BuildExplanation(Finding finding)
    {
        return string.Join(Environment.NewLine,
        [
            $"Probleem: {finding.Title}",
            $"Wat betekent dit: {finding.Description}",
            $"Waarschijnlijke oorzaak: {finding.ProbableCause}",
            $"Risico: {MapRisk(finding.Severity)}",
            $"Aanpak: {finding.Resolution}",
            $"Geschatte tijd: {finding.EstimatedMinutes} minuten",
            $"Moeilijkheidsgraad: {finding.Difficulty}",
            finding.AutoFixAvailable && !string.IsNullOrWhiteSpace(finding.AutoFixCommand)
                ? $"Automatische actie beschikbaar: {finding.AutoFixCommand}"
                : "Automatische actie: niet beschikbaar"
        ]);
    }

    private static string MapRisk(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Critical => "Kritiek, onmiddellijke actie nodig.",
            DiagnosticSeverity.High => "Hoog, snelle interventie aanbevolen.",
            DiagnosticSeverity.Medium => "Gemiddeld, monitoren en plannen.",
            DiagnosticSeverity.Low => "Laag, optimalisatie bij gelegenheid.",
            _ => "Informatief."
        };
    }
}
