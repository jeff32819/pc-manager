using Microsoft.Win32;

namespace PcManager;

public enum RemoteDesktopStatus
{
    Enabled,
    Disabled
}

public static class RemoteDesktopManager
{
    private const string KeyPath = @"SYSTEM\CurrentControlSet\Control\Terminal Server";
    private const string ValueName = "fDenyTSConnections";

    public static RemoteDesktopStatus Status
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey(KeyPath)
                ?? throw new InvalidOperationException($@"Registry key HKEY_LOCAL_MACHINE\{KeyPath} was not found.");

            var denyConnections = Convert.ToInt32(key.GetValue(ValueName, 1));
            return denyConnections == 0 ? RemoteDesktopStatus.Enabled : RemoteDesktopStatus.Disabled;
        }
    }

    public static void Enable() => SetDenyConnections(0);

    public static void Disable() => SetDenyConnections(1);

    private static void SetDenyConnections(int value)
    {
        using var key = Registry.LocalMachine.OpenSubKey(KeyPath, writable: true)
            ?? throw new InvalidOperationException($@"Registry key HKEY_LOCAL_MACHINE\{KeyPath} was not found.");

        key.SetValue(ValueName, value, RegistryValueKind.DWord);
    }
}
