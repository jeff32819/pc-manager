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
        if (!IsChocolateyInstalled())
        {
            var result = WingetInstaller.Install(ChocolateyWingetId);

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
            Environment.SetEnvironmentVariable("PATH", $"{currentPath}{Path.PathSeparator}{binPath}");
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

        for (var i = 0; i < apps.Count; i++)
        {
            var app = apps[i];
            var result = Install(app.PackageId, liveConsoleOutput, onOutputLine);
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

        if (IsInstalled(packageId))
        {
            return new ChocoInstallResult(packageId, ChocoInstallStatus.AlreadyInstalled, 0, string.Empty);
        }

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
        startInfo.ArgumentList.Add("--no-progress");

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

        process.Start();

        if (!liveConsoleOutput)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        process.WaitForExit();

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

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the choco process.");

        // Concurrent reads to avoid the classic Process redirection deadlock - see
        // WingetInstaller.IsInstalled for the full explanation of why this matters.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        Task.WaitAll(stdoutTask, stderrTask);

        process.WaitForExit();

        var output = stdoutTask.Result.Trim();
        return output.Length > 0 && output.StartsWith(packageId, StringComparison.OrdinalIgnoreCase);
    }
}
