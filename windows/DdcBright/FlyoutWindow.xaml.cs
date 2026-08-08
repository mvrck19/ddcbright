using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Brush = System.Windows.Media.Brush;
using TextBlock = System.Windows.Controls.TextBlock;

namespace DdcBright;

public partial class FlyoutWindow : FluentWindow
{
    private List<MonitorHandle> _monitors = [];
    private readonly Settings _settings;

    public FlyoutWindow(Settings settings)
    {
        InitializeComponent();
        _settings = settings;
        ApplicationThemeManager.Apply(this);

        SegmentedControlHelper.WireExclusive(AutoModeButtons, i =>
        {
            _settings.AutoBrightnessMode = (AutoBrightnessMode)i;
            _settings.Save();
            ((App)System.Windows.Application.Current).ApplyAutoBrightnessMode();
            RefreshAutoModeUi();
        });
    }

    private ToggleButton[] AutoModeButtons => [AutoOffBtn, AutoScheduleBtn, AutoAmbientBtn];

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
        RebuildMonitorRows();
        RefreshAutoModeUi();
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
        SegmentedControlHelper.SetSelected(AutoModeButtons, (int)_settings.AutoBrightnessMode);
        AutoStatusText.Text = AutoModeStatus.GetText(_settings);
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
