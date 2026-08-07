using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;

namespace DdcBright;

/// <summary>
/// Estimates ambient light from a brief, single webcam frame -- not a live
/// preview -- grabbed periodically while Ambient mode is active, matching
/// what Lunar (the macOS reference app) does when there's no dedicated
/// ambient light sensor: use the webcam as a stand-in.
/// </summary>
public class AmbientLightSensor
{
    private const int SampleIntervalSeconds = 30;
    private const int MinBrightness = 10; // never auto-dim to fully black
    private const int ChangeThreshold = 5; // ignore small fluctuations

    private readonly Settings _settings;
    private System.Threading.Timer? _timer;
    private int _lastAppliedBrightness = -1;
    private bool _running;

    public AmbientLightSensor(Settings settings)
    {
        _settings = settings;
    }

    public void Start()
    {
        Stop();
        _lastAppliedBrightness = -1;
        _timer = new System.Threading.Timer(
            _ => _ = TickAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(SampleIntervalSeconds));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private async Task TickAsync()
    {
        if (_running) return; // previous capture still running (camera busy, slow device, etc.)
        _running = true;
        try
        {
            var luma = await CaptureAverageLumaAsync();
            if (luma is not { } value) return;

            var brightness = MapLumaToBrightness(value);
            if (_lastAppliedBrightness >= 0 && Math.Abs(brightness - _lastAppliedBrightness) < ChangeThreshold)
                return;

            _lastAppliedBrightness = brightness;
            foreach (var monitor in MonitorControl.GetMonitors())
                MonitorControl.SetBrightness(monitor, brightness);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or COMException or InvalidOperationException)
        {
            // No camera, permission denied, unsupported device, or busy --
            // best-effort, just try again on the next tick.
            System.Diagnostics.Debug.WriteLine($"[AmbientLightSensor] capture failed: {ex.Message}");
        }
        finally
        {
            _running = false;
        }
    }

    private static async Task<int?> CaptureAverageLumaAsync()
    {
        using var mediaCapture = new MediaCapture();
        await mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
        {
            StreamingCaptureMode = StreamingCaptureMode.Video,
            PhotoCaptureSource = PhotoCaptureSource.VideoPreview,
        });

        var lowLagCapture = await mediaCapture.PrepareLowLagPhotoCaptureAsync(
            ImageEncodingProperties.CreateUncompressed(MediaPixelFormat.Bgra8));
        try
        {
            var capturedPhoto = await lowLagCapture.CaptureAsync();
            using var bitmap = capturedPhoto.Frame.SoftwareBitmap;
            return ComputeAverageLuma(bitmap);
        }
        finally
        {
            await lowLagCapture.FinishAsync();
        }
    }

    private static unsafe int ComputeAverageLuma(SoftwareBitmap bitmap)
    {
        using var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
        using var reference = buffer.CreateReference();
        ((IMemoryBufferByteAccess)reference).GetBuffer(out var data, out var capacity);

        long total = 0;
        long count = 0;
        // Every 8th pixel is plenty for an average -- this only feeds a
        // brightness estimate, not anything that needs per-pixel accuracy.
        for (uint i = 0; i + 4 <= capacity; i += 4 * 8)
        {
            byte b = data[i];
            byte g = data[i + 1];
            byte r = data[i + 2];
            total += (long)(0.299 * r + 0.587 * g + 0.114 * b);
            count++;
        }
        return count == 0 ? 128 : (int)(total / count);
    }

    private static int MapLumaToBrightness(int luma)
    {
        var percent = MinBrightness + (int)(luma / 255.0 * (100 - MinBrightness));
        return Math.Clamp(percent, MinBrightness, 100);
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}
