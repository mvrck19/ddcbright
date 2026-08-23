using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace DdcBright;

public partial class SettingsWindow : FluentWindow
{
    private readonly Settings _settings;
    private bool _suppressEvents;
    private List<(string Id, string Name)>? _cameras;

    public SettingsWindow(Settings settings)
    {
        InitializeComponent();
        _settings = settings;
        ApplicationThemeManager.Apply(this);

        _suppressEvents = true;
        ThemeSelector.SelectedIndex = (int)_settings.Theme;
        _suppressEvents = false;

        LaunchAtStartupToggle.IsChecked = _settings.LaunchAtStartup;

        SegmentedControlHelper.WireExclusive(ModeButtons, i =>
        {
            _settings.AutoBrightnessMode = (AutoBrightnessMode)i;
            _settings.Save();
            ((App)System.Windows.Application.Current).ApplyAutoBrightnessMode();
            RefreshAutoModeUi();
        });

        SegmentedControlHelper.WireExclusive(TransitionButtons, i =>
        {
            _settings.ScheduleTransition = (ScheduleTransitionMode)i;
            _settings.Save();
            RefreshAutoModeUi();
        });

        RefreshAutoModeUi();
    }

    private void LaunchAtStartupToggle_Click(object sender, RoutedEventArgs e)
    {
        _settings.LaunchAtStartup = LaunchAtStartupToggle.IsChecked == true;
        _settings.Save();
        if (_settings.LaunchAtStartup) Autostart.Register(); else Autostart.Unregister();
    }

    private ToggleButton[] ModeButtons => [AutoOffBtn, AutoScheduleBtn, AutoAmbientBtn];
    private ToggleButton[] TransitionButtons => [TransitionInstantBtn, TransitionGradualBtn];

    // SettingsWindow is a long-lived, lazily-reused window (App.ShowSettings)
    // that can stay open while the flyout changes AutoBrightnessMode --
    // refresh on reactivation so the two windows stay in sync.
    private void SettingsWindow_Activated(object sender, EventArgs e) => RefreshAutoModeUi();

    private void RefreshAutoModeUi()
    {
        SegmentedControlHelper.SetSelected(ModeButtons, (int)_settings.AutoBrightnessMode);
        SegmentedControlHelper.SetSelected(TransitionButtons, (int)_settings.ScheduleTransition);

        var isSchedule = _settings.AutoBrightnessMode == AutoBrightnessMode.Schedule;
        var isAmbient = _settings.AutoBrightnessMode == AutoBrightnessMode.Ambient;
        var isOff = _settings.AutoBrightnessMode == AutoBrightnessMode.Off;
        var isGradual = _settings.ScheduleTransition == ScheduleTransitionMode.Gradual;

        TransitionCard.Visibility = isSchedule ? Visibility.Visible : Visibility.Collapsed;
        ScheduleCardsRow.Visibility = isSchedule ? Visibility.Visible : Visibility.Collapsed;
        FadeDurationRow.Visibility = isSchedule && isGradual ? Visibility.Visible : Visibility.Collapsed;
        AmbientCard.Visibility = isAmbient ? Visibility.Visible : Visibility.Collapsed;
        OffModeHelperText.Visibility = isOff ? Visibility.Visible : Visibility.Collapsed;

        if (isAmbient && _cameras is null)
            _ = PopulateCameraListAsync();

        RefreshScheduleFields();
    }

    private async Task PopulateCameraListAsync()
    {
        _cameras = await AmbientLightSensor.GetAvailableCamerasAsync();

        _suppressEvents = true;
        AmbientCameraSelector.Items.Clear();
        AmbientCameraSelector.Items.Add("System default");
        foreach (var camera in _cameras)
            AmbientCameraSelector.Items.Add(camera.Name);

        var selectedIndex = string.IsNullOrEmpty(_settings.AmbientCameraId)
            ? 0
            : _cameras.FindIndex(c => c.Id == _settings.AmbientCameraId) + 1;
        AmbientCameraSelector.SelectedIndex = selectedIndex < 0 ? 0 : selectedIndex;
        _suppressEvents = false;
    }

    private void AmbientCameraSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _cameras is null) return;

        var index = AmbientCameraSelector.SelectedIndex;
        _settings.AmbientCameraId = index <= 0 ? null : _cameras[index - 1].Id;
        _settings.Save();
        AmbientTestResultText.Text = string.Empty;
    }

    private async void AmbientTestButton_Click(object sender, RoutedEventArgs e)
    {
        AmbientTestButton.IsEnabled = false;
        AmbientTestResultText.Text = "Testing…";

        var result = await AmbientLightSensor.CaptureOnceAsync(_settings.AmbientCameraId);
        AmbientTestResultText.Text = result.Message;

        AmbientTestButton.IsEnabled = true;
    }

    private void RefreshScheduleFields()
    {
        DayTimeLabel.Text = _settings.DayTime.ToString("h:mm tt");
        NightTimeLabel.Text = _settings.NightTime.ToString("h:mm tt");
        DayBrightnessLabel.Text = $"{_settings.DayBrightness}%";
        NightBrightnessLabel.Text = $"{_settings.NightBrightness}%";
        TransitionMinutesLabel.Text = $"{_settings.TransitionMinutes} min";
        AutoModeStatusText.Text = AutoModeStatus.GetText(_settings);
    }

    private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;

        _settings.Theme = (ThemePreference)ThemeSelector.SelectedIndex;
        _settings.Save();
        App.ApplyTheme(_settings.Theme);

        // ApplyTheme swaps the global resource dictionaries, so DynamicResource-
        // bound content updates everywhere immediately -- but re-calling
        // ApplicationThemeManager.Apply(window) on an already-initialized
        // window only re-swaps resources again, it doesn't re-trigger the
        // native Mica material, which is what actually left this window
        // with re-themed content sitting on a backdrop tinted for the old
        // theme. WindowBackgroundManager.UpdateBackground is WPF-UI's own
        // explicit "re-apply the backdrop effect" call for that case.
        ApplicationThemeManager.Apply(this);
        WindowBackgroundManager.UpdateBackground(this, ApplicationThemeManager.GetAppTheme(), WindowBackdropType.Mica);
    }

    private void DayTimeDown_Click(object sender, RoutedEventArgs e) => AdjustDayTime(-15);
    private void DayTimeUp_Click(object sender, RoutedEventArgs e) => AdjustDayTime(15);
    private void NightTimeDown_Click(object sender, RoutedEventArgs e) => AdjustNightTime(-15);
    private void NightTimeUp_Click(object sender, RoutedEventArgs e) => AdjustNightTime(15);

    private void DayBrightnessDown_Click(object sender, RoutedEventArgs e) => AdjustDayBrightness(-5);
    private void DayBrightnessUp_Click(object sender, RoutedEventArgs e) => AdjustDayBrightness(5);
    private void NightBrightnessDown_Click(object sender, RoutedEventArgs e) => AdjustNightBrightness(-5);
    private void NightBrightnessUp_Click(object sender, RoutedEventArgs e) => AdjustNightBrightness(5);

    private void DayBrightnessRow_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        AdjustDayBrightness(Math.Sign(e.Delta) * 5);
        e.Handled = true;
    }

    private void NightBrightnessRow_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        AdjustNightBrightness(Math.Sign(e.Delta) * 5);
        e.Handled = true;
    }

    private void TransitionMinutesDown_Click(object sender, RoutedEventArgs e) => AdjustTransitionMinutes(-15);
    private void TransitionMinutesUp_Click(object sender, RoutedEventArgs e) => AdjustTransitionMinutes(15);

    private void AdjustDayTime(int deltaMinutes)
    {
        _settings.DayTime = AdjustTime(_settings.DayTime, deltaMinutes);
        _settings.Save();
        RefreshScheduleFields();
    }

    private void AdjustNightTime(int deltaMinutes)
    {
        _settings.NightTime = AdjustTime(_settings.NightTime, deltaMinutes);
        _settings.Save();
        RefreshScheduleFields();
    }

    private void AdjustDayBrightness(int delta)
    {
        _settings.DayBrightness = Math.Clamp(_settings.DayBrightness + delta, 0, 100);
        _settings.Save();
        RefreshScheduleFields();
    }

    private void AdjustNightBrightness(int delta)
    {
        _settings.NightBrightness = Math.Clamp(_settings.NightBrightness + delta, 0, 100);
        _settings.Save();
        RefreshScheduleFields();
    }

    private void AdjustTransitionMinutes(int delta)
    {
        _settings.TransitionMinutes = Math.Clamp(_settings.TransitionMinutes + delta, 15, 120);
        _settings.Save();
        RefreshScheduleFields();
    }

    private static TimeOnly AdjustTime(TimeOnly time, int deltaMinutes)
    {
        var total = time.Hour * 60 + time.Minute + deltaMinutes;
        total = ((total % 1440) + 1440) % 1440;
        return new TimeOnly(total / 60, total % 60);
    }
}
