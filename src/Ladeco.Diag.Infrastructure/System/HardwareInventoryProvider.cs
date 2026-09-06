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
        var gpuDetails = ReadMany("SELECT Name,DriverVersion,AdapterRAM,VideoProcessor FROM Win32_VideoController", x =>
        {
            var name = x["Name"]?.ToString() ?? "Unknown";
            var driver = x["DriverVersion"]?.ToString() ?? "Unknown driver";
            var vram = x["AdapterRAM"] is null ? "Unknown VRAM" : $"{Math.Round(Convert.ToDouble(x["AdapterRAM"]) / 1024 / 1024 / 1024, 1)} GB VRAM";
            var processor = x["VideoProcessor"]?.ToString() ?? "Unknown processor";
            return $"{name} | {vram} | Driver {driver} | {processor}";
        }, 4);
        var totalRam = ReadTotalRam();
        var memoryModules = ReadMany("SELECT Capacity,Speed,Manufacturer,PartNumber FROM Win32_PhysicalMemory", x =>
        {
            var capacity = x["Capacity"] is null ? "? GB" : $"{Math.Round(Convert.ToDouble(x["Capacity"]) / 1024 / 1024 / 1024, 0)} GB";
            var speed = x["Speed"]?.ToString() ?? "?";
            var manufacturer = x["Manufacturer"]?.ToString()?.Trim() ?? "Unknown";
            var partNumber = x["PartNumber"]?.ToString()?.Trim() ?? "";
            return $"{capacity} | {speed} MHz | {manufacturer} {partNumber}".Trim();
        }, 8, "\n");
        var systemDetails = ReadSingle("SELECT Manufacturer,Model,SystemType FROM Win32_ComputerSystem", x =>
            $"{x["Manufacturer"]} {x["Model"]} | {x["SystemType"]}") ?? "Unavailable";
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
            serials,
            systemDetails,
            memoryModules,
            gpuDetails,
            ReadStorageDetails());

        return Task.FromResult(report);
    }

    private static string ReadMany(string query, Func<ManagementObject, string?> selector, int maxItems = 8, string joiner = "; ")
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

            return items.Count == 0 ? "None" : string.Join(joiner, items!);
        }
        catch
        {
            return "Unavailable";
        }
    }

        private string ReadStorageDetails()
    {
        try
        {
            var results = new global::System.Collections.Generic.List<string>();
            using var searcherPhysical = new ManagementObjectSearcher("Root\\Microsoft\\Windows\\Storage", "SELECT MediaType, HealthStatus, Size, FriendlyName FROM MSFT_PhysicalDisk");
            var physicalDisks = searcherPhysical.Get().Cast<ManagementObject>().ToList();

            if (physicalDisks.Count > 0)
            {
                foreach (var disk in physicalDisks)
                {
                    var name = disk["FriendlyName"]?.ToString() ?? "Unknown Disk";
                    var mediaTypeVal = Convert.ToInt32(disk["MediaType"]);
                    var mediaType = mediaTypeVal == 4 ? "SSD" : (mediaTypeVal == 3 ? "HDD" : "Unknown Type");
                    
                    var healthStatusVal = Convert.ToInt32(disk["HealthStatus"]);
                    var health = healthStatusVal == 0 ? "Healthy" : (healthStatusVal == 1 ? "Warning" : "Unhealthy");
                    
                    var sizeGb = disk["Size"] is null ? "?" : $"{Math.Round(Convert.ToDouble(disk["Size"]) / 1024 / 1024 / 1024, 0)} GB";
                    
                    string wearInfo = "";
                    try {
                        var wear = disk["Wear"];
                        if (wear != null && Convert.ToDouble(wear) > 0) 
                            wearInfo = $", Health: {100 - Convert.ToDouble(wear)}%";
                    } catch { }

                    results.Add($"{name}\n   {mediaType} - {sizeGb} - Status: {health}{wearInfo}");
                }
                return string.Join("\n\n", results);
            }
        }
        catch { }

        // Fallback if MSFT_PhysicalDisk is unavailable (requires admin sometimes)
        return ReadMany("SELECT Model,MediaType,Size,Status FROM Win32_DiskDrive", x =>
        {
            var model = x["Model"]?.ToString() ?? "Unknown";
            var mediaType = x["MediaType"]?.ToString() ?? "Local Disk";
            var status = x["Status"]?.ToString() ?? "OK";
            var sizeGb = x["Size"] is null ? "?" : $"{Math.Round(Convert.ToDouble(x["Size"]) / 1024 / 1024 / 1024, 0)}GB";
            if (mediaType.Contains("Fixed")) mediaType = "SSD/HDD";
            return $"{model}\n   {mediaType} - {sizeGb} - Status: {status}";
        }, 10, "\n\n");
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


