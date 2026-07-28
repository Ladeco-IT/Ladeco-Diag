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
        ["CleanTempFiles"] = "powershell -NoProfile -Command \"Get-ChildItem $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue\"",
        ["CleanWindowsCache"] = "cleanmgr /VERYLOWDISK",
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
        var stdOut = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = string.IsNullOrWhiteSpace(stdErr) ? stdOut : $"{stdOut}\n{stdErr}";
        return (process.ExitCode == 0, output.Trim());
    }
}
