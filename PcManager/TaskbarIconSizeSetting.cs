using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PcManager;

public enum TaskbarIconSize
{
    Small,
    Medium,
    Large
}

// TaskbarSi is a Windows 11 24H2+ feature (Settings > Personalization > Taskbar > Taskbar size).
// The older Windows 10 value, "TaskbarSmallIcons", stopped having any effect once Windows 11's
// rewritten taskbar shipped (small icons were removed from the UI entirely on 21H2-23H2) - so
// this targets the value that's actually honored on current Windows 11, not the legacy one.
public sealed class TaskbarIconSizeSetting
{
    public static readonly TaskbarIconSizeSetting Instance = new();

    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ValueName = "TaskbarSi";

    // Windows 11 24H2 is build 26100. Windows 10 never reached anywhere near this build number,
    // so this check alone also correctly rules out Windows 10 (which, like 11 21H2-23H2, has no
    // concept of TaskbarSi at all).
    private const int MinimumSupportedBuild = 26100;

    private TaskbarIconSizeSetting()
    {
    }

    public static bool IsSupported => Environment.OSVersion.Version.Build >= MinimumSupportedBuild;

    public TaskbarIconSize Status
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            var value = Convert.ToInt32(key?.GetValue(ValueName, 1));
            return value switch
            {
                0 => TaskbarIconSize.Small,
                2 => TaskbarIconSize.Large,
                _ => TaskbarIconSize.Medium
            };
        }
    }

    public void Set(TaskbarIconSize size)
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException(
                $"Taskbar icon size requires Windows 11 24H2 (build {MinimumSupportedBuild}) or later; " +
                $"this machine is running build {Environment.OSVersion.Version.Build}.");
        }

        var value = size switch
        {
            TaskbarIconSize.Small => 0,
            TaskbarIconSize.Large => 2,
            _ => 1
        };

        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true)
            ?? throw new InvalidOperationException($@"Registry key HKEY_CURRENT_USER\{KeyPath} was not found.");

        key.SetValue(ValueName, value, RegistryValueKind.DWord);

        // Best-effort live refresh. The Windows 11 taskbar is largely a separate XAML-based
        // process from classic Explorer shell code now, so unlike the simpler Explorer\Advanced
        // toggles (file extensions, etc.), this broadcast isn't guaranteed to repaint the taskbar
        // immediately - restarting explorer.exe (or signing out/in) may still be needed to see it.
        BroadcastSettingChange();
    }

    private static void BroadcastSettingChange()
    {
        var lParam = Marshal.StringToHGlobalUni("TraySettings");

        try
        {
            SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, lParam, SMTO_ABORTIFHUNG, 1000, out _);
        }
        finally
        {
            Marshal.FreeHGlobal(lParam);
        }
    }

    private const int WM_SETTINGCHANGE = 0x001A;
    private const int SMTO_ABORTIFHUNG = 0x0002;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, int flags, int timeout, out IntPtr result);
}
