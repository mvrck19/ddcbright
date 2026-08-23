using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using Wpf.Ui.Appearance;

namespace DdcBright;

public partial class App : System.Windows.Application
{
    private const int TrayScrollStepPercent = 5;
    private const string ReleasesUrl = "https://github.com/mvrck19/ddcbright/releases";
    private static readonly HttpClient UpdateHttpClient = new();

    private Mutex? _singleInstanceMutex;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private TrayIconScrollHook? _trayScrollHook;
    private readonly Debouncer _trayScrollDebouncer = new(TimeSpan.FromMilliseconds(80));
    private int? _trayBrightnessEstimate;
    private BrightnessOsd? _osd;
    private FlyoutWindow? _flyout;
    private SettingsWindow? _settingsWindow;
    private Settings? _settings;
    private BrightnessScheduler? _scheduler;
    private AmbientLightSensor? _ambientSensor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CrashReporting.Initialize();
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

        if (e.Args.Contains("--test-flyout-resize"))
        {
            TestFlyoutResize();
            return;
        }

        // `ddcbright.exe --ui-test-settings` opens the Settings window on a
        // throwaway in-memory Settings object (never touches settings.json)
        // and keeps running -- for the DdcBright.UiTests project (FlaUI) to
        // attach to and drive via real UI Automation. No Shutdown() here:
        // unlike the other self-checks below, this one needs the Dispatcher
        // to keep pumping so the external tool can interact with it.
        //
        // _settings/_scheduler/_ambientSensor must be set up for real (not
        // skipped along with the tray/mutex/autostart below) -- clicking a
        // mode button in Settings calls ApplyAutoBrightnessMode() on this
        // App instance, which NullReferenceExceptions without them. Found by
        // the very first UI test run against this flag.
        if (e.Args.Contains("--ui-test-settings"))
        {
            _settings = new Settings();
            _scheduler = new BrightnessScheduler(_settings);
            _ambientSensor = new AmbientLightSensor(_settings);
            _settingsWindow = new SettingsWindow(_settings);
            _settingsWindow.Show();
            return;
        }

        // `ddcbright.exe --test-ambient-light` does one webcam capture using
        // the saved camera choice and reports exactly what happened --
        // useful for checking whether an unsupported/old webcam is even
        // detected, without opening the UI or needing Ambient mode running.
        if (e.Args.Contains("--test-ambient-light"))
        {
            TestAmbientLight();
            return;
        }

        // Used by the installer's uninstaller, run before the exe it points
        // at gets deleted -- the app registers its own autostart entry on
        // every launch, so it also owns cleaning it up.
        if (e.Args.Contains("--unregister-autostart"))
        {
            Autostart.Unregister();
            Shutdown();
            return;
        }

        // Prevents duplicate tray icons if launched twice, and doubles as
        // the AppMutex the installer checks before install/uninstall so it
        // can prompt to close a running instance first. Only kept (and
        // later released) when this instance actually owns it -- a mutex
        // object that lost the race still gets a real handle back, and
        // ReleaseMutex() on one you don't own throws.
        var mutex = new Mutex(true, "DdcBright-SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }
        _singleInstanceMutex = mutex;

        _settings = Settings.Load();
        ApplyTheme(_settings.Theme);

        // Reconciled on every startup, not just when the Settings toggle is
        // clicked -- also cleans up the Run key for anyone who had it
        // registered under an older version (always-on) and then turns it off.
        if (_settings.LaunchAtStartup) Autostart.Register(); else Autostart.Unregister();

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
        menu.Items.Add("Check for Updates", null, async (_, _) => await CheckForUpdatesAsync());
        menu.Items.Add("Quit", null, (_, _) => Shutdown());
        _trayIcon.ContextMenuStrip = menu;

        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
            {
                _flyout!.ShowNearCursor();
            }
        };

        _osd = new BrightnessOsd();

        // Scroll wheel over the tray icon adjusts every monitor's brightness
        // directly, without opening the flyout, and shows the running
        // percentage in a small OSD. The hook callback runs synchronously on
        // the low-level global input pipeline -- ALL system mouse input
        // stalls while it runs, so it must never touch hardware directly.
        // Each notch only updates an in-memory target (cheap: no hardware
        // I/O, safe to do inline) and refreshes the OSD from it; a debounced
        // flush (on a thread-pool thread, well after the hook returns)
        // writes that settled target to the monitors once scrolling pauses.
        // Tracking a target in software like this -- rather than reading
        // hardware brightness and adding a delta on every notch -- also
        // sidesteps a real DDC/CI quirk: many monitors don't commit a
        // SetBrightness write in time for an immediate re-read, so
        // Get-then-Set once per notch in quick succession can make separate
        // notches race the hardware and stomp on each other.
        // Skip installing the hook entirely while a debugger is attached.
        // Windows serializes global mouse input through WH_MOUSE_LL hooks:
        // if the owning thread (this one) stops responding -- suspended at
        // a breakpoint, or torn down mid-restart during an edit/rebuild/
        // relaunch cycle -- system-wide mouse input can stall until this
        // hook responds or Windows' hook-timeout kicks in
        // (HKCU\Control Panel\Desktop\LowLevelHooksTimeout). Scroll-to-
        // adjust over the tray icon is a nicety, not worth that risk during
        // development.
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            _trayScrollHook = new TrayIconScrollHook(_trayIcon, direction =>
            {
                if (_trayBrightnessEstimate is not { } current) return; // startup read hasn't landed yet
                var target = Math.Clamp(current + direction * TrayScrollStepPercent, 0, 100);
                _trayBrightnessEstimate = target;
                _osd.ShowPercent(target);
                UpdateTrayTooltip(target);
                _trayScrollDebouncer.Trigger(() =>
                {
                    ExitAutoModeIfActive();
                    SetAllMonitorsBrightness(target);
                });
            });
        }

        // Seed the estimate once, off the UI thread, so the very first
        // hook invocation above doesn't have to block on a DDC/CI read.
        Task.Run(() =>
        {
            var monitors = MonitorControl.GetMonitors();
            var estimate = monitors.Count > 0 ? MonitorControl.GetBrightness(monitors[0]) ?? 50 : 50;
            _trayBrightnessEstimate = estimate;
            UpdateTrayTooltip(estimate);
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

        // `ddcbright.exe --test-crash` deliberately throws once the message
        // loop is pumping, to verify CrashReporting end-to-end (local
        // crash.log + Sentry, if configured). Deferred via BeginInvoke
        // rather than thrown synchronously here -- OnStartup runs before
        // the Dispatcher starts pumping, so a synchronous throw would hit
        // AppDomain.UnhandledException instead of the far more common
        // real-world path, DispatcherUnhandledException.
        if (e.Args.Contains("--test-crash"))
        {
            Dispatcher.BeginInvoke(new Action(() =>
                throw new InvalidOperationException("Test crash via --test-crash")));
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

    // A manual brightness change (flyout slider, tray scroll wheel) should
    // stick instead of getting silently overwritten by the scheduler/ambient
    // sensor on their next tick -- so it drops the mode back to Off. The
    // no-op guard matters: callers can invoke this many times per second
    // (slider drag, rapid scroll notches), and after the first call it's
    // just a cheap enum comparison.
    public void ExitAutoModeIfActive()
    {
        if (_settings!.AutoBrightnessMode == AutoBrightnessMode.Off) return;
        _settings.AutoBrightnessMode = AutoBrightnessMode.Off;
        _settings.Save();
        ApplyAutoBrightnessMode();
    }

    // Hovering the tray icon shows the current brightness rather than the
    // app name -- that's more useful at a glance, and "DdcBright" is
    // already visible from the icon itself being in the tray.
    internal void UpdateTrayTooltip(int percent) => _trayIcon!.Text = $"{percent}%";

    private static void SetAllMonitorsBrightness(int value)
    {
        foreach (var monitor in MonitorControl.GetMonitors())
            MonitorControl.SetBrightness(monitor, value);
    }

    private static void TestFlyoutResize()
    {
        var log = new List<string>();
        try
        {
            // Reported repro: SAME content both times, just open/close/reopen.
            var settings = new Settings { AutoBrightnessMode = AutoBrightnessMode.Off };

            var flyout = new FlyoutWindow(settings);
            flyout.ShowNearCursor();
            var tallHeight = flyout.ActualHeight;
            log.Add($"First open height: {tallHeight:F0}px");

            flyout.Hide();
            flyout.ShowNearCursor();
            var shortHeight = flyout.ActualHeight;
            log.Add($"Second open (same content) height: {shortHeight:F0}px");

            log.Add(Math.Abs(shortHeight - tallHeight) < 1
                ? "RESULT: PASS (same height both opens)"
                : $"RESULT: FAIL (height changed by {shortHeight - tallHeight:F0}px on re-open with identical content)");

            flyout.Close();
        }
        catch (Exception ex)
        {
            log.Add($"RESULT: FAIL - {ex}");
        }

        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "ddcbright_flyout_resize_test.txt"), log);
        Current.Shutdown();
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

    private static void TestAmbientLight()
    {
        var log = new List<string>();
        try
        {
            var settings = Settings.Load();

            // Task.Run, not a direct .GetAwaiter().GetResult(), matters here:
            // this runs during OnStartup, before Application.Run has started
            // pumping the Dispatcher. A direct blocking wait would capture
            // the (not-yet-pumping) DispatcherSynchronizationContext for each
            // `await` inside AmbientLightSensor, and every continuation would
            // then sit queued forever waiting for a message loop that can't
            // run because this very call is blocking it -- a classic
            // sync-over-async deadlock. Task.Run hops onto a thread-pool
            // thread with no captured context, so awaits resume there instead.
            var cameras = Task.Run(AmbientLightSensor.GetAvailableCamerasAsync).GetAwaiter().GetResult();
            log.Add(cameras.Count == 0
                ? "Cameras detected by Windows: none"
                : $"Cameras detected by Windows: {string.Join(", ", cameras.Select(c => c.Name))}");

            var result = Task.Run(() => AmbientLightSensor.CaptureOnceAsync(settings.AmbientCameraId)).GetAwaiter().GetResult();
            log.Add(result.Success
                ? $"RESULT: PASS - {result.Message} (luma={result.Luma})"
                : $"RESULT: FAIL - {result.Message}");
        }
        catch (Exception ex)
        {
            log.Add($"RESULT: FAIL - {ex}");
        }

        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "ddcbright_ambient_test.txt"), log);
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

    private static async Task CheckForUpdatesAsync()
    {
        var currentVersion = typeof(App).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var result = await UpdateChecker.CheckAsync(UpdateHttpClient, currentVersion);

        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable:
                var openReleases = System.Windows.MessageBox.Show(
                    $"v{result.LatestVersion} is available (you have v{currentVersion}).\n\nOpen the releases page?",
                    "Update Available", MessageBoxButton.YesNo);
                if (openReleases == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(ReleasesUrl) { UseShellExecute = true });
                break;
            case UpdateCheckStatus.UpToDate:
                System.Windows.MessageBox.Show($"You're up to date (v{result.LatestVersion}).", "Check for Updates");
                break;
            default:
                System.Windows.MessageBox.Show("Couldn't check for updates. Please try again later.", "Check for Updates");
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
        CrashReporting.Shutdown();
        _scheduler?.Stop();
        _ambientSensor?.Stop();
        _trayScrollHook?.Dispose();
        _trayIcon?.Dispose();
        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }
        base.OnExit(e);
    }
}
