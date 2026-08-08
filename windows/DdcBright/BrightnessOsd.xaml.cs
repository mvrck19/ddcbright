using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace DdcBright;

/// <summary>
/// A small transient toast showing the current brightness percentage while
/// scrolling the tray icon -- mirrors Windows' own volume/brightness OSD.
/// Never takes focus (ShowActivated/Focusable both false) so scrolling the
/// tray icon doesn't interrupt whatever window actually has focus.
/// </summary>
public partial class BrightnessOsd : FluentWindow
{
    private readonly DispatcherTimer _hideTimer;
    private ApplicationTheme _lastAppliedTheme;

    public BrightnessOsd()
    {
        InitializeComponent();
        ApplicationThemeManager.Apply(this);
        _lastAppliedTheme = ApplicationThemeManager.GetAppTheme();

        // Same show-hidden-then-measure workaround FlyoutWindow uses:
        // FluentWindow's chrome/backdrop only finalizes once the HWND
        // exists, so establish that once up front rather than on the
        // first ShowPercent() call (which needs a real ActualWidth/Height
        // immediately, with no visible flash while it settles). Opacity
        // only fades the WPF content layer -- the Mica backdrop is
        // DWM-composited on the native HWND separately and doesn't follow
        // it, so actually hiding later needs Hide(), not Opacity = 0.
        Opacity = 0;
        Show();
        Hide();

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            Hide();
        };
    }

    public void ShowPercent(int percent)
    {
        // Same "re-apply the backdrop, not just resources" fix as
        // FlyoutWindow/SettingsWindow -- gated on change since this runs on
        // every wheel notch and the underlying call is a real native one.
        var currentTheme = ApplicationThemeManager.GetAppTheme();
        if (currentTheme != _lastAppliedTheme)
        {
            ApplicationThemeManager.Apply(this);
            WindowBackgroundManager.UpdateBackground(this, currentTheme, WindowBackdropType.Mica);
            _lastAppliedTheme = currentTheme;
        }

        PercentText.Text = $"{percent}%";
        UpdateLayout();

        var workArea = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Bottom - ActualHeight - 80;

        Opacity = 1;
        Show();
        _hideTimer.Stop();
        _hideTimer.Start();
    }
}
