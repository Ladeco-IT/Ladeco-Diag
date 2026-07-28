using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Application.Abstractions;

public interface IDiagnosticsOrchestrator
{
    Task<ScanReport> RunFullScanAsync(CancellationToken cancellationToken = default);
}
