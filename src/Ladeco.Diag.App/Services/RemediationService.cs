using System.Diagnostics;

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
        ["ClearEventLogs"] = "powershell -NoProfile -Command \"Get-EventLog -LogName * | ForEach { Clear-EventLog $_.Log }\"",
        ["RebootSystem"] = "shutdown /r /t 15 /c \"Ladeco IT Diagnostic reboot\""
    };

    public async Task<(bool Success, string Output)> RunSafeActionAsync(string actionKey, CancellationToken cancellationToken = default)
    {
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
            var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
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
}
