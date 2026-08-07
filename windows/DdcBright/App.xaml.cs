using System.Windows;
using Wpf.Ui.Appearance;

namespace DdcBright;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private FlyoutWindow? _flyout;
    private SettingsWindow? _settingsWindow;
    private Settings? _settings;
    private BrightnessScheduler? _scheduler;
    private AmbientLightSensor? _ambientSensor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Autostart.Register();

        _settings = Settings.Load();
        ApplyTheme(_settings.Theme);

        _scheduler = new BrightnessScheduler(_settings);
        _ambientSensor = new AmbientLightSensor(_settings);
        ApplyAutoBrightnessMode();

        _flyout = new FlyoutWindow(_settings);

        using var iconStream = System.Reflection.Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("sun.ico")!;
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream),
            Visible = true,
            Text = "DDCBright",
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
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
        if (e.Args.Contains("--settings"))
        {
            ShowSettings();
        }
        if (e.Args.Contains("--ambient-test"))
        {
            // Test-only: force Ambient mode for this run without touching
            // the persisted settings file.
            _settings.AutoBrightnessMode = AutoBrightnessMode.Ambient;
            ApplyAutoBrightnessMode();
        }
    }

    public void ShowSettings()
    {
        if (_settingsWindow is null || !_settingsWindow.IsLoaded)
            _settingsWindow = new SettingsWindow(_settings!);

        _settingsWindow.Show();
        _settingsWindow.Activate();
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

    public void ApplyAutoBrightnessMode()
    {
        _scheduler?.Stop();
        _ambientSensor?.Stop();

        switch (_settings!.AutoBrightnessMode)
        {
            case AutoBrightnessMode.Schedule:
                _scheduler!.Start();
                break;
            case AutoBrightnessMode.Ambient:
                _ambientSensor!.Start();
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
        _scheduler?.Stop();
        _ambientSensor?.Stop();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
