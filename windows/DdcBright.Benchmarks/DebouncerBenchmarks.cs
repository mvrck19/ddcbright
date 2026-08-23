using BenchmarkDotNet.Attributes;

namespace DdcBright.Benchmarks;

/// <summary>
/// Models a fast mouse-wheel scroll burst over the tray icon: each notch
/// calls Trigger(), which cancels and reschedules the pending flush.
/// </summary>
[MemoryDiagnoser]
public class DebouncerBenchmarks
{
    private Debouncer _debouncer = null!;

    [IterationSetup]
    public void Setup() => _debouncer = new Debouncer(TimeSpan.FromMilliseconds(80));

    [Benchmark(Description = "10-notch scroll burst (Trigger() calls only, no flush)")]
    public void TriggerBurst()
    {
        for (var i = 0; i < 10; i++)
            _debouncer.Trigger(static () => { });
    }
}
