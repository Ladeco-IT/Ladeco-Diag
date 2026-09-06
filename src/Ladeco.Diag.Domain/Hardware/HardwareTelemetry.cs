namespace Ladeco.Diag.Domain.Hardware;

public sealed record HardwareTelemetry(
    string CpuTemperature,
    string CpuPower,
    string GpuTemperature,
    string GpuPower,
    string FanSpeeds,
    string StorageTemperature
);