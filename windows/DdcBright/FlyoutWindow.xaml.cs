using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace DdcBright;

public partial class FlyoutWindow : FluentWindow
{
    private readonly List<MonitorHandle> _monitors;
    private bool _suppressEvents;

    public FlyoutWindow()
    {
        InitializeComponent();
        ApplicationThemeManager.Apply(this);

        _monitors = MonitorControl.GetMonitors();
        foreach (var m in _monitors)
            MonitorSelector.Items.Add(string.IsNullOrWhiteSpace(m.Description) ? "Monitor" : m.Description);

        if (MonitorSelector.Items.Count > 0)
            MonitorSelector.SelectedIndex = 0;
        else
            BrightnessLabel.Text = "No monitors detected";
    }

    public void ShowNearCursor()
    {
        // FluentWindow's chrome/backdrop setup only finalizes once the HWND
        // exists (on Show()), so a pre-Show Measure/Arrange under-reports
        // the real size. Show hidden, measure the real ActualWidth/Height,
        // reposition, then reveal -- avoids both an undersized-window bug
        // and a flash-then-jump.
        RefreshBrightness();
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
