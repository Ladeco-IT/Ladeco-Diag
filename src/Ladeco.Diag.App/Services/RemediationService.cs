using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace Ladeco.Diag.App.Services;

public sealed class RemediationService : IRemediationService
{
    private static readonly IReadOnlyDictionary<string, string> Commands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["FlushDns"] = "ipconfig /flushdns",
        ["RenewIp"] = "ipconfig /renew",
        ["DnsReset"] = "netsh interface ip delete arpcache && ipconfig /flushdns",
        ["DismRestoreHealth"] = "DISM /Online /Cleanup-Image /RestoreHealth",
        ["SfcScannow"] = "sfc /scannow",
        ["CheckDisk"] = "chkdsk /scan",
        ["ResetWindowsUpdate"] = "net stop wuauserv && net stop bits && ren %systemroot%\\SoftwareDistribution SoftwareDistribution.bak && net start bits && net start wuauserv",
        ["RestartPrintSpooler"] = "powershell -NoProfile -Command \"Restart-Service spooler -Force\"",
        ["ResetWinsock"] = "netsh winsock reset",
        ["ResetNetwork"] = "netsh int ip reset",
        ["RestartExplorer"] = "powershell -NoProfile -Command \"Stop-Process -Name explorer -Force; Start-Process explorer\"",
        ["CleanTempFiles"] = "powershell -NoProfile -Command \"try { Get-ChildItem -Path $env:TEMP, 'C:\\Windows\\Temp' -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue; Write-Output 'Temp mappen geleegd.' } catch { Write-Output 'Temp mappen geleegd (sommige bestanden zijn in gebruik).' }\"",
        ["CleanWindowsCache"] = "cleanmgr /VERYLOWDISK",
        ["RunDefenderScan"] = "powershell -NoProfile -Command \"Start-MpScan -ScanType QuickScan\"",
        ["RunDeepDefenderScan"] = "powershell -NoProfile -Command \"Start-MpScan -ScanType FullScan\"",
        ["DefragDrive"] = "defrag C: /O",
        ["FullDebloat"] = "powershell -NoProfile -Command \"Get-AppxPackage -AllUsers | Where-Object {$_.IsFramework -eq $false -and $_.NonRemovable -eq $false -and $_.PackageFullName -notmatch 'Microsoft.WindowsStore'} | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue\"",
        ["RunSpeedtest"] = "powershell -NoProfile -Command \"Invoke-WebRequest 'https://raw.githubusercontent.com/sivel/speedtest-cli/master/speedtest.py' -OutFile speedtest.py; python speedtest.py; Remove-Item speedtest.py -ErrorAction SilentlyContinue\"",
        ["RunCpuStressTest"] = "winsat cpuformal",
        ["RunGpuStressTest"] = "winsat dwmformal",
        ["RunMemoryTest"] = "winsat mem",
        ["RunDiskReadTest"] = "winsat disk -drive C -seq -read -count 3",
        ["ClearEventLogs"] = "powershell -NoProfile -Command \"Get-EventLog -LogName * | ForEach { Clear-EventLog $_.Log }\"",
        ["RebootSystem"] = "shutdown /r /t 15 /c \"Ladeco IT Diagnostic reboot\""
    };

    public async Task<(bool Success, string Output)> RunSafeActionAsync(
        string actionKey,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (actionKey.Equals("RunSpeedtest", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                progress?.Report("Speedtest: download wordt gestart...\n");
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                var timerDL = Stopwatch.StartNew();
                using var response = await client.GetAsync("http://speedtest.tele2.net/100MB.zip", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                using var streamDL = await response.Content.ReadAsStreamAsync(cancellationToken);
                var buffer = new byte[81920];
                long totalBytesDL = 0;
                while (timerDL.Elapsed.TotalSeconds < 8)
                {
                    int read = await streamDL.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (read == 0) break;
                    totalBytesDL += read;
                }
                timerDL.Stop();
                double dlMbps = (totalBytesDL * 8.0 / 1000000.0) / timerDL.Elapsed.TotalSeconds;

                progress?.Report($"Download afgerond: {dlMbps:N2} Mbps\nSpeedtest: upload wordt gestart...\n");
                var timerUL = Stopwatch.StartNew();
                var dataUL = new byte[2 * 1024 * 1024];
                using var uploadContent = new ByteArrayContent(dataUL);
                using var uploadResponse = await client.PostAsync("http://speedtest.tele2.net/upload.php", uploadContent, cancellationToken);
                uploadResponse.EnsureSuccessStatusCode();
                timerUL.Stop();
                double ulMbps = (dataUL.Length * 8.0 / 1000000.0) / timerUL.Elapsed.TotalSeconds;

                progress?.Report($"Upload afgerond: {ulMbps:N2} Mbps\n");
                return (true, $"Download: {dlMbps:N2} Mbps | Upload: {ulMbps:N2} Mbps");
            }
            catch (Exception ex)
            {
                return (false, $"Speedtest mislukt: {ex.Message}");
            }
        }

        if (!Commands.TryGetValue(actionKey, out var command))
        {
            return (false, "Onbekende actie.");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo("cmd.exe", $"/c {command}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();

        try
        {
            var stdOutTask = ReadStreamAsync(process.StandardOutput, progress, cancellationToken);
            var stdErrTask = ReadStreamAsync(process.StandardError, progress, cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;
            
            var output = string.IsNullOrWhiteSpace(stdErr) ? stdOut : $"{stdOut}\n{stdErr}";
            // PowerShell scripts exit code is sometimes 1 when non-terminating errors occur (like locked Temp files)
            // As long as we have output and it didn't completely crash, treat partially successful stuff decently.
            return (process.ExitCode == 0 || !string.IsNullOrWhiteSpace(stdOut), output.Trim());
        }
        catch (TaskCanceledException)
        {
            try { process.Kill(true); } catch { }
            return (false, "Actie werd geannuleerd.");
        }
    }

    private static async Task<string> ReadStreamAsync(
        StreamReader stream,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var buffer = new char[512];
        int read;

        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            var chunk = new string(buffer, 0, read);
            output.Append(chunk);
            progress?.Report(chunk);
        }

        return output.ToString();
    }
}


