using System.Management;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Hardware;

namespace Ladeco.Diag.Infrastructure.System;

public sealed class HardwareInventoryProvider : IHardwareInventoryProvider
{
    public Task<HardwareInventoryReport> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        var motherboard = ReadSingle("SELECT Manufacturer,Product FROM Win32_BaseBoard", x => $"{x["Manufacturer"]} {x["Product"]}");
        var bios = ReadSingle("SELECT SMBIOSBIOSVersion FROM Win32_BIOS", x => x["SMBIOSBIOSVersion"]?.ToString());
        var cpu = ReadSingle("SELECT Name FROM Win32_Processor", x => x["Name"]?.ToString());
        var gpu = ReadMany("SELECT Name FROM Win32_VideoController", x => x["Name"]?.ToString());
        var totalRam = ReadTotalRam();
        var storage = ReadMany("SELECT Model,MediaType,Size FROM Win32_DiskDrive", x =>
        {
            var model = x["Model"]?.ToString() ?? "Unknown";
            var mediaType = x["MediaType"]?.ToString() ?? "Unknown";
            var sizeGb = x["Size"] is null ? "?" : $"{Math.Round(Convert.ToDouble(x["Size"]) / 1024 / 1024 / 1024, 0)}GB";
            return $"{model} ({mediaType}, {sizeGb})";
        });

        var monitors = ReadMany("SELECT Name FROM Win32_DesktopMonitor", x => x["Name"]?.ToString());
        var resolution = ReadSingle("SELECT CurrentHorizontalResolution,CurrentVerticalResolution FROM Win32_VideoController", x => $"{x["CurrentHorizontalResolution"]}x{x["CurrentVerticalResolution"]}");
        var networkAdapters = ReadMany("SELECT Name FROM Win32_NetworkAdapter WHERE PhysicalAdapter = True", x => x["Name"]?.ToString());
        var bluetooth = ReadMany("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%Bluetooth%'", x => x["Name"]?.ToString());
        var audio = ReadMany("SELECT Name FROM Win32_SoundDevice", x => x["Name"]?.ToString());
        var usb = ReadMany("SELECT Name FROM Win32_PnPEntity WHERE PNPClass = 'USB'", x => x["Name"]?.ToString(), maxItems: 20);
        var printers = ReadMany("SELECT Name FROM Win32_Printer", x => x["Name"]?.ToString());
        var cameras = ReadMany("SELECT Name FROM Win32_PnPEntity WHERE PNPClass = 'Image'", x => x["Name"]?.ToString());
        var mics = ReadMany("SELECT Name FROM Win32_SoundDevice WHERE Name LIKE '%Microphone%'", x => x["Name"]?.ToString());
        var touchscreen = ReadMany("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%Touch%'", x => x["Name"]?.ToString());
        var docks = ReadMany("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%Dock%'", x => x["Name"]?.ToString());

        var serials = string.Join("; ",
            ReadSingle("SELECT SerialNumber FROM Win32_BIOS", x => $"BIOS: {x["SerialNumber"]}") ?? "BIOS: N/A",
            ReadSingle("SELECT SerialNumber FROM Win32_BaseBoard", x => $"Board: {x["SerialNumber"]}") ?? "Board: N/A");

        var report = new HardwareInventoryReport(
            motherboard ?? "Unknown",
            bios ?? "Unknown",
            cpu ?? "Unknown",
            gpu,
            totalRam,
            storage,
            monitors,
            resolution ?? "Unknown",
            networkAdapters,
            bluetooth,
            audio,
            usb,
            printers,
            cameras,
            mics,
            string.IsNullOrWhiteSpace(touchscreen) ? "No" : "Yes",
            docks,
            serials);

        return Task.FromResult(report);
    }

    private static string ReadMany(string query, Func<ManagementObject, string?> selector, int maxItems = 8)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            var items = searcher.Get().Cast<ManagementObject>()
                .Select(selector)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxItems)
                .ToList();

            return items.Count == 0 ? "None" : string.Join("; ", items!);
        }
        catch
        {
            return "Unavailable";
        }
    }

    private static string? ReadSingle(string query, Func<ManagementObject, string?> selector)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            var item = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            return item is null ? null : selector(item);
        }
        catch
        {
            return null;
        }
    }

    private static string ReadTotalRam()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            var bytes = searcher.Get().Cast<ManagementObject>().Sum(x => Convert.ToDouble(x["Capacity"]));
            return $"{Math.Round(bytes / 1024 / 1024 / 1024, 1)} GB";
        }
        catch
        {
            return "Unavailable";
        }
    }
}
