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

    public string? AmbientCameraId { get; set; } // null = system default camera

    public bool SyncMonitors { get; set; } = false;
    public bool LaunchAtStartup { get; set; } = true;
    public ScheduleTransitionMode ScheduleTransition { get; set; } = ScheduleTransitionMode.Instant;
    public int TransitionMinutes { get; set; } = 30;

    // DDCBRIGHT_SETTINGS_PATH lets DdcBright.UiTests point Save()/Load() at
    // an isolated file -- without it, every Settings instance (including a
    // throwaway one built for --ui-test-settings) writes to the same real
    // path, which corrupted a real settings.json the first time this was
    // tried by hand.
    private static readonly string FilePath = Environment.GetEnvironmentVariable("DDCBRIGHT_SETTINGS_PATH")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ddcbright",
            "settings.json");

    public static Settings Load() => Load(FilePath);

    // Path-parameterized so unit tests can exercise load/save without going
    // through the static, env-var-resolved-once FilePath -- DdcBright.Tests
    // runs many tests in one process, unlike DdcBright.UiTests which
    // launches a separate ddcbright.exe with the env var set before launch.
    internal static Settings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
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

    public void Save() => Save(FilePath);

    internal void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
