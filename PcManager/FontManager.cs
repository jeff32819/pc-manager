using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace PcManager;

public sealed record InstalledFont(string Name, string FilePath, InstallScope Scope);

public sealed record FontInstallResult(string FilePath, bool Success, Exception? Error);

public static class FontManager
{
    private const string FontsKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
    private static readonly HashSet<string> FontExtensions = new(StringComparer.OrdinalIgnoreCase) { ".ttf", ".ttc", ".otf" };

    public static void Install(string fontFilePath, InstallScope scope)
    {
        if (!File.Exists(fontFilePath))
        {
            throw new FileNotFoundException("Font file was not found.", fontFilePath);
        }

        var (displayName, isOpenType) = ReadFontName(fontFilePath);
        var valueName = $"{displayName} ({(isOpenType ? "OpenType" : "TrueType")})";

        var fontsDirectory = GetFontsDirectory(scope);
        Directory.CreateDirectory(fontsDirectory);

        var destinationPath = Path.Combine(fontsDirectory, Path.GetFileName(fontFilePath));
        File.Copy(fontFilePath, destinationPath, overwrite: true);

        using var key = OpenFontsKey(scope, writable: true)!;
        key.SetValue(valueName, scope == InstallScope.AllUsers ? Path.GetFileName(destinationPath) : destinationPath, RegistryValueKind.String);

        AddFontResourceW(destinationPath);
        BroadcastFontChange();
    }

    public static IReadOnlyList<FontInstallResult> InstallFromFolder(string folderPath, InstallScope scope, bool recursive = false)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder was not found: {folderPath}");
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var fontFiles = Directory.EnumerateFiles(folderPath, "*.*", searchOption)
            .Where(f => FontExtensions.Contains(Path.GetExtension(f)));

        var results = new List<FontInstallResult>();

        foreach (var fontFile in fontFiles)
        {
            try
            {
                Install(fontFile, scope);
                results.Add(new FontInstallResult(fontFile, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new FontInstallResult(fontFile, false, ex));
            }
        }

        return results;
    }

    public static IReadOnlyList<InstalledFont> List(InstallScope? scope = null)
    {
        InstallScope[] scopes = scope is null ? [InstallScope.CurrentUser, InstallScope.AllUsers] : [scope.Value];
        var results = new List<InstalledFont>();

        foreach (var s in scopes)
        {
            using var key = OpenFontsKey(s, writable: false);
            if (key is null)
            {
                continue;
            }

            foreach (var valueName in key.GetValueNames())
            {
                if (key.GetValue(valueName) is not string value || value.Length == 0)
                {
                    continue;
                }

                var filePath = Path.IsPathRooted(value) ? value : Path.Combine(GetFontsDirectory(s), value);
                results.Add(new InstalledFont(valueName, filePath, s));
            }
        }

        return results;
    }

    public static IReadOnlyList<InstalledFont> Search(string searchTerm, InstallScope? scope = null) =>
        List(scope)
            .Where(f => f.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public static void Remove(string fontName, InstallScope scope)
    {
        using var key = OpenFontsKey(scope, writable: true)
            ?? throw new InvalidOperationException($"No fonts registry key found for scope {scope}.");

        var valueName = key.GetValueNames().FirstOrDefault(n => string.Equals(n, fontName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Font '{fontName}' is not installed for scope {scope}.", nameof(fontName));

        var value = key.GetValue(valueName) as string;
        key.DeleteValue(valueName);

        if (!string.IsNullOrEmpty(value))
        {
            var filePath = Path.IsPathRooted(value) ? value : Path.Combine(GetFontsDirectory(scope), value);
            RemoveFontResourceW(filePath);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        BroadcastFontChange();
    }

    private static string GetFontsDirectory(InstallScope scope) => scope switch
    {
        InstallScope.AllUsers => Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
        InstallScope.CurrentUser => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
    };

    private static RegistryKey? OpenFontsKey(InstallScope scope, bool writable) => scope switch
    {
        InstallScope.AllUsers => Registry.LocalMachine.OpenSubKey(FontsKeyPath, writable),
        InstallScope.CurrentUser => Registry.CurrentUser.OpenSubKey(FontsKeyPath, writable)
            ?? (writable ? Registry.CurrentUser.CreateSubKey(FontsKeyPath) : null),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
    };

    private static void BroadcastFontChange() =>
        SendMessageTimeout(HWND_BROADCAST, WM_FONTCHANGE, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 1000, out _);

    private static (string DisplayName, bool IsOpenType) ReadFontName(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var offset = 0;

        if (ReadTag(bytes, 0) == "ttcf")
        {
            offset = (int)ReadUInt32(bytes, 12);
        }

        var isOpenType = ReadTag(bytes, offset) == "OTTO";
        var numTables = ReadUInt16(bytes, offset + 4);
        var nameTableOffset = -1;

        for (var i = 0; i < numTables; i++)
        {
            var recordOffset = offset + 12 + i * 16;
            if (ReadTag(bytes, recordOffset) == "name")
            {
                nameTableOffset = (int)ReadUInt32(bytes, recordOffset + 8);
                break;
            }
        }

        if (nameTableOffset < 0)
        {
            return (Path.GetFileNameWithoutExtension(filePath), isOpenType);
        }

        var count = ReadUInt16(bytes, nameTableOffset + 2);
        var storageOffset = nameTableOffset + ReadUInt16(bytes, nameTableOffset + 4);

        string? family = null;
        string? subfamily = null;

        for (var i = 0; i < count; i++)
        {
            var recordOffset = nameTableOffset + 6 + i * 12;
            var platformId = ReadUInt16(bytes, recordOffset);
            var languageId = ReadUInt16(bytes, recordOffset + 4);
            var nameId = ReadUInt16(bytes, recordOffset + 6);
            var length = ReadUInt16(bytes, recordOffset + 8);
            var stringOffset = ReadUInt16(bytes, recordOffset + 10);

            if (nameId != 1 && nameId != 2)
            {
                continue;
            }

            var isPreferred = platformId == 3 && languageId == 0x0409;
            var text = DecodeNameString(bytes, storageOffset + stringOffset, length, platformId);

            if (nameId == 1 && (family is null || isPreferred))
            {
                family = text;
            }
            else if (nameId == 2 && (subfamily is null || isPreferred))
            {
                subfamily = text;
            }
        }

        family ??= Path.GetFileNameWithoutExtension(filePath);

        var displayName = string.IsNullOrWhiteSpace(subfamily) || string.Equals(subfamily, "Regular", StringComparison.OrdinalIgnoreCase)
            ? family
            : $"{family} {subfamily}";

        return (displayName, isOpenType);
    }

    private static string DecodeNameString(byte[] bytes, int offset, int length, int platformId)
    {
        var encoding = platformId == 1 ? Encoding.Latin1 : Encoding.BigEndianUnicode;
        return encoding.GetString(bytes, offset, length).Trim();
    }

    private static string ReadTag(byte[] bytes, int offset) => Encoding.ASCII.GetString(bytes, offset, 4);
    private static ushort ReadUInt16(byte[] bytes, int offset) => (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
    private static uint ReadUInt32(byte[] bytes, int offset) => (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);

    private const int WM_FONTCHANGE = 0x001D;
    private const int SMTO_ABORTIFHUNG = 0x0002;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    [DllImport("gdi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int AddFontResourceW(string fileName);

    [DllImport("gdi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool RemoveFontResourceW(string fileName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, int flags, int timeout, out IntPtr result);
}
