using System.Runtime.InteropServices;

namespace DdcBright;

/// <summary>
/// Applies a real translucent backdrop to a window's native HWND, mirroring
/// what Twinkle Tray does: the modern documented DWM API on Windows 11
/// 22H2+, falling back to the older undocumented SetWindowCompositionAttribute
/// blur-behind for everything older (back to Windows 10 1803).
/// </summary>
public static class Backdrop
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystembackdropType = 38;
    private const int DwmwcpRound = 2;
    private const int DwmsbtTransientwindow = 3; // backdrop meant for flyouts/context menus

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private enum AccentState
    {
        Disabled = 0,
        EnableAcrylicBlurBehind = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private const int WcaAccentPolicy = 19;

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    /// <summary>Returns true if the modern Windows 11 backdrop applied (caller
    /// can then leave its own background transparent); false means the legacy
    /// blur-behind fallback was used instead, which still needs a tint color.</summary>
    public static bool Apply(IntPtr hwnd, bool dark, System.Windows.Media.Color tint)
    {
        var darkValue = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkValue, sizeof(int));

        var corner = DwmwcpRound;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));

        var backdrop = DwmsbtTransientwindow;
        var hr = DwmSetWindowAttribute(hwnd, DwmwaSystembackdropType, ref backdrop, sizeof(int));
        if (hr == 0)
            return true;

        var gradientColor = (tint.A << 24) | (tint.B << 16) | (tint.G << 8) | tint.R;
        var policy = new AccentPolicy
        {
            AccentState = AccentState.EnableAcrylicBlurBehind,
            AccentFlags = 2,
            GradientColor = gradientColor,
            AnimationId = 0,
        };

        var policyPtr = Marshal.AllocHGlobal(Marshal.SizeOf<AccentPolicy>());
        try
        {
            Marshal.StructureToPtr(policy, policyPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = policyPtr,
                SizeOfData = Marshal.SizeOf<AccentPolicy>(),
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(policyPtr);
        }

        return false;
    }
}
