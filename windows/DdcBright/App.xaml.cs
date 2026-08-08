using System.IO;
using System.Windows;
using Wpf.Ui.Appearance;

namespace DdcBright;

public partial class App : System.Windows.Application
{
    private const int TrayScrollStepPercent = 5;

    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private TrayIconScrollHook? _trayScrollHook;
    private readonly Debouncer _trayScrollDebouncer = new(TimeSpan.FromMilliseconds(80));
    private int _pendingTrayScrollDelta;
    private FlyoutWindow? _flyout;
    private SettingsWindow? _settingsWindow;
    private Settings? _settings;
    private BrightnessScheduler? _scheduler;
    private AmbientLightSensor? _ambientSensor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // `ddcbright.exe --render-preview <dir>` renders the flyout/settings
        // windows to PNG without showing them, for eyeballing XAML changes
        // in a script or a locked/headless session -- skips tray icon,
        // autostart, and scheduler setup entirely.
        if (GetArgValue(e.Args, "--render-preview") is { } previewDir)
        {
            RenderPreview(previewDir);
            Shutdown();
            return;
        }

        if (e.Args.Contains("--test-debouncer"))
        {
            TestDebouncer();
            return;
        }

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

        // Scroll wheel over the tray icon adjusts every monitor's brightness
        // directly, without opening the flyout. The hook callback runs
        // synchronously on the low-level global input pipeline -- ALL
        // system mouse input stalls while it runs, so it must never touch
        // hardware directly. Each notch just accumulates a pending delta;
        // a debounced flush (on a thread-pool thread, well after the hook
        // returns) does the actual Get+Set round trip once scrolling
        // pauses. This also sidesteps a real DDC/CI quirk: many monitors
        // don't commit a SetBrightness write in time for an immediate
        // re-read, so firing Get+Set once per notch in quick succession
        // can make separate notches race and stomp on each other instead
        // of adding up the way the user actually scrolled.
        _trayScrollHook = new TrayIconScrollHook(_trayIcon, direction =>
        {
            Interlocked.Add(ref _pendingTrayScrollDelta, direction * TrayScrollStepPercent);
            _trayScrollDebouncer.Trigger(() =>
                AdjustAllMonitorsBrightness(Interlocked.Exchange(ref _pendingTrayScrollDelta, 0)));
        });

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

    private static void AdjustAllMonitorsBrightness(int delta)
    {
        if (delta == 0) return;

        foreach (var monitor in MonitorControl.GetMonitors())
        {
            var current = MonitorControl.GetBrightness(monitor);
            MonitorControl.SetBrightness(monitor, Math.Clamp(current + delta, 0, 100));
        }
    }

    private static void TestDebouncer()
    {
        var log = new List<string>();
        var fireCount = 0;
        var lastValue = 0;
        var debouncer = new Debouncer(TimeSpan.FromMilliseconds(50));

        try
        {
            for (var burst = 1; burst <= 3; burst++)
            {
                var b = burst;
                for (var i = 0; i < 5; i++)
                {
                    debouncer.Trigger(() =>
                    {
                        Interlocked.Increment(ref fireCount);
                        lastValue = b;
                    });
                    Thread.Sleep(10);
                }
                Thread.Sleep(150); // let it settle and fire before starting the next burst
                log.Add($"Burst {burst}: fireCount={fireCount} (expected {burst}), lastValue={lastValue} (expected {burst})");
            }
            log.Add("RESULT: PASS (no exception across 3 settle-then-trigger-again cycles)");
        }
        catch (Exception ex)
        {
            log.Add($"RESULT: FAIL - {ex}");
        }

        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "ddcbright_debouncer_test.txt"), log);
        Current.Shutdown();
    }

    private static void RenderPreview(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var settings = Settings.Load();
        ApplyTheme(settings.Theme);

        var flyout = new FlyoutWindow(settings);
        flyout.PrepareForPreview();
        PreviewRenderer.Render(flyout, Path.Combine(outputDir, "flyout.png"), width: 400);

        var settingsWindow = new SettingsWindow(settings);
        PreviewRenderer.Render(settingsWindow, Path.Combine(outputDir, "settings.png"), width: 460);
    }

    private static string? GetArgValue(string[] args, string flag)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
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
        _trayScrollHook?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
