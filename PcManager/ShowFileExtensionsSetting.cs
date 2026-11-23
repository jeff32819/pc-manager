using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PcManager;

public enum FileExtensionVisibility
{
    Shown,
    Hidden
}

public sealed class ShowFileExtensionsSetting : IToggleSetting
{
    public static readonly ShowFileExtensionsSetting Instance = new();

    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ValueName = "HideFileExt";

    private ShowFileExtensionsSetting()
    {
    }

    public FileExtensionVisibility Status
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return Convert.ToInt32(key?.GetValue(ValueName, 1)) == 0 ? FileExtensionVisibility.Shown : FileExtensionVisibility.Hidden;
        }
    }

    object IToggleSetting.Status => Status;

    public bool? IsEnabled => Status == FileExtensionVisibility.Shown;

    public void Enable() => SetHideFileExt(0);

    public void Disable() => SetHideFileExt(1);

    private static void SetHideFileExt(int value)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true)
            ?? throw new InvalidOperationException($@"Registry key HKEY_CURRENT_USER\{KeyPath} was not found.");

        key.SetValue(ValueName, value, RegistryValueKind.DWord);
        RefreshExplorer();
    }

    private static void RefreshExplorer() =>
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
