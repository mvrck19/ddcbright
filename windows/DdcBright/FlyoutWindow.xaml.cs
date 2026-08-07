using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Size = System.Windows.Size;
using Brushes = System.Windows.Media.Brushes;

namespace DdcBright;

public partial class FlyoutWindow : Window
{
    private readonly List<MonitorHandle> _monitors;
    private bool _suppressEvents;

    public FlyoutWindow()
    {
        InitializeComponent();

        _monitors = MonitorControl.GetMonitors();
        foreach (var m in _monitors)
            MonitorSelector.Items.Add(string.IsNullOrWhiteSpace(m.Description) ? "Monitor" : m.Description);

        if (MonitorSelector.Items.Count > 0)
            MonitorSelector.SelectedIndex = 0;
        else
            BrightnessLabel.Text = "No monitors detected";
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var dark = Theme.IsDarkMode();
        var accent = Theme.GetAccentColor();
        var tint = dark ? Color.FromArgb(200, 32, 32, 32) : Color.FromArgb(200, 243, 243, 243);
        var translucent = Backdrop.Apply(hwnd, dark, tint);

        RootBorder.Background = translucent
            ? Brushes.Transparent
            : new SolidColorBrush(dark ? Color.FromRgb(43, 43, 43) : Color.FromRgb(243, 243, 243));

        BrightnessLabel.Foreground = new SolidColorBrush(dark ? Colors.White : Colors.Black);
        BrightnessSlider.Foreground = new SolidColorBrush(accent);
    }

    public void ShowNearCursor()
    {
        // Measure/arrange before Show() so the window can be positioned
        // correctly on first paint instead of flickering at (0,0) and
        // jumping to its real spot.
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(DesiredSize));

        var cursor = System.Windows.Forms.Cursor.Position;
        var workArea = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;
        var width = DesiredSize.Width;
        var height = DesiredSize.Height;

        var x = cursor.X - width / 2;
        var y = workArea.Bottom - height - 8;
        x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - width));
        y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - height));

        Left = x;
        Top = y;

        RefreshBrightness();
        Show();
        Activate();
    }

    private void Window_Deactivated(object sender, EventArgs e) => Hide();

    private void MonitorSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => RefreshBrightness();

    private void RefreshBrightness()
    {
        if (MonitorSelector.SelectedIndex < 0 || MonitorSelector.SelectedIndex >= _monitors.Count)
            return;

        var brightness = MonitorControl.GetBrightness(_monitors[MonitorSelector.SelectedIndex]);

        _suppressEvents = true;
        BrightnessSlider.Value = brightness;
        _suppressEvents = false;
        BrightnessLabel.Text = $"Current Brightness: {brightness}%";
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressEvents) return;
        if (MonitorSelector.SelectedIndex < 0 || MonitorSelector.SelectedIndex >= _monitors.Count)
            return;

        var value = (int)BrightnessSlider.Value;
        MonitorControl.SetBrightness(_monitors[MonitorSelector.SelectedIndex], value);
        BrightnessLabel.Text = $"Current Brightness: {value}%";
    }
}
