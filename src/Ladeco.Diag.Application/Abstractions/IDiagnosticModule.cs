using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Application.Abstractions;

public interface IDiagnosticModule
{
    string Name { get; }
    Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default);
}
