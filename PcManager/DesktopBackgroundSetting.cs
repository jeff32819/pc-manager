using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PcManager;

public static class DesktopBackgroundSetting
{
    public static void SetSolidColor(string hexColor) => SetSolidColor(ParseHexColor(hexColor));

    public static void SetSolidColor(KnownColor knownColor) => SetSolidColor(Color.FromKnownColor(knownColor));

    public static void SetSolidColor(Color color)
    {
        var wallpaper = CreateDesktopWallpaper();

        try
        {
            // Clears any picture wallpaper first - SetBackgroundColor alone only controls the
            // color shown behind/around a picture, not a replacement for one.
            wallpaper.SetWallpaper(null, string.Empty);
            wallpaper.SetBackgroundColor(ColorToColorRef(color));
        }
        finally
        {
            Marshal.ReleaseComObject(wallpaper);
        }
    }

    public static Color GetSolidColor()
    {
        var wallpaper = CreateDesktopWallpaper();

        try
        {
            return ColorRefToColor(wallpaper.GetBackgroundColor());
        }
        finally
        {
            Marshal.ReleaseComObject(wallpaper);
        }
    }

    private static Color ParseHexColor(string hexColor)
    {
        var hex = hexColor.TrimStart('#');

        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            throw new FormatException($"'{hexColor}' is not a valid hex color. Expected \"#RRGGBB\", \"RRGGBB\", or the 3-digit shorthand \"#RGB\".");
        }

        var r = (byte)((rgb >> 16) & 0xFF);
        var g = (byte)((rgb >> 8) & 0xFF);
        var b = (byte)(rgb & 0xFF);

        return Color.FromArgb(r, g, b);
    }

    // COLORREF is 0x00BBGGRR - byte order reversed from RGB.
    private static uint ColorToColorRef(Color color) => (uint)(color.R | (color.G << 8) | (color.B << 16));

    private static Color ColorRefToColor(uint colorRef)
    {
        var r = (byte)(colorRef & 0xFF);
        var g = (byte)((colorRef >> 8) & 0xFF);
        var b = (byte)((colorRef >> 16) & 0xFF);

        return Color.FromArgb(r, g, b);
    }

    private static IDesktopWallpaper CreateDesktopWallpaper()
    {
        var type = Type.GetTypeFromCLSID(ClsidDesktopWallpaper)
            ?? throw new InvalidOperationException("Could not resolve the DesktopWallpaper COM class.");

        return (IDesktopWallpaper)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Could not create the DesktopWallpaper COM object."));
    }

    private static readonly Guid ClsidDesktopWallpaper = new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD");

    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);

        uint GetMonitorDevicePathCount();

        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId, out RECT displayRect);

        void SetBackgroundColor(uint color);

        uint GetBackgroundColor();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
