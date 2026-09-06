using Ladeco.Diag.Domain.Hardware;

namespace Ladeco.Diag.Application.Abstractions;

public interface IHardwareTelemetryProvider
{
    Task<HardwareTelemetry> GetTelemetryAsync(CancellationToken cancellationToken = default);
}