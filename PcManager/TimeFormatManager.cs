using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PcManager;

public enum TimeFormat
{
    Standard12Hour,
    Military24Hour
}

public static class TimeFormatManager
{
    private const string KeyPath = @"Control Panel\International";

    public static TimeFormat Status
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue("iTime") as string == "1" ? TimeFormat.Military24Hour : TimeFormat.Standard12Hour;
        }
    }

    public static void SetMilitaryTime() => SetFormat(TimeFormat.Military24Hour);

    public static void SetStandardTime() => SetFormat(TimeFormat.Standard12Hour);

    private static void SetFormat(TimeFormat format)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true)
            ?? throw new InvalidOperationException($@"Registry key HKEY_CURRENT_USER\{KeyPath} was not found.");

        if (format == TimeFormat.Military24Hour)
        {
            key.SetValue("iTime", "1");
            key.SetValue("sTimeFormat", "HH:mm:ss");
            key.SetValue("sShortTime", "HH:mm");
        }
        else
        {
            key.SetValue("iTime", "0");
            key.SetValue("sTimeFormat", "h:mm:ss tt");
            key.SetValue("sShortTime", "h:mm tt");
        }

        BroadcastSettingChange();
    }

    private static void BroadcastSettingChange()
    {
        var lParam = Marshal.StringToHGlobalUni("intl");

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
