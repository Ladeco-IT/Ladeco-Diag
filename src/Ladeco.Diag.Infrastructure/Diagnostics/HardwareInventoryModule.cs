using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Diagnostics;

namespace Ladeco.Diag.Infrastructure.Diagnostics;

public sealed class HardwareInventoryModule : IDiagnosticModule
{
    private readonly IHardwareInventoryProvider _hardwareProvider;

    public HardwareInventoryModule(IHardwareInventoryProvider hardwareProvider)
    {
        _hardwareProvider = hardwareProvider;
    }

    public string Name => "HardwareInventory";

    public async Task<ModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var inventory = await _hardwareProvider.GetInventoryAsync(cancellationToken);

        var findings = new List<Finding>();
        if (inventory.StorageDevices.Contains("Unavailable", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new Finding(
                "HW_STORAGE_ENUM_FAILED",
                "Opslaginformatie onvolledig",
                "Niet alle opslagapparaten konden worden uitgelezen.",
                "WMI providerfout of rechtenprobleem.",
                "Voer de scan uit als administrator en controleer WMI repository.",
                DiagnosticSeverity.Low,
                35,
                false,
                null,
                10,
                "Gemiddeld"));
        }

        if (inventory.Touchscreen.Equals("Yes", StringComparison.OrdinalIgnoreCase) &&
            inventory.Monitors.Contains("Generic", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new Finding(
                "HW_TOUCH_GENERIC_MONITOR",
                "Touch-apparaat met generiek displaydriver",
                "Touch-functionaliteit kan beperkt zijn door generieke monitor-driver.",
                "Specifieke OEM-driver ontbreekt.",
                "Installeer OEM display/touch drivers.",
                DiagnosticSeverity.Medium,
                55,
                false,
                null,
                20,
                "Gemiddeld"));
        }

        var measurements = new Dictionary<string, string>
        {
            ["Motherboard"] = inventory.Motherboard,
            ["BiosVersion"] = inventory.BiosVersion,
            ["Cpu"] = inventory.Cpu,
            ["Gpu"] = inventory.Gpu,
            ["TotalRam"] = inventory.TotalRam,
            ["StorageDevices"] = inventory.StorageDevices,
            ["Monitors"] = inventory.Monitors,
            ["Resolution"] = inventory.Resolution,
            ["NetworkAdapters"] = inventory.NetworkAdapters,
            ["BluetoothAdapters"] = inventory.BluetoothAdapters,
            ["AudioDevices"] = inventory.AudioDevices,
            ["UsbDevices"] = inventory.UsbDevices,
            ["Printers"] = inventory.Printers,
            ["Cameras"] = inventory.CameraDevices,
            ["Microphones"] = inventory.Microphones,
            ["Touchscreen"] = inventory.Touchscreen,
            ["DockingStations"] = inventory.DockingStations,
            ["SerialNumbers"] = inventory.SerialNumbers
        };

        return new ModuleResult(Name, started, DateTimeOffset.UtcNow, findings, measurements);
    }
}
