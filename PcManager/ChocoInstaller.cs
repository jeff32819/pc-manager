using System.Diagnostics;
using System.Text;

namespace PcManager;

public sealed record ChocoApp(string Name, string PackageId);

public enum ChocoInstallStatus
{
    Installed,
    AlreadyInstalled,
    Failed
}

public sealed record ChocoInstallResult(string PackageId, ChocoInstallStatus Status, int ExitCode, string Output);

public static class ChocoInstaller
{
    public static readonly IReadOnlyList<ChocoApp> KnownApps =
    [
        new ChocoApp("AnyToISO", "anytoiso")
    ];

    private const string ChocolateyWingetId = "Chocolatey.Chocolatey";

    public static bool IsChocolateyInstalled() => File.Exists(Path.Combine(GetInstallRoot(), "bin", "choco.exe"));

    public static void EnsureChocolateyInstalled()
    {
        Console.WriteLine($"[DEBUG] EnsureChocolateyInstalled: IsChocolateyInstalled={IsChocolateyInstalled()}, root={GetInstallRoot()}");

        if (!IsChocolateyInstalled())
        {
            Console.WriteLine("[DEBUG] EnsureChocolateyInstalled: installing via winget...");
            var result = WingetInstaller.Install(ChocolateyWingetId);
            Console.WriteLine($"[DEBUG] EnsureChocolateyInstalled: winget install finished, status={result.Status}");

            if (result.Status == WingetInstallStatus.Failed)
            {
                throw new InvalidOperationException(
                    $"Failed to install Chocolatey via winget (exit code {result.ExitCode}).\n{result.Output}");
            }
        }

        // Chocolatey's bin folder may be on PATH for a fresh shell but NOT for this already-running
        // process - PATH is inherited from the parent process at launch time and never re-read live,
        // so a process started before Chocolatey was installed (by us just now, or at any point before
        // this process launched) won't resolve "choco" even though the file genuinely exists on disk.
        // This runs every time, not just in the "we just installed it" branch above, since that stale-
        // PATH problem can happen either way.
        var binPath = Path.Combine(GetInstallRoot(), "bin");
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        if (!currentPath.Split(Path.PathSeparator).Contains(binPath, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[DEBUG] EnsureChocolateyInstalled: adding {binPath} to this process' PATH");
            Environment.SetEnvironmentVariable("PATH", $"{currentPath}{Path.PathSeparator}{binPath}");
        }
        else
        {
            Console.WriteLine($"[DEBUG] EnsureChocolateyInstalled: {binPath} already on this process' PATH");
        }
    }

    private static string GetInstallRoot() =>
        Environment.GetEnvironmentVariable("ChocolateyInstall", EnvironmentVariableTarget.Machine)
        ?? Environment.GetEnvironmentVariable("ChocolateyInstall")
        ?? @"C:\ProgramData\chocolatey";

    public static ChocoInstallResult Install(ChocoApp app, bool liveConsoleOutput = false, Action<string>? onOutputLine = null) =>
        Install(app.PackageId, liveConsoleOutput, onOutputLine);

    public static IReadOnlyList<ChocoInstallResult> InstallAll(
        Action<ChocoApp, ChocoInstallResult, int, int>? onProgress = null,
        bool liveConsoleOutput = false,
        Action<string>? onOutputLine = null)
    {
        var apps = KnownApps;
        var results = new List<ChocoInstallResult>(apps.Count);

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

    public static ChocoInstallResult Install(string packageId, bool liveConsoleOutput = false, Action<string>? onOutputLine = null)
    {
        // Self-bootstrapping: a cheap File.Exists check after the first successful install, so
        // callers don't have to remember to call EnsureChocolateyInstalled() separately before
        // using this class on a machine that doesn't have Chocolatey yet.
        EnsureChocolateyInstalled();

        Console.WriteLine($"[DEBUG] Install({packageId}): checking IsInstalled...");

        if (IsInstalled(packageId))
        {
            Console.WriteLine($"[DEBUG] Install({packageId}): already installed, skipping");
            return new ChocoInstallResult(packageId, ChocoInstallStatus.AlreadyInstalled, 0, string.Empty);
        }

        Console.WriteLine($"[DEBUG] Install({packageId}): not installed, building ProcessStartInfo");

        var startInfo = new ProcessStartInfo("choco")
        {
            RedirectStandardOutput = !liveConsoleOutput,
            RedirectStandardError = !liveConsoleOutput,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        startInfo.ArgumentList.Add("install");
        startInfo.ArgumentList.Add(packageId);
        startInfo.ArgumentList.Add("-y");

        // Deliberately not passing --no-progress: that flag suppresses the "Progress: NN%" lines
        // choco writes during download, which callers need for onOutputLine-based progress bars.

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

        var status = process.ExitCode == 0 ? ChocoInstallStatus.Installed : ChocoInstallStatus.Failed;
        return new ChocoInstallResult(packageId, status, process.ExitCode, outputBuilder.ToString());
    }

    // "choco list --local-only <id> --exact -r" prints "id|version" on a match and nothing
    // otherwise - unlike winget, choco's own exit code is 0 either way, so detection has to
    // come from the output text rather than the exit code.
    public static bool IsInstalled(string packageId)
    {
        var startInfo = new ProcessStartInfo("choco")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("list");
        startInfo.ArgumentList.Add("--local-only");
        startInfo.ArgumentList.Add(packageId);
        startInfo.ArgumentList.Add("--exact");
        startInfo.ArgumentList.Add("-r");

        Console.WriteLine($"[DEBUG] IsInstalled({packageId}): calling process.Start()...");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the choco process.");

        Console.WriteLine($"[DEBUG] IsInstalled({packageId}): started, PID {process.Id}, reading stdout+stderr concurrently...");

        // Concurrent reads to avoid the classic Process redirection deadlock - see
        // WingetInstaller.IsInstalled for the full explanation of why this matters.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        Task.WaitAll(stdoutTask, stderrTask);

        Console.WriteLine($"[DEBUG] IsInstalled({packageId}): both streams read, waiting for exit...");

        process.WaitForExit();

        Console.WriteLine($"[DEBUG] IsInstalled({packageId}): exited with code {process.ExitCode}");

        var output = stdoutTask.Result.Trim();
        return output.Length > 0 && output.StartsWith(packageId, StringComparison.OrdinalIgnoreCase);
    }
}
