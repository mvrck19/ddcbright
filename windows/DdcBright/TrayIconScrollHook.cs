using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DdcBright;

/// <summary>
/// Lets the user scroll the mouse wheel over the tray icon to adjust
/// brightness. WinForms' NotifyIcon has no MouseWheel event, and in its
/// default (v0) shell registration Windows doesn't even forward wheel
/// messages to it -- rather than force NOTIFYICON_VERSION_4 and reverse
/// engineer the resulting multiplexed callback message, this installs a
/// low-level mouse hook and checks the cursor against the icon's real
/// screen rect (Shell_NotifyIconGetRect), which needs no version
/// negotiation with NotifyIcon's own message plumbing.
/// </summary>
internal sealed class TrayIconScrollHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEWHEEL = 0x020A;

    // internal (not private): lets DdcBright.Tests/DdcBright.Benchmarks
    // construct these directly to exercise TryGetScrollDirection without
    // installing a real global hook or faking a NotifyIcon.
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseLowLevelHookStruct
    {
        public Point Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint CbSize;
        public IntPtr HWnd;
        public uint Uid;
        public Guid GuidItem;
    }

    private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out Rect iconLocation);

    private readonly IntPtr _iconHwnd;
    private readonly uint _iconId;
    private readonly LowLevelMouseProc _proc;
    private readonly Action<int> _onScroll;
    private IntPtr _hookHandle;

    public TrayIconScrollHook(NotifyIcon icon, Action<int> onScroll)
    {
        _onScroll = onScroll;
        _proc = HookCallback;

        var windowField = typeof(NotifyIcon).GetField("_window", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("NotifyIcon._window field not found.");
        var nativeWindow = (NativeWindow)windowField.GetValue(icon)!;
        _iconHwnd = nativeWindow.Handle;

        var idField = typeof(NotifyIcon).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("NotifyIcon._id field not found.");
        _iconId = (uint)idField.GetValue(icon)!;

        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc, IntPtr.Zero, 0);
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        // This runs on Windows' global low-level input pipeline: an
        // exception escaping a native hook callback is fatal to the whole
        // process, not just this feature, so any bug here must never
        // propagate past this method.
        try
        {
            if (code >= 0 && wParam.ToInt32() == WM_MOUSEWHEEL)
            {
                var data = Marshal.PtrToStructure<MouseLowLevelHookStruct>(lParam);
                if (TryGetIconRect(out var rect) && TryGetScrollDirection(data, rect, out var direction))
                    _onScroll(direction);
            }
        }
        catch
        {
            // Swallow -- see comment above. Losing one scroll notch beats
            // crashing the app.
        }
        return CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    /// <summary>
    /// The pure decision logic of the hook callback, pulled out so it can be
    /// unit-tested and benchmarked without installing a real global hook or
    /// a live NotifyIcon: given a wheel event already known to have occurred
    /// (see HookCallback), decide whether the cursor was over the tray icon
    /// and, if so, which way it scrolled.
    /// </summary>
    internal static bool TryGetScrollDirection(MouseLowLevelHookStruct data, Rect iconRect, out int direction)
    {
        direction = 0;
        if (!IsInside(data.Pt, iconRect)) return false;
        var wheelDelta = unchecked((short)(data.MouseData >> 16));
        direction = Math.Sign(wheelDelta);
        return true;
    }

    private bool TryGetIconRect(out Rect rect)
    {
        var identifier = new NotifyIconIdentifier
        {
            CbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            HWnd = _iconHwnd,
            Uid = _iconId,
            GuidItem = Guid.Empty,
        };
        return Shell_NotifyIconGetRect(ref identifier, out rect) == 0;
    }

    private static bool IsInside(Point pt, Rect r) =>
        pt.X >= r.Left && pt.X <= r.Right && pt.Y >= r.Top && pt.Y <= r.Bottom;

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
