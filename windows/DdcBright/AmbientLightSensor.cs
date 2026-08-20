using System.IO;
using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using WinRT;

namespace DdcBright;

/// <summary>
/// Outcome of a single ambient-light capture attempt, used both by the
/// periodic sampling in <see cref="AmbientLightSensor"/> and by on-demand
/// callers (the Settings "Test now" button, the --test-ambient-light CLI
/// flag) that want to check whether the webcam is readable at all.
/// </summary>
public record AmbientCaptureResult(bool Success, int? Luma, int? Brightness, string Message);

/// <summary>
/// Estimates ambient light from a brief, single webcam frame -- not a live
/// preview -- grabbed periodically while Ambient mode is active, matching
/// what Lunar (the macOS reference app) does when there's no dedicated
/// ambient light sensor: use the webcam as a stand-in.
/// </summary>
public class AmbientLightSensor
{
    internal const int SampleIntervalSeconds = 30;
    internal const int MinBrightness = 10; // never auto-dim to fully black
    private const int ChangeThreshold = 5; // ignore small fluctuations
    private const double SmoothingAlpha = 0.3; // EMA weight for each new sample
    private const long MaxLogBytes = 512 * 1024;

    // Some webcam drivers never complete (or fail) MediaCapture's WinRT
    // calls at all -- observed firsthand: InitializeAsync just hangs
    // forever on an old/unsupported device, with no exception, ever, and
    // no response to a CancellationToken either (the native call is
    // blocked, not cooperatively checking for cancellation). Racing it
    // against Task.Delay is the only way to stop *waiting* on it -- the
    // abandoned inner task may itself never finish, which leaks one
    // thread-pool thread on a truly wedged driver, but that beats the
    // whole sensor (and every future tick) freezing forever.
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(8);

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ddcbright",
        "ambient.log");

    private readonly Settings _settings;
    private System.Threading.Timer? _timer;
    private int _lastAppliedBrightness = -1;
    private double? _smoothedLuma;
    private bool _running;

    public AmbientLightSensor(Settings settings)
    {
        _settings = settings;
    }

    public void Start()
    {
        Stop();
        _lastAppliedBrightness = -1;
        _smoothedLuma = null;
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
            var result = await CaptureOnceAsync(_settings.AmbientCameraId, "tick");
            if (!result.Success || result.Luma is not { } luma)
                return;

            // Smooth across ticks (EMA) rather than reacting to a single
            // noisy frame -- the dead-band below then avoids redundant DDC
            // writes once the smoothed value has settled.
            _smoothedLuma = _smoothedLuma is { } prev ? prev * (1 - SmoothingAlpha) + luma * SmoothingAlpha : luma;
            var brightness = MapLumaToBrightness((int)Math.Round(_smoothedLuma.Value));

            if (_lastAppliedBrightness >= 0 && Math.Abs(brightness - _lastAppliedBrightness) < ChangeThreshold)
            {
                Log($"tick  smoothed={_smoothedLuma:F0} brightness={brightness}% SKIPPED (within {ChangeThreshold}% of last applied {_lastAppliedBrightness}%)");
                return;
            }

            _lastAppliedBrightness = brightness;
            foreach (var monitor in MonitorControl.GetMonitors())
                MonitorControl.SetBrightness(monitor, brightness);
            Log($"tick  smoothed={_smoothedLuma:F0} brightness={brightness}% APPLIED");
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>
    /// Does one capture + luma + brightness-mapping pass and reports exactly
    /// what happened, instead of throwing -- this is the single seam shared
    /// by periodic sampling, the Settings "Test now" button, and the
    /// --test-ambient-light CLI flag. Every outcome (including failures) is
    /// appended to ambient.log so camera problems are visible even in a
    /// Release build with no UI open.
    /// </summary>
    public static async Task<AmbientCaptureResult> CaptureOnceAsync(string? cameraId, string context = "test")
    {
        var captureTask = CaptureOnceInnerAsync(cameraId);
        var winner = await Task.WhenAny(captureTask, Task.Delay(CaptureTimeout));

        AmbientCaptureResult result;
        if (winner == captureTask)
        {
            result = await captureTask;
        }
        else
        {
            result = new AmbientCaptureResult(false, null, null,
                $"Capture timed out after {CaptureTimeout.TotalSeconds:F0}s -- the camera driver isn't responding.");
            _ = captureTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        Log(result.Success
            ? $"{context}  luma={result.Luma} brightness={result.Brightness}%"
            : $"{context}  FAILED: {result.Message}");
        return result;
    }

    private static async Task<AmbientCaptureResult> CaptureOnceInnerAsync(string? cameraId)
    {
        try
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            if (devices.Count == 0)
                return new AmbientCaptureResult(false, null, null, "No camera detected by Windows.");

            using var mediaCapture = new MediaCapture();
            var initSettings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                PhotoCaptureSource = PhotoCaptureSource.VideoPreview,
            };
            if (!string.IsNullOrEmpty(cameraId))
                initSettings.VideoDeviceId = cameraId;

            await mediaCapture.InitializeAsync(initSettings);

            var lowLagCapture = await mediaCapture.PrepareLowLagPhotoCaptureAsync(
                ImageEncodingProperties.CreateUncompressed(MediaPixelFormat.Bgra8));
            try
            {
                var capturedPhoto = await lowLagCapture.CaptureAsync();
                using var bitmap = capturedPhoto.Frame.SoftwareBitmap;
                var luma = ComputeAverageLuma(bitmap);
                var brightness = MapLumaToBrightness(luma);
                return new AmbientCaptureResult(true, luma, brightness, $"Camera OK — measured brightness ~{brightness}%.");
            }
            finally
            {
                await lowLagCapture.FinishAsync();
            }
        }
        catch (UnauthorizedAccessException)
        {
            return new AmbientCaptureResult(false, null, null,
                "Camera access is turned off in Windows Settings > Privacy > Camera.");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // Unsupported device/format, camera busy, or another driver-level
            // failure -- surface the message rather than guessing further.
            return new AmbientCaptureResult(false, null, null, $"Capture failed: {ex.Message}");
        }
    }

    /// <summary>Cameras Windows currently exposes as video capture devices, for the Settings picker.</summary>
    public static async Task<List<(string Id, string Name)>> GetAvailableCamerasAsync()
    {
        var enumerateTask = DeviceInformation.FindAllAsync(DeviceClass.VideoCapture).AsTask();
        var winner = await Task.WhenAny(enumerateTask, Task.Delay(CaptureTimeout));
        if (winner != enumerateTask)
        {
            _ = enumerateTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            return [];
        }

        var devices = await enumerateTask;
        return devices.Select(d => (d.Id, d.Name)).ToList();
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogBytes)
                File.Delete(LogPath); // ponytail: simple restart-on-overflow, no rotation
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static unsafe int ComputeAverageLuma(SoftwareBitmap bitmap)
    {
        using var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
        using var reference = buffer.CreateReference();
        // A plain C# `(IMemoryBufferByteAccess)reference` cast throws
        // InvalidCastException under CsWinRT (net8.0-windows's WinRT
        // projection) -- the RCW is an IInspectable wrapper, not a classic
        // COM object, so it needs CsWinRT's own QueryInterface path via
        // WinRT.CastExtensions.As<T>() instead of a runtime-cast.
        reference.As<IMemoryBufferByteAccess>().GetBuffer(out var data, out var capacity);

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
