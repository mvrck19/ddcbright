using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Brush = System.Windows.Media.Brush;
using TextBlock = System.Windows.Controls.TextBlock;

namespace DdcBright;

public partial class FlyoutWindow : FluentWindow
{
    private const int WheelStepPercent = 5;

    private List<MonitorHandle> _monitors = [];
    private readonly Settings _settings;
    private ThemePreference _lastAppliedTheme;

    public FlyoutWindow(Settings settings)
    {
        InitializeComponent();
        _settings = settings;
        ApplicationThemeManager.Apply(this);
        _lastAppliedTheme = _settings.Theme;
    }

    // Used by --render-preview: builds the same content ShowNearCursor()
    // would, without the cursor-position/Show() machinery a headless render
    // doesn't need.
    internal void PrepareForPreview()
    {
        RebuildMonitorRows();
        RefreshAutoModeUi();
    }

    public void ShowNearCursor()
    {
        // FluentWindow's chrome/backdrop setup only finalizes once the HWND
        // exists (on Show()), so a pre-Show Measure/Arrange under-reports
        // the real size. Show hidden, measure the real ActualWidth/Height,
        // reposition, then reveal -- avoids both an undersized-window bug
        // and a flash-then-jump.
        //
        // Re-applying theme here (not just once in the constructor) covers
        // the flyout being shown after the user changed theme from Settings
        // while the flyout was hidden -- its native chrome otherwise never
        // finds out the theme changed. ApplicationThemeManager.Apply(window)
        // only re-swaps resource dictionaries on a second call; it doesn't
        // re-trigger the native Mica material, which is what actually
        // produced dark-on-dark unreadable text after switching to Light --
        // WindowBackgroundManager.UpdateBackground is WPF-UI's own explicit
        // "re-apply the backdrop effect" call for exactly that case. Only
        // doing this when the theme actually changed since the last show,
        // since it's a real native/DWM call, not free to redo every open.
        if (_settings.Theme != _lastAppliedTheme)
        {
            ApplicationThemeManager.Apply(this);
            WindowBackgroundManager.UpdateBackground(this, ApplicationThemeManager.GetAppTheme(), WindowBackdropType.Mica);
            _lastAppliedTheme = _settings.Theme;
        }
        RebuildMonitorRows();
        RefreshAutoModeUi();

        // This window is constructed once and reused for the app's whole
        // lifetime, and SizeToContent can get stuck at a previous (taller)
        // size across repeated Show()/Hide() cycles -- e.g. after showing
        // once with a longer status line or an extra monitor row, it may
        // never shrink back down for a shorter one. Clearing Width/Height
        // back to Auto forces a fresh remeasure against the CURRENT content
        // instead of whatever size the HWND already had cached.
        Width = double.NaN;
        Height = double.NaN;
        Opacity = 0;
        Show();
        UpdateLayout();

        WindowPositioning.NearCursor(this);
        Opacity = 1;
        Activate();
    }

    private void Window_Deactivated(object sender, EventArgs e) => Hide();

    // internal (not private): App's --test-flyout-resize self-check exercises
    // this directly to simulate an in-place header-icon mode toggle.
    internal void RefreshAutoModeUi()
    {
        // Reflect the real setting on the header icons even when it was
        // changed elsewhere (Settings window) -- setting IsChecked here
        // doesn't fire Click, so no re-entrancy risk.
        ScheduleModeToggle.IsChecked = _settings.AutoBrightnessMode == AutoBrightnessMode.Schedule;
        AmbientModeToggle.IsChecked = _settings.AutoBrightnessMode == AutoBrightnessMode.Ambient;

        // Hidden entirely when Off -- an inactive row has nothing worth
        // glancing at, just clutter.
        var isOff = _settings.AutoBrightnessMode == AutoBrightnessMode.Off;
        AutoBrightnessSection.Visibility = isOff ? Visibility.Collapsed : Visibility.Visible;

        if (!isOff)
        {
            AutoStatusText.Text = AutoModeStatus.GetText(_settings);
            AutoModeBadgeText.Text = _settings.AutoBrightnessMode == AutoBrightnessMode.Schedule ? "Schedule" : "Ambient";

            // AccentTextFillColorPrimaryBrush, not SystemAccentColorPrimaryBrush:
            // the latter is meant for fills (buttons, toggles), not calibrated
            // for text-on-neutral-background contrast -- it read as barely
            // readable for the "Settings" link before this fix.
            AutoModeBadgeText.Foreground = (Brush)FindResource("AccentTextFillColorPrimaryBrush");
        }

        // Toggling AutoBrightnessSection's Visibility while the flyout is
        // already open (header icon click, not a fresh ShowNearCursor) hits
        // the same "SizeToContent stuck at a previous taller size" quirk
        // ShowNearCursor works around -- without this, turning a mode back
        // off left a blank gap where the status section used to be, because
        // the HWND never shrank back down even though the content did.
        // IsVisible guards this to only run while actually on screen (not
        // during ShowNearCursor's own pre-Show setup, or --render-preview's
        // headless content, which handle sizing themselves).
        if (IsVisible)
        {
            Width = double.NaN;
            Height = double.NaN;
            InvalidateMeasure();
            InvalidateArrange();
            UpdateLayout();

            var workArea = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top)).WorkingArea;
            Top = workArea.Bottom - ActualHeight - 8;
        }
    }

    private void RebuildMonitorRows()
    {
        MonitorRowsPanel.Children.Clear();
        _monitors = MonitorControl.GetMonitors();

        LinkMonitorsToggle.Visibility = _monitors.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        LinkMonitorsToggle.IsChecked = _settings.SyncMonitors;

        if (_monitors.Count == 0)
        {
            MonitorRowsPanel.Children.Add(new TextBlock
            {
                Text = "No monitors detected",
                FontSize = 13,
                Foreground = (Brush)FindResource("TextFillColorSecondaryBrush"),
                Margin = new Thickness(0, 0, 0, 16),
            });
            return;
        }

        if (_settings.SyncMonitors && _monitors.Count > 1)
        {
            var brightness = MonitorControl.GetBrightness(_monitors[0]) ?? 50;
            MonitorRowsPanel.Children.Add(BuildMonitorRow("All Monitors", brightness, value =>
            {
                // Attempt every monitor regardless of earlier failures (no
                // short-circuiting), but still report if any of them failed.
                var success = true;
                foreach (var monitor in _monitors)
                    success &= MonitorControl.SetBrightness(monitor, value);
                return success;
            }));
            return;
        }

        foreach (var monitor in _monitors)
        {
            var name = string.IsNullOrWhiteSpace(monitor.Description) ? "Monitor" : monitor.Description;
            var brightness = MonitorControl.GetBrightness(monitor) ?? 50;
            MonitorRowsPanel.Children.Add(BuildMonitorRow(name, brightness,
                value => MonitorControl.SetBrightness(monitor, value)));
        }
    }

    private FrameworkElement BuildMonitorRow(string name, int brightness, Func<int, bool> onChanged)
    {
        var secondaryBrush = (Brush)FindResource("TextFillColorSecondaryBrush");

        var header = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        header.Children.Add(new SymbolIcon
        {
            Symbol = SymbolRegular.Desktop24,
            FontSize = 14,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });

        var percentLabel = new TextBlock { Text = $"{brightness}%", FontSize = 13, Foreground = secondaryBrush, VerticalAlignment = VerticalAlignment.Center };

        var warningIcon = new TextBlock
        {
            Text = "⚠",
            FontSize = 12,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = System.Windows.Media.Brushes.OrangeRed,
            Visibility = Visibility.Collapsed,
            ToolTip = "This monitor didn't respond to the brightness change.",
        };

        var sliderRow = new Grid();
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var slider = new Slider
        {
            Minimum = 0, Maximum = 100, Value = brightness,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(slider, 0);
        slider.ValueChanged += (_, e) =>
        {
            var value = (int)e.NewValue;
            percentLabel.Text = $"{value}%";
            warningIcon.Visibility = onChanged(value) ? Visibility.Collapsed : Visibility.Visible;
            var app = (App)System.Windows.Application.Current;
            app.ExitAutoModeIfActive();
            app.UpdateTrayTooltip(value);
            RefreshAutoModeUi();
        };

        var percentPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 0) };
        Grid.SetColumn(percentPanel, 1);
        percentPanel.Children.Add(percentLabel);
        percentPanel.Children.Add(warningIcon);

        sliderRow.Children.Add(slider);
        sliderRow.Children.Add(percentPanel);

        var row = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        row.Children.Add(header);
        row.Children.Add(sliderRow);

        // Scroll to adjust while hovering anywhere over the row, not just
        // the slider thumb itself.
        row.PreviewMouseWheel += (_, e) =>
        {
            slider.Value = Math.Clamp(slider.Value + Math.Sign(e.Delta) * WheelStepPercent, slider.Minimum, slider.Maximum);
            e.Handled = true;
        };

        return row;
    }

    private void LinkMonitorsToggle_Click(object sender, RoutedEventArgs e)
    {
        _settings.SyncMonitors = LinkMonitorsToggle.IsChecked == true;
        _settings.Save();
        RebuildMonitorRows();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
        => ((App)System.Windows.Application.Current).ShowSettings();

    private void ScheduleModeToggle_Click(object sender, RoutedEventArgs e) => SetAutoBrightnessMode(AutoBrightnessMode.Schedule);
    private void AmbientModeToggle_Click(object sender, RoutedEventArgs e) => SetAutoBrightnessMode(AutoBrightnessMode.Ambient);

    // Clicking the already-active mode's icon turns it back off -- these two
    // icons plus "neither pressed" cover all three AutoBrightnessMode values
    // without a dedicated Off icon.
    private void SetAutoBrightnessMode(AutoBrightnessMode mode)
    {
        _settings.AutoBrightnessMode = _settings.AutoBrightnessMode == mode ? AutoBrightnessMode.Off : mode;
        _settings.Save();
        ((App)System.Windows.Application.Current).ApplyAutoBrightnessMode();
        RefreshAutoModeUi();
    }
}
