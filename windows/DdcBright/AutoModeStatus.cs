namespace DdcBright;

/// <summary>
/// Computes the human-readable auto-brightness status line shown under the
/// mode selector in both FlyoutWindow and SettingsWindow, so the copy lives
/// in exactly one place.
/// </summary>
internal static class AutoModeStatus
{
    public static string GetText(Settings settings)
    {
        return settings.AutoBrightnessMode switch
        {
            AutoBrightnessMode.Off => "Adjust brightness manually.",
            AutoBrightnessMode.Schedule => settings.ScheduleTransition == ScheduleTransitionMode.Gradual
                ? $"Fades to {settings.NightBrightness}% over {settings.TransitionMinutes} min starting at {Format(settings.NightTime)}, " +
                  $"and to {settings.DayBrightness}% over {settings.TransitionMinutes} min starting at {Format(settings.DayTime)}."
                : $"Dims to {settings.NightBrightness}% at {Format(settings.NightTime)}, brightens to {settings.DayBrightness}% at {Format(settings.DayTime)}.",
            AutoBrightnessMode.Ambient =>
                $"Samples your webcam every {AmbientLightSensor.SampleIntervalSeconds} seconds to estimate room brightness. " +
                $"Never dims below {AmbientLightSensor.MinBrightness}%.",
            _ => string.Empty,
        };
    }

    private static string Format(TimeOnly time) => time.ToString("h:mm tt");
}
