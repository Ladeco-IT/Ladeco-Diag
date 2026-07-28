using Ladeco.Diag.Domain.Hardware;

namespace Ladeco.Diag.Application.Abstractions;

public interface IHardwareInventoryProvider
{
    Task<HardwareInventoryReport> GetInventoryAsync(CancellationToken cancellationToken = default);
}
