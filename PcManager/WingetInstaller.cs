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

    // "winget" on PATH is an App Execution Alias stub (%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe)
    // that forwards to the real MSIX-packaged exe. That forwarding works fine run directly/interactively
    // (e.g. typing "winget --version" in a terminal) but is documented to hang indefinitely when its
    // stdout/stderr are redirected by a parent process - which is exactly what IsInstalled() and
    // Install() (by default) need to do to capture output. Resolving the real packaged winget.exe and
    // calling that directly avoids the alias stub, and redirection against it works normally.
    // Resolved once and cached; falls back to the "winget" alias if resolution fails for any reason.
    private static readonly Lazy<string> WingetExecutablePath = new(ResolveWingetExecutablePath);

    private static string ResolveWingetExecutablePath()
    {
        try
        {
            var startInfo = new ProcessStartInfo("powershell")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add("(Get-AppxPackage -Name Microsoft.DesktopAppInstaller).InstallLocation");

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start powershell.");

            var installLocation = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(installLocation))
            {
                var candidate = Path.Combine(installLocation, "winget.exe");

                if (File.Exists(candidate))
                {
                    Console.WriteLine($"[DEBUG] Resolved real winget.exe: {candidate}");
                    return candidate;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Failed to resolve real winget.exe, falling back to alias: {ex.Message}");
        }

        return "winget";
    }

    public static WingetInstallResult Install(WingetApp app, bool liveConsoleOutput = false, Action<string>? onOutputLine = null) =>
        Install(app.PackageId, liveConsoleOutput, onOutputLine);

    public static IReadOnlyList<WingetInstallResult> InstallAll(
        Action<WingetApp, WingetInstallResult, int, int>? onProgress = null,
        bool liveConsoleOutput = false,
        Action<string>? onOutputLine = null)
    {
        var apps = KnownApps;
        var results = new List<WingetInstallResult>(apps.Count);

        Console.WriteLine($"[DEBUG] InstallAll: {apps.Count} app(s) queued");

        for (var i = 0; i < apps.Count; i++)
        {
            var app = apps[i];
            Console.WriteLine($"[DEBUG] InstallAll: starting {app.Name} ({app.PackageId}) - {i + 1}/{apps.Count}");
            var result = Install(app.PackageId, liveConsoleOutput, onOutputLine);
            Console.WriteLine($"[DEBUG] InstallAll: finished {app.Name} -> {result.Status} (exit {result.ExitCode})");
            results.Add(result);
            onProgress?.Invoke(app, result, i + 1, apps.Count);
        }

        return results;
    }

    // liveConsoleOutput: winget detects when its own stdout isn't a real console (i.e. when
    // we redirect it to capture it) and suppresses/buffers its progress output almost entirely
    // in that case - so redirecting, even with streamed reads, can show nothing at all until
    // the process exits. Passing liveConsoleOutput: true skips redirection completely and lets
    // winget inherit our console directly, which is the only way to get its real animated
    // download percentage bar back. Trade-off: in that mode nothing is captured, so Output is
    // empty and onOutputLine is never called - the text is going straight to the screen, not
    // through this process.
    public static WingetInstallResult Install(string packageId, bool liveConsoleOutput = false, Action<string>? onOutputLine = null)
    {
        // Pre-check with "winget list" before calling "winget install".
        // winget install is ALSO idempotent on its own (it checks the same Add/Remove Programs
        // registry entries and just no-ops with exit code 0 if the package is already present),
        // so this isn't required for correctness - it just lets us report "AlreadyInstalled"
        // as its own status instead of lumping it in with "Installed", and skips re-invoking
        // the installer entirely when we already know the answer.
        Console.WriteLine($"[DEBUG] Install({packageId}): checking IsInstalled...");

        if (IsInstalled(packageId))
        {
            Console.WriteLine($"[DEBUG] Install({packageId}): already installed, skipping");
            return new WingetInstallResult(packageId, WingetInstallStatus.AlreadyInstalled, 0, string.Empty);
        }

        Console.WriteLine($"[DEBUG] Install({packageId}): not installed, building ProcessStartInfo");

        var startInfo = new ProcessStartInfo(WingetExecutablePath.Value)
        {
            RedirectStandardOutput = !liveConsoleOutput,
            RedirectStandardError = !liveConsoleOutput,
            UseShellExecute = false,
            CreateNoWindow = false
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

        using var process = new Process { StartInfo = startInfo };

        if (!liveConsoleOutput)
        {
            process.OutputDataReceived += OnDataReceived;
            process.ErrorDataReceived += OnDataReceived;
        }

        Console.WriteLine($"[DEBUG] Install({packageId}): calling process.Start()...");
        process.Start();
        Console.WriteLine($"[DEBUG] Install({packageId}): started, PID {process.Id}");

        if (!liveConsoleOutput)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        Console.WriteLine($"[DEBUG] Install({packageId}): waiting for exit...");
        process.WaitForExit();
        Console.WriteLine($"[DEBUG] Install({packageId}): exited with code {process.ExitCode}");

        var status = process.ExitCode == 0 ? WingetInstallStatus.Installed : WingetInstallStatus.Failed;
        return new WingetInstallResult(packageId, status, process.ExitCode, outputBuilder.ToString());
    }

    // "winget list --id <id> -e" exits 0 when a matching package is found, non-zero otherwise.
    // Detection is based on winget's own source data matched against the Add/Remove Programs
    // (ARP) registry entries - so this also catches packages installed via the vendor's own
    // installer/website, AS LONG AS that installer registered proper ARP entries (most do,
    // including Notepad++'s official installer). Portable/zip installs that skip ARP registration,
    // or installers with a mismatched display name/version, won't be detected this way.
    //
    // IMPORTANT: both stdout and stderr are redirected below, so they must be read concurrently,
    // never with sequential blocking ReadToEnd() calls - that's the classic Process redirection
    // deadlock: if the child fills up the OS pipe buffer on the stream nobody's draining yet
    // (e.g. winget writing warnings/update notices to stderr while we're still blocked reading
    // stdout), the child blocks on its write and never closes either stream, so our read never
    // returns either. This is what caused InstallAll to hang indefinitely on some machines.
    public static bool IsInstalled(string packageId)
    {
        var startInfo = new ProcessStartInfo(WingetExecutablePath.Value)
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

        Console.WriteLine($"[DEBUG] IsInstalled({packageId}): calling process.Start()...");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the winget process.");

        Console.WriteLine($"[DEBUG] IsInstalled({packageId}): started, PID {process.Id}, reading stdout+stderr concurrently...");

        // Must kick off both reads concurrently (not sequential ReadToEnd() calls) - see comment
        // on the method above. Draining both pipes at once avoids the deadlock where one pipe's
        // buffer fills up while nobody is reading it.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        Task.WaitAll(stdoutTask, stderrTask);

        Console.WriteLine($"[DEBUG] IsInstalled({packageId}): both streams read, waiting for exit...");

        process.WaitForExit();

        Console.WriteLine($"[DEBUG] IsInstalled({packageId}): exited with code {process.ExitCode}");

        return process.ExitCode == 0;
    }
}
