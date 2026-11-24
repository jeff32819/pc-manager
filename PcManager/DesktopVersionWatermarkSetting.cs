using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PcManager;

public enum DesktopVersionVisibility
{
    Hidden,
    Shown
}

public sealed class DesktopVersionWatermarkSetting : IToggleSetting
{
    public static readonly DesktopVersionWatermarkSetting Instance = new();

    private const string KeyPath = @"Control Panel\Desktop";
    private const string ValueName = "PaintDesktopVersion";

    private DesktopVersionWatermarkSetting()
    {
    }

    public DesktopVersionVisibility Status
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return Convert.ToInt32(key?.GetValue(ValueName, 0)) != 0 ? DesktopVersionVisibility.Shown : DesktopVersionVisibility.Hidden;
        }
    }

    object IToggleSetting.Status => Status;

    public bool? IsEnabled => Status == DesktopVersionVisibility.Shown;

    // Confirmed by direct test: unlike wallpaper/icon-spacing settings, UpdatePerUserSystemParameters
    // alone does NOT make this watermark appear/disappear live - Explorer only reads
    // PaintDesktopVersion when the desktop itself initializes. Restarting explorer.exe was
    // enough (a full sign-out/sign-in was not required). Enable()/Disable() do NOT restart
    // Explorer by default - that flashes the desktop/taskbar, which not every caller wants -
    // so the registry value is set but won't visibly change until the caller either restarts
    // Explorer themselves, calls the restartExplorer: true overload below, or the user next
    // signs out and back in.
    public void Enable() => SetValue(1, restartExplorer: false);

    public void Disable() => SetValue(0, restartExplorer: false);

    public void Enable(bool restartExplorer) => SetValue(1, restartExplorer);

    public void Disable(bool restartExplorer) => SetValue(0, restartExplorer);

    private static void SetValue(int value, bool restartExplorer)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true)
            ?? throw new InvalidOperationException($@"Registry key HKEY_CURRENT_USER\{KeyPath} was not found.");

        key.SetValue(ValueName, value, RegistryValueKind.DWord);
        UpdatePerUserSystemParameters(1, true);

        if (restartExplorer)
        {
            RestartExplorer();
        }
    }

    private static void RestartExplorer()
    {
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            process.Kill();
        }

        Process.Start("explorer.exe");
    }

    [DllImport("user32.dll")]
    private static extern void UpdatePerUserSystemParameters(int dwReserved, bool bEnable);
}
