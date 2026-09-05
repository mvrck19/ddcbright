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

    // Last brightness observed per monitor (keyed by description -- handles
    // are re-issued on every GetMonitors() call, descriptions aren't).
    // Lets callers like the flyout render instantly from a known-good value
    // instead of blocking the UI thread on a fresh DDC/CI read (an I2C round
    // trip, tens to hundreds of ms) every time they open.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> LastKnown = new();

    public static bool TryGetLastKnownBrightness(string description, out int percent) =>
        LastKnown.TryGetValue(description, out percent);

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

    /// <summary>Returns null if the monitor didn't respond -- distinct from a
    /// genuine 0% reading, which a plain int couldn't represent.</summary>
    public static int? GetBrightness(MonitorHandle monitor)
    {
        DdcBrightEventSource.Log.GetBrightnessStart(monitor.Handle.ToInt64());
        int? result = null;
        try
        {
            if (GetVCPFeatureAndVCPFeatureReply(monitor.Handle, VcpCodeBrightness, IntPtr.Zero, out var current, out _))
            {
                result = ClampPercent((int)current);
                LastKnown[monitor.Description] = result.Value;
            }
            else
                DdcBrightEventSource.Log.GetBrightnessFailed(monitor.Handle.ToInt64());
            return result;
        }
        finally
        {
            // -1 sentinel keeps this event's on-disk int arg unchanged for a
            // failed read; GetBrightnessFailed above is what actually flags it.
            DdcBrightEventSource.Log.GetBrightnessStop(result ?? -1);
        }
    }

    /// <summary>Returns whether the monitor actually accepted the write.</summary>
    public static bool SetBrightness(MonitorHandle monitor, int percent)
    {
        var clamped = ClampPercent(percent);
        DdcBrightEventSource.Log.SetBrightnessStart(monitor.Handle.ToInt64(), clamped);
        try
        {
            var success = SetVCPFeature(monitor.Handle, VcpCodeBrightness, (uint)clamped);
            if (success)
                LastKnown[monitor.Description] = clamped;
            else
                DdcBrightEventSource.Log.SetBrightnessFailed(monitor.Handle.ToInt64());
            return success;
        }
        finally
        {
            DdcBrightEventSource.Log.SetBrightnessStop();
        }
    }

    public static void ReleaseMonitor(MonitorHandle monitor) => DestroyPhysicalMonitor(monitor.Handle);

    internal static int ClampPercent(int percent) => Math.Clamp(percent, 0, 100);
}
