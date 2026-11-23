using Microsoft.Win32;

namespace PcManager;

public enum RemoteDesktopStatus
{
    Enabled,
    Disabled
}

public sealed class RemoteDesktopManager : IToggleSetting
{
    public static readonly RemoteDesktopManager Instance = new();

    private const string KeyPath = @"SYSTEM\CurrentControlSet\Control\Terminal Server";
    private const string ValueName = "fDenyTSConnections";

    private RemoteDesktopManager()
    {
    }

    public RemoteDesktopStatus Status
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey(KeyPath)
                ?? throw new InvalidOperationException($@"Registry key HKEY_LOCAL_MACHINE\{KeyPath} was not found.");

            var denyConnections = Convert.ToInt32(key.GetValue(ValueName, 1));
            return denyConnections == 0 ? RemoteDesktopStatus.Enabled : RemoteDesktopStatus.Disabled;
        }
    }

    object IToggleSetting.Status => Status;

    public bool? IsEnabled => Status == RemoteDesktopStatus.Enabled;

    public void Enable() => SetDenyConnections(0);

    public void Disable() => SetDenyConnections(1);

    private static void SetDenyConnections(int value)
    {
        using var key = Registry.LocalMachine.OpenSubKey(KeyPath, writable: true)
            ?? throw new InvalidOperationException($@"Registry key HKEY_LOCAL_MACHINE\{KeyPath} was not found.");

        key.SetValue(ValueName, value, RegistryValueKind.DWord);
    }
}
