using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PcManager;

public enum ContextMenuVisibility
{
    Shown,
    Hidden
}

public sealed class CopyMoveToContextMenuSetting : IToggleSetting
{
    public static readonly CopyMoveToContextMenuSetting Instance = new();

    private const string KeyPath = @"AllFilesystemObjects\shellex\ContextMenuHandlers";
    private const string CopyToClsid = "{C2FBB630-2971-11D1-A18C-00C04FD75D13}";
    private const string MoveToClsid = "{C2FBB631-2971-11D1-A18C-00C04FD75D13}";

    private CopyMoveToContextMenuSetting()
    {
    }

    public ContextMenuVisibility Status
    {
        get
        {
            using var key = Registry.ClassesRoot.OpenSubKey(KeyPath);
            var hasCopyTo = key?.OpenSubKey(CopyToClsid) is not null;
            var hasMoveTo = key?.OpenSubKey(MoveToClsid) is not null;
            return hasCopyTo && hasMoveTo ? ContextMenuVisibility.Shown : ContextMenuVisibility.Hidden;
        }
    }

    object IToggleSetting.Status => Status;

    public bool? IsEnabled => Status == ContextMenuVisibility.Shown;

    public void Enable()
    {
        using var key = Registry.ClassesRoot.CreateSubKey(KeyPath)
            ?? throw new InvalidOperationException($@"Registry key HKEY_CLASSES_ROOT\{KeyPath} could not be created.");

        using (var copyToKey = key.CreateSubKey(CopyToClsid))
        {
            copyToKey.SetValue(string.Empty, "Copy To");
        }

        using (var moveToKey = key.CreateSubKey(MoveToClsid))
        {
            moveToKey.SetValue(string.Empty, "Move To");
        }

        RefreshExplorer();
    }

    public void Disable()
    {
        using var key = Registry.ClassesRoot.OpenSubKey(KeyPath, writable: true);

        key?.DeleteSubKeyTree(CopyToClsid, throwOnMissingSubKey: false);
        key?.DeleteSubKeyTree(MoveToClsid, throwOnMissingSubKey: false);

        RefreshExplorer();
    }

    private static void RefreshExplorer() =>
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
