namespace DdcBright.Tests;

public class SettingsTests
{
    // The path-parameterized Load(path)/Save(path) overloads are internal,
    // reached via InternalsVisibleTo.

    [Fact]
    public void SaveThenLoad_RoundTripsEveryField()
    {
        var path = Path.GetTempFileName();
        try
        {
            var original = new Settings
            {
                Theme = ThemePreference.Dark,
                AutoBrightnessMode = AutoBrightnessMode.Schedule,
                DayTime = new TimeOnly(6, 30),
                NightTime = new TimeOnly(21, 15),
                DayBrightness = 90,
                NightBrightness = 15,
                AmbientCameraId = "cam-123",
                SyncMonitors = true,
                ScheduleTransition = ScheduleTransitionMode.Gradual,
                TransitionMinutes = 45,
                LaunchAtStartup = false,
            };

            original.Save(path);
            var loaded = Settings.Load(path);

            Assert.Equal(original.Theme, loaded.Theme);
            Assert.Equal(original.AutoBrightnessMode, loaded.AutoBrightnessMode);
            Assert.Equal(original.DayTime, loaded.DayTime);
            Assert.Equal(original.NightTime, loaded.NightTime);
            Assert.Equal(original.DayBrightness, loaded.DayBrightness);
            Assert.Equal(original.NightBrightness, loaded.NightBrightness);
            Assert.Equal(original.AmbientCameraId, loaded.AmbientCameraId);
            Assert.Equal(original.SyncMonitors, loaded.SyncMonitors);
            Assert.Equal(original.ScheduleTransition, loaded.ScheduleTransition);
            Assert.Equal(original.TransitionMinutes, loaded.TransitionMinutes);
            Assert.Equal(original.LaunchAtStartup, loaded.LaunchAtStartup);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ddcbright-settings-test-{Guid.NewGuid()}.json");

        var loaded = Settings.Load(path);

        Assert.Equal(new Settings().AutoBrightnessMode, loaded.AutoBrightnessMode);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileIsCorrupt()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{ not valid json ");

            var loaded = Settings.Load(path);

            Assert.Equal(new Settings().AutoBrightnessMode, loaded.AutoBrightnessMode);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
