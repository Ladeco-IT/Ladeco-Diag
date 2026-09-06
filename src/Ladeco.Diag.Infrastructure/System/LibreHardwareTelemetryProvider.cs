using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Hardware;
using LibreHardwareMonitor.Hardware;

namespace Ladeco.Diag.Infrastructure.System;

public sealed class LibreHardwareTelemetryProvider : IHardwareTelemetryProvider, IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsMotherboardEnabled = true,
        IsStorageEnabled = true
    };
    private readonly object _syncRoot = new();
    private bool _isOpen;

    public Task<HardwareTelemetry> GetTelemetryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            try
            {
                if (!_isOpen)
                {
                    _computer.Open();
                    _isOpen = true;
                }

                var hardware = GetHardware(_computer.Hardware).ToList();
                hardware.ForEach(item => item.Update());

                return Task.FromResult(new HardwareTelemetry(
                    ReadCpuTemperatures(hardware),
                    ReadSensor(hardware, "Cpu", SensorType.Power, "W"),
                    ReadSensor(hardware, "Gpu", SensorType.Temperature, "C"),
                    ReadSensor(hardware, "Gpu", SensorType.Power, "W"),
                    ReadFanSpeeds(hardware),
                    ReadSensor(hardware, "Storage", SensorType.Temperature, "C")));
            }
            catch
            {
                return Task.FromResult(NotAvailable());
            }
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isOpen)
            {
                _computer.Close();
                _isOpen = false;
            }
        }
    }

    private static IEnumerable<IHardware> GetHardware(IEnumerable<IHardware> hardware)
    {
        foreach (var item in hardware)
        {
            yield return item;
            foreach (var subHardware in GetHardware(item.SubHardware))
            {
                yield return subHardware;
            }
        }
    }

    private static string ReadSensor(IEnumerable<IHardware> hardware, string hardwareTypePrefix, SensorType sensorType, string unit)
    {
        var sensor = hardware
            .Where(item => item.HardwareType.ToString().StartsWith(hardwareTypePrefix, StringComparison.OrdinalIgnoreCase))
            .SelectMany(item => item.Sensors)
            .FirstOrDefault(item => item.SensorType == sensorType && item.Value.HasValue);

        return sensor?.Value is float value ? $"{value:F1} {unit}" : "Niet beschikbaar";
    }

    private static string ReadCpuTemperatures(IEnumerable<IHardware> hardware)
    {
        var temperatures = hardware
            .Where(item => item.HardwareType == HardwareType.Cpu)
            .SelectMany(item => item.Sensors)
            .Where(item => item.SensorType == SensorType.Temperature && item.Value.HasValue)
            .OrderBy(item => GetTemperaturePriority(item.Name))
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select(item => $"{item.Name}: {item.Value:F1} C")
            .ToList();

        return temperatures.Count == 0 ? "Niet beschikbaar" : string.Join(" | ", temperatures);
    }

    private static int GetTemperaturePriority(string name)
    {
        if (name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Tdie", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return name.Contains("Core", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }

    private static string ReadFanSpeeds(IEnumerable<IHardware> hardware)
    {
        var speeds = hardware
            .SelectMany(item => item.Sensors)
            .Where(item => item.SensorType == SensorType.Fan && item.Value.HasValue)
            .Take(3)
            .Select(item => $"{item.Name}: {item.Value:F0} RPM")
            .ToList();

        return speeds.Count == 0 ? "Niet beschikbaar" : string.Join(" | ", speeds);
    }

    private static HardwareTelemetry NotAvailable() => new(
        "Niet beschikbaar",
        "Niet beschikbaar",
        "Niet beschikbaar",
        "Niet beschikbaar",
        "Niet beschikbaar",
        "Niet beschikbaar");
}