using System.Diagnostics;

namespace PcManager;

public enum WindowsTimeZone
{
    Utc,
    HawaiianStandardTime,
    AlaskanStandardTime,
    PacificStandardTime,
    UsMountainStandardTime,
    MountainStandardTime,
    CentralStandardTime,
    EasternStandardTime,
    AtlanticStandardTime,
    NewfoundlandStandardTime,
    GmtStandardTime,
    WEuropeStandardTime,
    CentralEuropeStandardTime,
    RomanceStandardTime,
    GtbStandardTime,
    FleStandardTime,
    RussianStandardTime,
    EgyptStandardTime,
    IsraelStandardTime,
    ArabicStandardTime,
    ArabStandardTime,
    IndiaStandardTime,
    ChinaStandardTime,
    SingaporeStandardTime,
    TokyoStandardTime,
    KoreaStandardTime,
    SouthAfricaStandardTime,
    WAustraliaStandardTime,
    AusCentralStandardTime,
    AusEasternStandardTime,
    NewZealandStandardTime
}

public static class TimeZoneManager
{
    // Enum member -> the canonical Windows time zone registry ID (same strings TimeZoneInfo
    // and tzutil.exe use, under HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Time Zones).
    // Validated against the OS at Set() time via TimeZoneInfo.FindSystemTimeZoneById, so a
    // typo'd/missing ID throws a clear exception rather than tzutil silently doing nothing.
    private static readonly Dictionary<WindowsTimeZone, string> Ids = new()
    {
        [WindowsTimeZone.Utc] = "UTC",
        [WindowsTimeZone.HawaiianStandardTime] = "Hawaiian Standard Time",
        [WindowsTimeZone.AlaskanStandardTime] = "Alaskan Standard Time",
        [WindowsTimeZone.PacificStandardTime] = "Pacific Standard Time",
        [WindowsTimeZone.UsMountainStandardTime] = "US Mountain Standard Time",
        [WindowsTimeZone.MountainStandardTime] = "Mountain Standard Time",
        [WindowsTimeZone.CentralStandardTime] = "Central Standard Time",
        [WindowsTimeZone.EasternStandardTime] = "Eastern Standard Time",
        [WindowsTimeZone.AtlanticStandardTime] = "Atlantic Standard Time",
        [WindowsTimeZone.NewfoundlandStandardTime] = "Newfoundland Standard Time",
        [WindowsTimeZone.GmtStandardTime] = "GMT Standard Time",
        [WindowsTimeZone.WEuropeStandardTime] = "W. Europe Standard Time",
        [WindowsTimeZone.CentralEuropeStandardTime] = "Central Europe Standard Time",
        [WindowsTimeZone.RomanceStandardTime] = "Romance Standard Time",
        [WindowsTimeZone.GtbStandardTime] = "GTB Standard Time",
        [WindowsTimeZone.FleStandardTime] = "FLE Standard Time",
        [WindowsTimeZone.RussianStandardTime] = "Russian Standard Time",
        [WindowsTimeZone.EgyptStandardTime] = "Egypt Standard Time",
        [WindowsTimeZone.IsraelStandardTime] = "Israel Standard Time",
        [WindowsTimeZone.ArabicStandardTime] = "Arabic Standard Time",
        [WindowsTimeZone.ArabStandardTime] = "Arab Standard Time",
        [WindowsTimeZone.IndiaStandardTime] = "India Standard Time",
        [WindowsTimeZone.ChinaStandardTime] = "China Standard Time",
        [WindowsTimeZone.SingaporeStandardTime] = "Singapore Standard Time",
        [WindowsTimeZone.TokyoStandardTime] = "Tokyo Standard Time",
        [WindowsTimeZone.KoreaStandardTime] = "Korea Standard Time",
        [WindowsTimeZone.SouthAfricaStandardTime] = "South Africa Standard Time",
        [WindowsTimeZone.WAustraliaStandardTime] = "W. Australia Standard Time",
        [WindowsTimeZone.AusCentralStandardTime] = "AUS Central Standard Time",
        [WindowsTimeZone.AusEasternStandardTime] = "AUS Eastern Standard Time",
        [WindowsTimeZone.NewZealandStandardTime] = "New Zealand Standard Time"
    };

    public static string CurrentTimeZoneId => TimeZoneInfo.Local.Id;

    public static TimeZoneInfo Current => TimeZoneInfo.Local;

    public static IReadOnlyList<TimeZoneInfo> List() => TimeZoneInfo.GetSystemTimeZones().ToList();

    public static void Set(WindowsTimeZone zone)
    {
        if (!Ids.TryGetValue(zone, out var id))
        {
            throw new ArgumentOutOfRangeException(nameof(zone), zone, "No Windows time zone ID is mapped for this enum member.");
        }

        Set(id);
    }

    public static void Set(string windowsTimeZoneId)
    {
        // Throws TimeZoneNotFoundException if this OS doesn't know the ID - fail fast with a
        // clear error instead of letting tzutil silently no-op on a bad/unsupported ID.
        TimeZoneInfo.FindSystemTimeZoneById(windowsTimeZoneId);

        var startInfo = new ProcessStartInfo("tzutil")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("/s");
        startInfo.ArgumentList.Add(windowsTimeZoneId);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start tzutil.");

        // Read both streams concurrently, never sequential ReadToEnd() calls - see the same
        // deadlock note in WingetInstaller.IsInstalled.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        Task.WaitAll(stdoutTask, stderrTask);

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var message = stderrTask.Result.Trim();
            throw new InvalidOperationException(string.IsNullOrEmpty(message)
                ? $"tzutil failed with exit code {process.ExitCode}."
                : $"tzutil failed with exit code {process.ExitCode}: {message}");
        }
    }
}
