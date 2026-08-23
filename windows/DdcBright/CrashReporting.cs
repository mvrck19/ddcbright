using System.IO;
using System.Reflection;
using Sentry;

namespace DdcBright;

/// <summary>
/// Wires up crash reporting for the whole app: a local log file at
/// %APPDATA%\ddcbright\crash.log (always on, mirrors AmbientLightSensor's
/// existing ambient.log convention) plus Sentry (Release builds only,
/// opt-out via DDCBRIGHT_DISABLE_SENTRY) for the three places an exception
/// can otherwise vanish without a trace: the AppDomain, the WPF Dispatcher,
/// and unobserved Task exceptions.
/// </summary>
internal static class CrashReporting
{
    private const long MaxLogBytes = 512 * 1024;

    // Sentry DSNs are write-only ingest identifiers, not secrets -- Sentry's
    // own docs say they're safe to embed directly in client/desktop apps.
    // Empty here on purpose: create a Sentry project (sentry.io, free
    // Developer tier) and paste its DSN in before shipping a Release build,
    // otherwise Sentry reporting is silently skipped (the local crash.log
    // still works either way).
    private const string Dsn = "";

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ddcbright",
        "crash.log");

    public static void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            ReportFatal(e.ExceptionObject as Exception ?? new Exception($"Non-Exception object thrown: {e.ExceptionObject}"), "AppDomain");
            // The process is already terminating here (e.IsTerminating is
            // effectively always true for this event) -- Sentry's transport
            // is async, so without a synchronous flush the queued event can
            // be lost before it ever gets sent.
            SentrySdk.Flush(TimeSpan.FromSeconds(2));
        };

        System.Windows.Application.Current.DispatcherUnhandledException += (_, e) =>
        {
            ReportFatal(e.Exception, "Dispatcher");
            // Deliberately not handled: this app does live hardware I/O
            // (DDC/CI writes) and runs background timers (scheduler fades,
            // ambient-light EMA) -- an unanticipated UI-thread exception
            // means some invariant broke, and limping on risks writing a
            // bad brightness value or corrupting that state silently.
            e.Handled = false;
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ReportFatal(e.Exception, "UnobservedTask");
            e.SetObserved();
        };

        if (Environment.GetEnvironmentVariable("DDCBRIGHT_DISABLE_SENTRY") is not null || Dsn.Length == 0)
            return;

#if !DEBUG
        SentrySdk.Init(options =>
        {
            options.Dsn = Dsn;
            // Non-ASP.NET app: there's no per-request scope to isolate
            // events into, so global mode is what Sentry's own desktop-app
            // guidance calls for.
            options.IsGlobalModeEnabled = true;
            options.Release = typeof(App).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            options.Environment = "production";
            // A brightness-control utility has no legitimate need for PII
            // in breadcrumbs/events.
            options.SendDefaultPii = false;
            // Performance data goes through DdcBrightEventSource/ETW
            // instead (see DdcBrightEventSource.cs) -- enabling Sentry's
            // own tracing here would be a second, redundant instrumentation
            // system (and eats into the free tier's event quota).
            options.TracesSampleRate = 0;
            // This is a laptop-class tray app that can be asleep/offline
            // when it crashes -- queue undelivered events on disk instead
            // of dropping them.
            options.CacheDirectoryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ddcbright", "sentry-cache");
            // All three exception sources above are hand-wired uniformly;
            // don't also let Sentry auto-subscribe to two of them.
            options.DisableAppDomainUnhandledExceptionCapture();
            options.DisableUnobservedTaskExceptionCapture();
        });
#endif
    }

    public static void Shutdown() => SentrySdk.Close();

    private static void ReportFatal(Exception ex, string source)
    {
        SentrySdk.CaptureException(ex);
        WriteLocalLog(source, ex);
    }

    private static void WriteLocalLog(string source, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            if (File.Exists(LogPath) && ShouldRotate(new FileInfo(LogPath).Length, MaxLogBytes))
                File.Delete(LogPath); // ponytail: simple restart-on-overflow, no rotation (matches ambient.log)
            File.AppendAllText(LogPath, FormatLogLine(DateTime.Now, source, ex));
        }
        catch (Exception logEx) when (logEx is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal static string FormatLogLine(DateTime timestamp, string source, Exception ex) =>
        $"{timestamp:yyyy-MM-dd HH:mm:ss}  [{source}]  {ex}{Environment.NewLine}";

    internal static bool ShouldRotate(long currentFileLengthBytes, long maxBytes) =>
        currentFileLengthBytes > maxBytes;
}
