namespace Ladeco.Diag.Domain.Hardware;

public sealed record RuntimeSnapshot(
    string ComputerName,
    string UserName,
    string WindowsVersion,
    string WindowsBuild,
    string WindowsEdition,
    DateTimeOffset LastUpdate,
    string IpAddress,
    string PublicIpAddress,
    string MacAddress,
    string Uptime,
    double CpuUsagePercent,
    double RamUsagePercent,
    double StorageUsagePercent,
    double? CpuTemperatureCelsius,
    bool InternetAvailable,
    string DefenderStatus,
    string BitLockerStatus,
    string BatteryStatus,
    string NetworkType,
    string WifiSignalStrength,
    string NetworkSpeed,
    string CpuName,
    string GpuName,
    string TotalRam,
    long LatencyMs
);
