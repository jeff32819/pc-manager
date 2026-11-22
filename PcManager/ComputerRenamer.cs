using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PcManager;

public static class ComputerRenamer
{
    private const int MaxComputerNameLength = 15;

    public static void Rename(string pcName)
    {
        if (string.IsNullOrWhiteSpace(pcName))
        {
            throw new ArgumentException("Computer name cannot be null or empty.", nameof(pcName));
        }

        if (pcName.Length > MaxComputerNameLength)
        {
            throw new ArgumentException($"Computer name cannot exceed {MaxComputerNameLength} characters. (for NetBIOS computer name)", nameof(pcName));
        }

        if (!SetComputerNameEx(COMPUTER_NAME_FORMAT.ComputerNamePhysicalNetBIOS, pcName))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (!SetComputerNameEx(COMPUTER_NAME_FORMAT.ComputerNamePhysicalDnsHostname, pcName))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private enum COMPUTER_NAME_FORMAT
    {
        ComputerNameNetBIOS,
        ComputerNameDnsHostname,
        ComputerNameDnsDomain,
        ComputerNameDnsFullyQualified,
        ComputerNamePhysicalNetBIOS,
        ComputerNamePhysicalDnsHostname,
        ComputerNamePhysicalDnsDomain,
        ComputerNamePhysicalDnsFullyQualified,
        ComputerNameMax
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetComputerNameEx(COMPUTER_NAME_FORMAT nameType, string lpBuffer);
}
