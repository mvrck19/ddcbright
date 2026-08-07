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

    public BrightnessScheduler(Settings settings)
    {
        _settings = settings;
    }

    public void Start()
    {
        Stop();
        _lastAppliedIsDay = null;
        _timer = new System.Threading.Timer(_ => Tick(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Tick()
    {
        var now = TimeOnly.FromDateTime(DateTime.Now);
        var isDay = IsDayPeriod(now, _settings.DayTime, _settings.NightTime);

        if (_lastAppliedIsDay == isDay)
            return;
        _lastAppliedIsDay = isDay;

        var brightness = isDay ? _settings.DayBrightness : _settings.NightBrightness;
        foreach (var monitor in MonitorControl.GetMonitors())
            MonitorControl.SetBrightness(monitor, brightness);
    }

    private static bool IsDayPeriod(TimeOnly now, TimeOnly dayStart, TimeOnly nightStart)
    {
        if (dayStart < nightStart)
            return now >= dayStart && now < nightStart;
        return now >= dayStart || now < nightStart; // schedule wraps past midnight
    }
}
