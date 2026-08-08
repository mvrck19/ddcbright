using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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
        // finds out the theme changed. Only calling it when the theme
        // actually changed since the last show, rather than unconditionally
        // on every open, since ApplicationThemeManager.Apply(window) touches
        // native chrome/backdrop setup that isn't free to redo.
        if (_settings.Theme != _lastAppliedTheme)
        {
            ApplicationThemeManager.Apply(this);
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

        var cursor = System.Windows.Forms.Cursor.Position;
        var workArea = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;
        var width = ActualWidth;
        var height = ActualHeight;

        var x = cursor.X - width / 2;
        var y = workArea.Bottom - height - 8;
        x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - width));
        y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - height));

        Left = x;
        Top = y;
        Opacity = 1;
        Activate();
    }

    private void Window_Deactivated(object sender, EventArgs e) => Hide();

    private void RefreshAutoModeUi()
    {
        AutoStatusText.Text = AutoModeStatus.GetText(_settings);

        AutoModeBadgeText.Text = _settings.AutoBrightnessMode switch
        {
            AutoBrightnessMode.Schedule => "Schedule",
            AutoBrightnessMode.Ambient => "Ambient",
            _ => "Off",
        };

        // Plain colored text, no custom background -- reuses the same two
        // brushes already relied on elsewhere in this window (secondary-gray
        // body text, and the same accent brush the "Settings" link uses)
        // rather than a one-off alpha-blended pill that isn't proven legible
        // across both themes and every possible accent color.
        AutoModeBadgeText.Foreground = _settings.AutoBrightnessMode == AutoBrightnessMode.Off
            ? (Brush)FindResource("TextFillColorSecondaryBrush")
            : (Brush)FindResource("SystemAccentColorPrimaryBrush");
    }

    private void RebuildMonitorRows()
    {
        MonitorRowsPanel.Children.Clear();
        _monitors = MonitorControl.GetMonitors();

        LinkMonitorsRow.Visibility = _monitors.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
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
            var brightness = MonitorControl.GetBrightness(_monitors[0]);
            MonitorRowsPanel.Children.Add(BuildMonitorRow("All Monitors", brightness, value =>
            {
                foreach (var monitor in _monitors)
                    MonitorControl.SetBrightness(monitor, value);
            }));
            return;
        }

        foreach (var monitor in _monitors)
        {
            var name = string.IsNullOrWhiteSpace(monitor.Description) ? "Monitor" : monitor.Description;
            var brightness = MonitorControl.GetBrightness(monitor);
            MonitorRowsPanel.Children.Add(BuildMonitorRow(name, brightness,
                value => MonitorControl.SetBrightness(monitor, value)));
        }
    }

    private FrameworkElement BuildMonitorRow(string name, int brightness, Action<int> onChanged)
    {
        var secondaryBrush = (Brush)FindResource("TextFillColorSecondaryBrush");
        var dotBrush = (Brush)FindResource("TextFillColorPrimaryBrush");

        var header = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameLabel = new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(nameLabel, 0);

        var percentLabel = new TextBlock { Text = $"{brightness}%", FontSize = 13, Foreground = secondaryBrush };
        Grid.SetColumn(percentLabel, 1);

        header.Children.Add(nameLabel);
        header.Children.Add(percentLabel);

        var sliderRow = new Grid();
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dimDot = new Ellipse
        {
            Width = 7, Height = 7, Fill = dotBrush, Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(dimDot, 0);

        var slider = new Slider
        {
            Minimum = 0, Maximum = 100, Value = brightness,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(slider, 1);
        slider.ValueChanged += (_, e) =>
        {
            var value = (int)e.NewValue;
            percentLabel.Text = $"{value}%";
            onChanged(value);
        };

        var brightDot = new Ellipse
        {
            Width = 14, Height = 14, Fill = dotBrush,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0),
        };
        Grid.SetColumn(brightDot, 2);

        sliderRow.Children.Add(dimDot);
        sliderRow.Children.Add(slider);
        sliderRow.Children.Add(brightDot);

        var row = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
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

    private void SettingsLink_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => ((App)System.Windows.Application.Current).ShowSettings();
}
