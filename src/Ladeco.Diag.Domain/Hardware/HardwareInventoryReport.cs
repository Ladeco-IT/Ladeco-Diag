namespace Ladeco.Diag.Domain.Hardware;

public sealed record HardwareInventoryReport(
    string Motherboard,
    string BiosVersion,
    string Cpu,
    string Gpu,
    string TotalRam,
    string StorageDevices,
    string Monitors,
    string Resolution,
    string NetworkAdapters,
    string BluetoothAdapters,
    string AudioDevices,
    string UsbDevices,
    string Printers,
    string CameraDevices,
    string Microphones,
    string Touchscreen,
    string DockingStations,
    string SerialNumbers
);
