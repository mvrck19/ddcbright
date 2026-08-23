using System.Windows;

namespace DdcBright;

/// <summary>
/// Positions a transient popup window near the tray icon -- approximated by
/// the mouse cursor, since that's where the user just clicked/scrolled/
/// right-clicked the tray icon, and WinForms NotifyIcon exposes no direct
/// coordinates of its own. Anchored to the bottom of the screen (matching
/// where the taskbar/tray actually is) and clamped to the working area so it
/// never lands off-screen or behind the taskbar.
/// </summary>
internal static class WindowPositioning
{
    public static void NearCursor(Window window)
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var workArea = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;
        var width = window.ActualWidth;
        var height = window.ActualHeight;

        var x = cursor.X - width / 2;
        var y = workArea.Bottom - height - 8;
        x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - width));
        y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - height));

        window.Left = x;
        window.Top = y;
    }
}
