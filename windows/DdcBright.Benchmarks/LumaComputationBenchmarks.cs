using System.Runtime.InteropServices.WindowsRuntime;
using BenchmarkDotNet.Attributes;
using Windows.Graphics.Imaging;

namespace DdcBright.Benchmarks;

/// <summary>
/// AmbientLightSensor's per-tick webcam frame processing. Allocation
/// tracking matters here specifically: this runs on a background timer
/// every 30s for as long as Ambient mode is active, in a long-lived tray
/// process, so steady GC pressure is a real concern, not noise.
/// </summary>
[MemoryDiagnoser]
public class LumaComputationBenchmarks
{
    // A typical low-res webcam preview frame size -- large enough to be
    // representative, small enough to keep the benchmark fast to run.
    private const int Width = 640;
    private const int Height = 480;

    private SoftwareBitmap _frame = null!;

    [GlobalSetup]
    public void Setup()
    {
        var pixels = new byte[Width * Height * 4];
        new Random(42).NextBytes(pixels);
        _frame = SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8, Width, Height);
    }

    [GlobalCleanup]
    public void Cleanup() => _frame.Dispose();

    [Benchmark]
    public int ComputeAverageLuma() => AmbientLightSensor.ComputeAverageLuma(_frame);
}
