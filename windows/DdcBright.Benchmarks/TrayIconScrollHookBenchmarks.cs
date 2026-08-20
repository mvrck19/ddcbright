using BenchmarkDotNet.Attributes;

namespace DdcBright.Benchmarks;

/// <summary>
/// Headline benchmark: TryGetScrollDirection is TrayIconScrollHook's
/// callback decision logic, which runs synchronously on Windows' global
/// low-level mouse input pipeline (see App.xaml.cs / TrayIconScrollHook.cs)
/// -- its latency is a desktop-wide concern, not just this app's.
/// </summary>
[MemoryDiagnoser]
public class TrayIconScrollHookBenchmarks
{
    private static readonly TrayIconScrollHook.Rect IconRect = new() { Left = 100, Top = 100, Right = 116, Bottom = 116 };

    private TrayIconScrollHook.MouseLowLevelHookStruct _overIcon;
    private TrayIconScrollHook.MouseLowLevelHookStruct _awayFromIcon;

    [GlobalSetup]
    public void Setup()
    {
        _overIcon = new TrayIconScrollHook.MouseLowLevelHookStruct
        {
            Pt = new TrayIconScrollHook.Point { X = 108, Y = 108 },
            MouseData = unchecked((uint)(120 << 16)),
        };
        _awayFromIcon = new TrayIconScrollHook.MouseLowLevelHookStruct
        {
            Pt = new TrayIconScrollHook.Point { X = 800, Y = 800 },
            MouseData = unchecked((uint)(120 << 16)),
        };
    }

    [Benchmark(Description = "Cursor over the tray icon (scroll accepted)")]
    public bool OverIcon() => TrayIconScrollHook.TryGetScrollDirection(_overIcon, IconRect, out _);

    [Benchmark(Description = "Cursor elsewhere (scroll ignored)")]
    public bool AwayFromIcon() => TrayIconScrollHook.TryGetScrollDirection(_awayFromIcon, IconRect, out _);
}
