using System.Diagnostics;
using System.Text;

namespace PcManager;

public sealed record WingetApp(string Name, string PackageId);

public enum WingetInstallStatus
{
    Installed,
    AlreadyInstalled,
    Failed
}

public sealed record WingetInstallResult(string PackageId, WingetInstallStatus Status, int ExitCode, string Output);

public static class WingetInstaller
{
    public static readonly IReadOnlyList<WingetApp> KnownApps =
    [
        new WingetApp("Notepad++", "Notepad++.Notepad++"),
        new WingetApp("Google Chrome", "Google.Chrome")
    ];

    public static WingetInstallResult Install(WingetApp app, Action<string>? onOutputLine = null) =>
        Install(app.PackageId, onOutputLine);

    public static IReadOnlyList<WingetInstallResult> InstallAll(
        Action<WingetApp, WingetInstallResult, int, int>? onProgress = null,
        Action<string>? onOutputLine = null)
    {
        var apps = KnownApps;
        var results = new List<WingetInstallResult>(apps.Count);

        for (var i = 0; i < apps.Count; i++)
        {
            var app = apps[i];
            var result = Install(app.PackageId, onOutputLine);
            results.Add(result);
            onProgress?.Invoke(app, result, i + 1, apps.Count);
        }

        return results;
    }

    public static WingetInstallResult Install(string packageId, Action<string>? onOutputLine = null)
    {
        // Pre-check with "winget list" before calling "winget install".
        // winget install is ALSO idempotent on its own (it checks the same Add/Remove Programs
        // registry entries and just no-ops with exit code 0 if the package is already present),
        // so this isn't required for correctness - it just lets us report "AlreadyInstalled"
        // as its own status instead of lumping it in with "Installed", and skips re-invoking
        // the installer entirely when we already know the answer.
        if (IsInstalled(packageId))
        {
            return new WingetInstallResult(packageId, WingetInstallStatus.AlreadyInstalled, 0, string.Empty);
        }

        var startInfo = new ProcessStartInfo("winget")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("install");
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add("--id");
        startInfo.ArgumentList.Add(packageId);
        startInfo.ArgumentList.Add("--accept-source-agreements");
        startInfo.ArgumentList.Add("--accept-package-agreements");

        var outputBuilder = new StringBuilder();

        void OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            if (e.Data is null)
            {
                return;
            }

            outputBuilder.AppendLine(e.Data);
            onOutputLine?.Invoke(e.Data);
        }

        // Read output as it arrives (OutputDataReceived/ErrorDataReceived) instead of
        // ReadToEnd(), which blocks until the process' output stream closes - i.e. until
        // the whole download+install is finished. This lets callers see winget's status
        // lines live instead of getting everything dumped at once at the end. Note: winget
        // detects when stdout isn't a real console and falls back to plainer, less frequent
        // status lines rather than its normal animated percentage bar.
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += OnDataReceived;
        process.ErrorDataReceived += OnDataReceived;

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        var status = process.ExitCode == 0 ? WingetInstallStatus.Installed : WingetInstallStatus.Failed;
        return new WingetInstallResult(packageId, status, process.ExitCode, outputBuilder.ToString());
    }

    // "winget list --id <id> -e" exits 0 when a matching package is found, non-zero otherwise.
    // Detection is based on winget's own source data matched against the Add/Remove Programs
    // (ARP) registry entries - so this also catches packages installed via the vendor's own
    // installer/website, AS LONG AS that installer registered proper ARP entries (most do,
    // including Notepad++'s official installer). Portable/zip installs that skip ARP registration,
    // or installers with a mismatched display name/version, won't be detected this way.
    public static bool IsInstalled(string packageId)
    {
        var startInfo = new ProcessStartInfo("winget")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("list");
        startInfo.ArgumentList.Add("--id");
        startInfo.ArgumentList.Add(packageId);
        startInfo.ArgumentList.Add("-e");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the winget process.");

        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0;
    }
}
