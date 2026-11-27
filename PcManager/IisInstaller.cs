using System.Diagnostics;
using System.Text;

namespace PcManager;

public sealed record IisFeature(string Name, string FeatureName);

public enum IisInstallStatus
{
    Installed,
    AlreadyInstalled,
    RebootRequired,
    Failed
}

public sealed record IisInstallResult(string FeatureName, IisInstallStatus Status, int ExitCode, string Output);

public static class IisInstaller
{
    // These DISM feature names are shared between client Windows (10/11) and Windows Server -
    // the "Web-Server" role name ServerManager/Install-WindowsFeature uses on Server is just an
    // alias over these same underlying component names, so this one list works on both SKUs.
    public static readonly IReadOnlyList<IisFeature> KnownFeatures =
    [
        new IisFeature("IIS Web Server Role", "IIS-WebServerRole"),
        new IisFeature("IIS Web Server", "IIS-WebServer"),
        new IisFeature("Common HTTP Features", "IIS-CommonHttpFeatures"),
        new IisFeature("Default Document", "IIS-DefaultDocument"),
        new IisFeature("Directory Browsing", "IIS-DirectoryBrowsing"),
        new IisFeature("HTTP Errors", "IIS-HttpErrors"),
        new IisFeature("Static Content", "IIS-StaticContent"),
        new IisFeature("HTTP Logging", "IIS-HttpLogging"),
        new IisFeature("Request Filtering", "IIS-RequestFiltering"),
        new IisFeature("Static Content Compression", "IIS-HttpCompressionStatic"),
        new IisFeature("Management Console", "IIS-ManagementConsole"),

        // Despite the "45" in the DISM name, these cover the whole .NET Framework 4.x family
        // (4.5 through 4.8.1) since it's a single in-place CLR (4.0.30319) - not just 4.5.
        new IisFeature(".NET Extensibility 4.8", "IIS-NetFxExtensibility45"),
        new IisFeature("ASP.NET 4.8", "IIS-ASPNET45"),

        // ASP.NET's managed handler mappings run through these - DISM's /all only auto-enables
        // parent features, not sibling dependencies, so ASP.NET 4.8 would enable but not actually
        // serve requests without these also being on.
        new IisFeature("ISAPI Extensions", "IIS-ISAPIExtensions"),
        new IisFeature("ISAPI Filters", "IIS-ISAPIFilter"),
    ];

    // DISM uses 3010 (not just a non-zero/non-zero split) to mean "succeeded, but a restart is
    // needed before the change takes effect" - common for feature installs. That's a success,
    // not a failure, so it gets its own status rather than being lumped into either bucket.
    private const int DismSuccessExitCode = 0;
    private const int DismRebootRequiredExitCode = 3010;

    public static IisInstallResult Install(IisFeature feature, bool liveConsoleOutput = false, Action<string>? onOutputLine = null) =>
        Install(feature.FeatureName, liveConsoleOutput, onOutputLine);

    public static IReadOnlyList<IisInstallResult> InstallAll(
        Action<IisFeature, IisInstallResult, int, int>? onProgress = null,
        bool liveConsoleOutput = false,
        Action<string>? onOutputLine = null)
    {
        var features = KnownFeatures;
        var results = new List<IisInstallResult>(features.Count);

        Console.WriteLine($"[DEBUG] InstallAll: {features.Count} feature(s) queued");

        for (var i = 0; i < features.Count; i++)
        {
            var feature = features[i];
            Console.WriteLine($"[DEBUG] InstallAll: starting {feature.Name} ({feature.FeatureName}) - {i + 1}/{features.Count}");
            var result = Install(feature.FeatureName, liveConsoleOutput, onOutputLine);
            Console.WriteLine($"[DEBUG] InstallAll: finished {feature.Name} -> {result.Status} (exit {result.ExitCode})");
            results.Add(result);
            onProgress?.Invoke(feature, result, i + 1, features.Count);
        }

        return results;
    }

    public static IisInstallResult Install(string featureName, bool liveConsoleOutput = false, Action<string>? onOutputLine = null)
    {
        Console.WriteLine($"[DEBUG] Install({featureName}): checking IsInstalled...");

        if (IsInstalled(featureName))
        {
            Console.WriteLine($"[DEBUG] Install({featureName}): already enabled, skipping");
            return new IisInstallResult(featureName, IisInstallStatus.AlreadyInstalled, 0, string.Empty);
        }

        Console.WriteLine($"[DEBUG] Install({featureName}): not enabled, building ProcessStartInfo");

        var startInfo = new ProcessStartInfo("dism")
        {
            RedirectStandardOutput = !liveConsoleOutput,
            RedirectStandardError = !liveConsoleOutput,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        if (!liveConsoleOutput)
        {
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;
        }

        startInfo.ArgumentList.Add("/online");
        startInfo.ArgumentList.Add("/enable-feature");
        startInfo.ArgumentList.Add($"/featurename:{featureName}");
        startInfo.ArgumentList.Add("/all");
        startInfo.ArgumentList.Add("/NoRestart");

        var outputBuilder = new StringBuilder();

        void OnLine(string line)
        {
            outputBuilder.AppendLine(line);
            onOutputLine?.Invoke(line);
        }

        using var process = new Process { StartInfo = startInfo };

        Console.WriteLine($"[DEBUG] Install({featureName}): calling process.Start()...");
        process.Start();
        Console.WriteLine($"[DEBUG] Install({featureName}): started, PID {process.Id}");

        Task? stdoutTask = null;
        Task? stderrTask = null;

        if (!liveConsoleOutput)
        {
            // DISM draws its "[== NN.N% ==]" progress bar with bare '\r' updates, same as choco's
            // download percentage - Process.BeginOutputReadLine()/OutputDataReceived only treats
            // '\n' as a line terminator, so those updates would sit buffered and invisible until a
            // real newline showed up. Reading the raw stream ourselves and splitting on '\r' too
            // fixes that - see ChocoInstaller.ReadStreamUpdatesAsync for the fuller explanation.
            // Both streams are read concurrently to avoid the classic Process redirection deadlock.
            stdoutTask = ReadStreamUpdatesAsync(process.StandardOutput, OnLine);
            stderrTask = ReadStreamUpdatesAsync(process.StandardError, OnLine);
        }

        Console.WriteLine($"[DEBUG] Install({featureName}): waiting for exit...");
        process.WaitForExit();

        if (stdoutTask is not null && stderrTask is not null)
        {
            Task.WaitAll(stdoutTask, stderrTask);
        }

        Console.WriteLine($"[DEBUG] Install({featureName}): exited with code {process.ExitCode}");

        var status = process.ExitCode switch
        {
            DismSuccessExitCode => IisInstallStatus.Installed,
            DismRebootRequiredExitCode => IisInstallStatus.RebootRequired,
            _ => IisInstallStatus.Failed
        };

        return new IisInstallResult(featureName, status, process.ExitCode, outputBuilder.ToString());
    }

    // "dism /online /get-featureinfo /featurename:<name>" exits 0 whether the feature is enabled
    // or disabled, so detection has to come from the "State : Enabled/Disabled" line in its output
    // rather than the exit code - same quirk as choco's "list" command (see ChocoInstaller).
    public static bool IsInstalled(string featureName)
    {
        var startInfo = new ProcessStartInfo("dism")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("/online");
        startInfo.ArgumentList.Add("/get-featureinfo");
        startInfo.ArgumentList.Add($"/featurename:{featureName}");

        Console.WriteLine($"[DEBUG] IsInstalled({featureName}): calling process.Start()...");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the dism process.");

        Console.WriteLine($"[DEBUG] IsInstalled({featureName}): started, PID {process.Id}, reading stdout+stderr concurrently...");

        // Concurrent reads to avoid the classic Process redirection deadlock - see
        // WingetInstaller.IsInstalled for the full explanation of why this matters.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        Task.WaitAll(stdoutTask, stderrTask);

        Console.WriteLine($"[DEBUG] IsInstalled({featureName}): both streams read, waiting for exit...");

        process.WaitForExit();

        Console.WriteLine($"[DEBUG] IsInstalled({featureName}): exited with code {process.ExitCode}");

        return stdoutTask.Result.Contains("State : Enabled", StringComparison.OrdinalIgnoreCase);
    }

    // Reads a process stream character-by-character and invokes onUpdate once per '\r'- or
    // '\n'-terminated chunk, so a bare '\r' (used for in-place progress updates) is treated as
    // its own update instead of being swallowed until the next real newline. Same fix as
    // ChocoInstaller.ReadStreamUpdatesAsync.
    private static async Task ReadStreamUpdatesAsync(StreamReader reader, Action<string> onUpdate)
    {
        var buffer = new char[1];
        var current = new StringBuilder();

        while (await reader.ReadAsync(buffer, 0, 1) > 0)
        {
            var c = buffer[0];

            if (c is '\r' or '\n')
            {
                if (current.Length > 0)
                {
                    onUpdate(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            onUpdate(current.ToString());
        }
    }
}
