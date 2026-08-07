using System.Windows;
using Wpf.Ui.Appearance;

namespace DdcBright;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private FlyoutWindow? _flyout;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Autostart.Register();

        var settings = Settings.Load();
        ApplyTheme(settings.Theme);

        _flyout = new FlyoutWindow(settings);

        using var iconStream = System.Reflection.Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("sun.ico")!;
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream),
            Visible = true,
            Text = "DDCBright",
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("About", null, (_, _) => ShowAbout());
        menu.Items.Add("Quit", null, (_, _) => Shutdown());
        _trayIcon.ContextMenuStrip = menu;

        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
            {
                _flyout!.ShowNearCursor();
            }
        };

        // Debug affordance: `ddcbright.exe --show` opens the flyout
        // immediately instead of waiting for a tray click, so it can be
        // screenshotted/tested without simulating a click on the tray icon
        // (notoriously unreliable to automate -- it lives in the shell's
        // process, not this one).
        if (e.Args.Contains("--show"))
        {
            _flyout.ShowNearCursor();
        }
    }

    public static void ApplyTheme(ThemePreference preference)
    {
        switch (preference)
        {
            case ThemePreference.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            case ThemePreference.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
            default:
                ApplicationThemeManager.ApplySystemTheme();
                break;
        }
    }

    private static void ShowAbout()
    {
        System.Windows.MessageBox.Show(
            "This is a brightness control application.\n\n" +
            "For more information, please visit the project's GitHub page:\n" +
            "https://github.com/mvrck19/ddcbright\n\n" +
            "Icon made by Iconic Panda from Flaticon:\n" +
            "https://www.flaticon.com/free-icons/brightness",
            "About DDCBright");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
