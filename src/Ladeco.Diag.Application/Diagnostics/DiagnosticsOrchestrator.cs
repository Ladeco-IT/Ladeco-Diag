using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Application.Diagnostics;

public sealed class DiagnosticsOrchestrator : IDiagnosticsOrchestrator
{
    private readonly IReadOnlyList<IDiagnosticModule> _modules;

    public DiagnosticsOrchestrator(IEnumerable<IDiagnosticModule> modules)
    {
        _modules = modules.ToList();
    }

    public async Task<ScanReport> RunFullScanAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var moduleResults = new List<ModuleResult>(_modules.Count);

        foreach (var module in _modules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Execute modules on a background thread so synchronous work (like WMI) doesn't freeze the UI 
            moduleResults.Add(await Task.Run(() => module.ExecuteAsync(cancellationToken), cancellationToken));
        }

        return new ScanReport
        {
            Id = Guid.NewGuid(),
            ComputerName = Environment.MachineName,
            UserName = Environment.UserName,
            ScannedAt = started,
            ModuleResults = moduleResults
        };
    }
}
