using System.IO;
using System.Text.Json;

namespace DdcBright;

public enum ThemePreference { System, Light, Dark }

public enum AutoBrightnessMode { Off, Schedule, Ambient }

public enum ScheduleTransitionMode { Instant, Gradual }

public class Settings
{
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public AutoBrightnessMode AutoBrightnessMode { get; set; } = AutoBrightnessMode.Off;
    public TimeOnly DayTime { get; set; } = new(8, 0);
    public TimeOnly NightTime { get; set; } = new(20, 0);
    public int DayBrightness { get; set; } = 80;
    public int NightBrightness { get; set; } = 30;

    public bool SyncMonitors { get; set; } = false;
    public ScheduleTransitionMode ScheduleTransition { get; set; } = ScheduleTransitionMode.Instant;
    public int TransitionMinutes { get; set; } = 30;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ddcbright",
        "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                if (settings is not null)
                    return settings;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
