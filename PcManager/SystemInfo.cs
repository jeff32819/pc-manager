using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace PcManager;

public sealed record DriveSummary(string Name, DriveType DriveType, bool IsReady, string? Format, long TotalSizeBytes, long AvailableFreeSpaceBytes);

public static class SystemInfo
{
    public static bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static string UserName => Environment.UserName;

    public static string Domain => Environment.UserDomainName;

    public static string MachineName => Environment.MachineName;

    public static IReadOnlyList<DriveSummary> Drives =>
        DriveInfo.GetDrives()
            .Select(d => new DriveSummary(
                d.Name,
                d.DriveType,
                d.IsReady,
                d.IsReady ? d.DriveFormat : null,
                d.IsReady ? d.TotalSize : 0,
                d.IsReady ? d.AvailableFreeSpace : 0))
            .ToList();

    public static long TotalPhysicalMemoryBytes => (long)GetMemoryStatus().ullTotalPhys;

    public static long AvailablePhysicalMemoryBytes => (long)GetMemoryStatus().ullAvailPhys;

    public static TimeSpan Uptime => TimeSpan.FromMilliseconds(GetTickCount64());

    public static DateTime LastBootTimeUtc => DateTime.UtcNow - Uptime;

    public static int ProcessorCount => Environment.ProcessorCount;

    public static string ProcessorName
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "Unknown";
        }
    }

    public static string OperatingSystem
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var productName = key?.GetValue("ProductName") as string ?? "Unknown";
            var displayVersion = key?.GetValue("DisplayVersion") as string;
            var buildNumber = key?.GetValue("CurrentBuildNumber") as string;

            if (buildNumber is null)
            {
                return productName;
            }

            var versionSuffix = displayVersion is null ? string.Empty : $" {displayVersion}";
            return $"{productName}{versionSuffix} (Build {buildNumber})";
        }
    }

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    private static MEMORYSTATUSEX GetMemoryStatus()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };

        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return status;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
