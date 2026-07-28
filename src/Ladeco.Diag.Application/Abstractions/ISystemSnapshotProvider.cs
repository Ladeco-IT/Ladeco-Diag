using Ladeco.Diag.Domain.Hardware;

namespace Ladeco.Diag.Application.Abstractions;

public interface ISystemSnapshotProvider
{
    Task<RuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
