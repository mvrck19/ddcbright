using System.Runtime.InteropServices;

namespace DdcBright;

public record MonitorHandle(IntPtr Handle, string Description);

/// <summary>
/// DDC/CI brightness control via dxva2.dll -- the same Windows Monitor
/// Configuration API the Python app's monitorcontrol dependency wraps.
/// VCP code 0x10 is the MCCS "luminance" (brightness) feature; monitors
/// overwhelmingly report a 0-100 range for it, so (like monitorcontrol)
/// this treats the raw VCP value as a direct percentage rather than
/// rescaling against the reported maximum.
/// </summary>
public static class MonitorControl
{
    private const byte VcpCodeBrightness = 0x10;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PhysicalMonitor
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("dxva2.dll")]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll")]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PhysicalMonitor[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll")]
    private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

    [DllImport("dxva2.dll")]
    private static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte bVCPCode, IntPtr pvct, out uint pdwCurrentValue, out uint pdwMaximumValue);

    [DllImport("dxva2.dll")]
    private static extern bool SetVCPFeature(IntPtr hMonitor, byte bVCPCode, uint dwNewValue);

    public static List<MonitorHandle> GetMonitors()
    {
        var results = new List<MonitorHandle>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var count) || count == 0)
                return true;

            var physicalMonitors = new PhysicalMonitor[count];
            if (GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physicalMonitors))
            {
                foreach (var pm in physicalMonitors)
                    results.Add(new MonitorHandle(pm.hPhysicalMonitor, pm.szPhysicalMonitorDescription));
            }
            return true;
        }, IntPtr.Zero);

        return results;
    }

    public static int GetBrightness(MonitorHandle monitor)
    {
        if (GetVCPFeatureAndVCPFeatureReply(monitor.Handle, VcpCodeBrightness, IntPtr.Zero, out var current, out _))
            return Math.Clamp((int)current, 0, 100);
        return 0;
    }

    public static void SetBrightness(MonitorHandle monitor, int percent)
    {
        SetVCPFeature(monitor.Handle, VcpCodeBrightness, (uint)Math.Clamp(percent, 0, 100));
    }

    public static void ReleaseMonitor(MonitorHandle monitor) => DestroyPhysicalMonitor(monitor.Handle);
}
