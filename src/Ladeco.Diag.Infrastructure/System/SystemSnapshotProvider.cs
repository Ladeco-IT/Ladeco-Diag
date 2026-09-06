using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Domain.Hardware;
using Microsoft.Win32;

namespace Ladeco.Diag.Infrastructure.System;

public sealed class SystemSnapshotProvider : ISystemSnapshotProvider
{
    public async Task<RuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var os = ReadOperatingSystemInfo();
        var lastUpdate = ReadLastUpdateDate();

        var (ip, mac) = ReadPrimaryNetworkIdentity();
        var publicIp = await ReadPublicIpAsync(cancellationToken);

        var cpuUsage = await ReadCpuUsageAsync(cancellationToken);
        var ramUsage = ReadRamUsage();
        var storageUsage = ReadStorageUsage();

        var (netType, wifiSignal, wifiSsid, wifiChannel, wifiRadioType) = ReadWirelessInfo();

        var cpuName = ReadHardwareInfo("Win32_Processor", "Name").FirstOrDefault() ?? "Onbekende CPU";
        var gpuName = string.Join(" | ", ReadHardwareInfo("Win32_VideoController", "Name"));
        if (string.IsNullOrWhiteSpace(gpuName)) gpuName = "Onbekende GPU";

        var totalRamBytes = ReadHardwareInfo("Win32_ComputerSystem", "TotalPhysicalMemory").FirstOrDefault();
        var totalRamStr = "Onbekend RAM";
        if (ulong.TryParse(totalRamBytes, out var bytes))
        {
            totalRamStr = $"{Math.Round(bytes / 1024.0 / 1024.0 / 1024.0, 1)} GB";
        }

        return new RuntimeSnapshot(
            ComputerName: Environment.MachineName,
            UserName: Environment.UserName,
            WindowsVersion: os.Version,
            WindowsBuild: os.Build,
            WindowsEdition: os.Edition,
            LastUpdate: lastUpdate,
            IpAddress: ip,
            PublicIpAddress: publicIp,
            MacAddress: mac,
            Uptime: ReadUptime(),
            CpuUsagePercent: cpuUsage,
            RamUsagePercent: ramUsage,
            StorageUsagePercent: storageUsage,
            CpuTemperatureCelsius: ReadCpuTemperature(),
            InternetAvailable: NetworkInterface.GetIsNetworkAvailable(),
            DefenderStatus: ReadDefenderStatus(),
            BitLockerStatus: ReadBitLockerStatus(),
            BatteryStatus: ReadBatteryStatus(),
            NetworkType: netType,
            WifiSignalStrength: wifiSignal,
            NetworkSpeed: ReadNetworkSpeed(),
            CpuName: cpuName,
            GpuName: gpuName,
            TotalRam: totalRamStr,
            LatencyMs: ReadLatency(),
            WifiSsid: wifiSsid,
            WifiChannel: wifiChannel,
            WifiRadioType: wifiRadioType,
            DnsServers: ReadDnsServers());
    }

    private static long ReadLatency()
    {
        try
        {
            using var pinger = new Ping();
            var reply = pinger.Send("8.8.8.8", 1500);
            if (reply.Status == IPStatus.Success)
            {
                return reply.RoundtripTime;
            }
        }
        catch { }
        return -1;
    }

    private static IEnumerable<string> ReadHardwareInfo(string wmiClass, string property)
    {
        var results = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
            foreach (var item in searcher.Get().Cast<ManagementObject>())
            {
                var val = item[property]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) results.Add(val.Trim());
            }
        }
        catch { }
        return results;
    }

    private static (string Type, string Signal, string Ssid, string Channel, string RadioType) ReadWirelessInfo()
    {
        try
        {
            var isWifi = NetworkInterface.GetAllNetworkInterfaces()
                .Any(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
                
            if (!isWifi)
            {
                return ("Ethernet (Bekabeld)", "N/A", "N/A", "N/A", "N/A");
            }
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var lines = output.Split('\n');
            var signalLine = lines.FirstOrDefault(l => l.Contains("Signal") || l.Contains("Signaal"));
            var ssidLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("SSID", StringComparison.OrdinalIgnoreCase) && !l.Contains("BSSID", StringComparison.OrdinalIgnoreCase));
            var channelLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("Channel", StringComparison.OrdinalIgnoreCase) || l.TrimStart().StartsWith("Kanaal", StringComparison.OrdinalIgnoreCase));
            var radioLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("Radio type", StringComparison.OrdinalIgnoreCase) || l.TrimStart().StartsWith("Radiotype", StringComparison.OrdinalIgnoreCase));
            var ssid = ReadNetshValue(ssidLine);
            var channel = ReadNetshValue(channelLine);
            var radioType = ReadNetshValue(radioLine);
            if (signalLine != null)
            {
                var signalStr = ReadNetshValue(signalLine).Replace("%", "");
                if (int.TryParse(signalStr, out int signalPercent))
                {
                    var quality = signalPercent > 80 ? "Uitstekend" : signalPercent > 60 ? "Goed" : signalPercent > 40 ? "Matig" : "Zwak";
                    return ("WiFi (Draadloos)", $"{signalPercent}% ({quality})", ssid, channel, radioType);
                }
            }
        }
        catch
        {
            // Ignored, fallback below
        }

        return ("WiFi (Draadloos)", "Onbekend", "Onbekend", "Onbekend", "Onbekend");
    }

    private static string ReadNetshValue(string? line)
    {
        var separator = line?.IndexOf(':') ?? -1;
        return separator >= 0 ? line![(separator + 1)..].Trim() : "Onbekend";
    }

    private static string ReadDnsServers()
    {
        try
        {
            var servers = NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.OperationalStatus == OperationalStatus.Up && network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(network => network.GetIPProperties().DnsAddresses)
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return servers.Count == 0 ? "N/A" : string.Join(", ", servers);
        }
        catch
        {
            return "N/A";
        }
    }

    private static string ReadNetworkSpeed()
    {
        try
        {
            var activeNet = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            
            if (activeNet != null)
            {
                var mbps = activeNet.Speed / 1_000_000;
                return mbps > 0 ? $"{mbps} Mbps (Lokale link)" : "N/A";
            }
        }
        catch { }
        return "N/A";
    }

    private static (string Version, string Build, string Edition) ReadOperatingSystemInfo()
    {
        const string keyPath = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion";
        using var key = Registry.LocalMachine.OpenSubKey(keyPath);

        var version = key?.GetValue("DisplayVersion")?.ToString() ?? "Unknown";
        var build = key?.GetValue("CurrentBuildNumber")?.ToString() ?? "Unknown";
        var edition = key?.GetValue("EditionID")?.ToString() ?? "Unknown";

        return (version, build, edition);
    }

    private static DateTimeOffset ReadLastUpdateDate()
    {
        var qfeSearcher = new ManagementObjectSearcher("SELECT InstalledOn FROM Win32_QuickFixEngineering");
        var newest = DateTime.MinValue;

        foreach (ManagementObject item in qfeSearcher.Get())
        {
            var dateText = item["InstalledOn"]?.ToString();
            if (DateTime.TryParse(dateText, out var parsed) && parsed > newest)
            {
                newest = parsed;
            }
        }

        return newest == DateTime.MinValue ? DateTimeOffset.MinValue : newest;
    }

    private static (string Ip, string Mac) ReadPrimaryNetworkIdentity()
    {
        var network = NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up &&
                        x.NetworkInterfaceType is not NetworkInterfaceType.Loopback)
            .Select(x => new
            {
                Interface = x,
                Address = x.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    ?.Address
                    .ToString()
            })
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Address));

        return network is null
            ? ("N/A", "N/A")
            : (network.Address!, network.Interface.GetPhysicalAddress().ToString());
    }

    private static async Task<string> ReadPublicIpAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            var json = await client.GetStringAsync("https://api.ipify.org?format=json", cancellationToken);
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("ip").GetString() ?? "Unknown";
        }
        catch
        {
            return "Unavailable";
        }
    }

    private static async Task<double> ReadCpuUsageAsync(CancellationToken cancellationToken)
    {
        using var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _ = counter.NextValue();
        await Task.Delay(800, cancellationToken);
        return Math.Round(counter.NextValue(), 2);
    }

    private static double ReadRamUsage()
    {
        var total = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem")
            .Get()
            .Cast<ManagementObject>()
            .FirstOrDefault();

        if (total is null)
        {
            return 0;
        }

        var totalKb = Convert.ToDouble(total["TotalVisibleMemorySize"]);
        var freeKb = Convert.ToDouble(total["FreePhysicalMemory"]);
        var used = (totalKb - freeKb) / totalKb * 100;
        return Math.Round(used, 2);
    }

    private static double ReadStorageUsage()
    {
        var drive = DriveInfo.GetDrives()
            .FirstOrDefault(d => d.IsReady && d.Name.Equals(Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase));

        if (drive is null)
        {
            return 0;
        }

        var used = ((double)drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize * 100;
        return Math.Round(used, 2);
    }

    private static double? ReadCpuTemperature()
    {
        try
        {
            var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (ManagementObject item in searcher.Get())
            {
                var temperatureTenthsKelvin = Convert.ToDouble(item["CurrentTemperature"]);
                var celsius = (temperatureTenthsKelvin / 10) - 273.15;
                return Math.Round(celsius, 1);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string ReadDefenderStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Defender", "SELECT RealTimeProtectionEnabled FROM MSFT_MpComputerStatus");
            var item = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (item is null)
            {
                return "Unknown";
            }

            return Convert.ToBoolean(item["RealTimeProtectionEnabled"]) ? "Enabled" : "Disabled";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string ReadBitLockerStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftVolumeEncryption", "SELECT ProtectionStatus FROM Win32_EncryptableVolume");
            var statuses = searcher.Get().Cast<ManagementObject>().Select(x => Convert.ToInt32(x["ProtectionStatus"])).ToList();
            if (statuses.Count == 0)
            {
                return "Not Supported";
            }

            return statuses.All(x => x == 1) ? "Protected" : "Partially Protected";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string ReadBatteryStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT BatteryStatus,EstimatedChargeRemaining FROM Win32_Battery");
            var battery = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (battery is null)
            {
                return "Desktop";
            }

            var remaining = battery["EstimatedChargeRemaining"]?.ToString() ?? "?";
            return $"{remaining}%";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string ReadUptime()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
    }
}
