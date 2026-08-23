namespace DdcBright;

/// <summary>
/// Applies Day/Night brightness from Settings on a schedule. Reads the
/// current values from Settings on every tick, so edits from the UI take
/// effect without needing to restart the scheduler.
/// </summary>
public class BrightnessScheduler
{
    private readonly Settings _settings;
    private System.Threading.Timer? _timer;
    private bool? _lastAppliedIsDay;

    // Gradual-transition fade state. Null _fadeStartUtc means no fade in
    // progress. A fade is only ever started on a day/night transition edge,
    // and always reaches exactly _fadeTargetBrightness at _fadeEndUtc.
    private DateTime? _fadeStartUtc;
    private DateTime? _fadeEndUtc;
    private int _fadeStartBrightness;
    private int _fadeTargetBrightness;

    public BrightnessScheduler(Settings settings)
    {
        _settings = settings;
    }

    public void Start()
    {
        Stop();
        _lastAppliedIsDay = null;
        _fadeStartUtc = null;
        _fadeEndUtc = null;
        _timer = new System.Threading.Timer(_ => Tick(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Tick()
    {
        DdcBrightEventSource.Log.SchedulerTickStart();
        try
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);
            var isDay = IsDayPeriod(now, _settings.DayTime, _settings.NightTime);

            if (_lastAppliedIsDay != isDay)
            {
                _lastAppliedIsDay = isDay;
                var target = isDay ? _settings.DayBrightness : _settings.NightBrightness;

                if (_settings.ScheduleTransition == ScheduleTransitionMode.Gradual)
                    StartFade(target);
                else
                {
                    // Instant: unchanged existing behavior -- apply immediately,
                    // and drop any in-progress fade left over from a prior
                    // Gradual-mode transition.
                    _fadeStartUtc = null;
                    _fadeEndUtc = null;
                    ApplyToAllMonitors(target);
                }
            }

            AdvanceFade();
        }
        finally
        {
            DdcBrightEventSource.Log.SchedulerTickStop();
        }
    }

    private void StartFade(int target)
    {
        var monitors = MonitorControl.GetMonitors();
        var current = monitors.Count > 0 ? MonitorControl.GetBrightness(monitors[0]) : target;

        _fadeStartUtc = DateTime.UtcNow;
        _fadeEndUtc = _fadeStartUtc.Value.AddMinutes(_settings.TransitionMinutes);
        _fadeStartBrightness = current;
        _fadeTargetBrightness = target;
    }

    private void AdvanceFade()
    {
        if (_fadeStartUtc is not { } start || _fadeEndUtc is not { } end)
            return;

        var now = DateTime.UtcNow;
        if (now >= end)
        {
            ApplyToAllMonitors(_fadeTargetBrightness);
            _fadeStartUtc = null;
            _fadeEndUtc = null;
            return;
        }

        var t = (now - start).TotalSeconds / (end - start).TotalSeconds;
        ApplyToAllMonitors(InterpolateBrightness(_fadeStartBrightness, _fadeTargetBrightness, t));
    }

    /// <summary>Pure fade math, pulled out of AdvanceFade so it's testable/benchmarkable without a real timer or wall clock.</summary>
    internal static int InterpolateBrightness(int start, int target, double t) =>
        (int)Math.Round(start + (target - start) * t);

    private static void ApplyToAllMonitors(int brightness)
    {
        foreach (var monitor in MonitorControl.GetMonitors())
            MonitorControl.SetBrightness(monitor, brightness);
    }

    internal static bool IsDayPeriod(TimeOnly now, TimeOnly dayStart, TimeOnly nightStart)
    {
        if (dayStart < nightStart)
            return now >= dayStart && now < nightStart;
        return now >= dayStart || now < nightStart; // schedule wraps past midnight
    }
}
