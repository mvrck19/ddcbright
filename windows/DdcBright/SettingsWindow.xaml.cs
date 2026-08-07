using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace DdcBright;

public partial class SettingsWindow : FluentWindow
{
    private readonly Settings _settings;
    private bool _suppressEvents;

    public SettingsWindow(Settings settings)
    {
        InitializeComponent();
        _settings = settings;
        ApplicationThemeManager.Apply(this);

        _suppressEvents = true;
        DayHourBox.Value = _settings.DayTime.Hour;
        DayMinuteBox.Value = _settings.DayTime.Minute;
        NightHourBox.Value = _settings.NightTime.Hour;
        NightMinuteBox.Value = _settings.NightTime.Minute;
        DayBrightnessBox.Value = _settings.DayBrightness;
        NightBrightnessBox.Value = _settings.NightBrightness;
        _suppressEvents = false;
    }

    private void DayTimeBox_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
    {
        if (_suppressEvents) return;
        _settings.DayTime = new TimeOnly((int)(DayHourBox.Value ?? 0), (int)(DayMinuteBox.Value ?? 0));
        _settings.Save();
    }

    private void NightTimeBox_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
    {
        if (_suppressEvents) return;
        _settings.NightTime = new TimeOnly((int)(NightHourBox.Value ?? 0), (int)(NightMinuteBox.Value ?? 0));
        _settings.Save();
    }

    private void DayBrightnessBox_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
    {
        if (_suppressEvents || e.NewValue is not { } value) return;
        _settings.DayBrightness = (int)value;
        _settings.Save();
    }

    private void NightBrightnessBox_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
    {
        if (_suppressEvents || e.NewValue is not { } value) return;
        _settings.NightBrightness = (int)value;
        _settings.Save();
    }
}
