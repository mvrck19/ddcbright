using Microsoft.Win32;

namespace DdcBright;

public static class Theme
{
    private static readonly System.Windows.Media.Color DefaultAccent =
        System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xd4);

    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or ObjectDisposedException)
        {
            return false;
        }
    }

    public static System.Windows.Media.Color GetAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            // AccentColor is a DWORD packed as 0xAABBGGRR.
            if (key?.GetValue("AccentColor") is int packed)
            {
                byte r = (byte)(packed & 0xFF);
                byte g = (byte)((packed >> 8) & 0xFF);
                byte b = (byte)((packed >> 16) & 0xFF);
                return System.Windows.Media.Color.FromRgb(r, g, b);
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or ObjectDisposedException)
        {
        }
        return DefaultAccent;
    }
}
