using System.IO;

namespace DdcBright;

/// <summary>
/// Wires up crash reporting for the whole app: a local log file at
/// %APPDATA%\ddcbright\crash.log (always on, mirrors AmbientLightSensor's
/// existing ambient.log convention) for the three places an exception can
/// otherwise vanish without a trace: the AppDomain, the WPF Dispatcher, and
/// unobserved Task exceptions. Deliberately local-only: single-user app, no
/// telemetry leaves the machine.
/// </summary>
internal static class CrashReporting
{
    private const long MaxLogBytes = 512 * 1024;

    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ddcbright");

    public static void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ReportFatal(e.ExceptionObject as Exception ?? new Exception($"Non-Exception object thrown: {e.ExceptionObject}"), "AppDomain");

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
    }

    private static void ReportFatal(Exception ex, string source) =>
        AppendLog("crash.log", FormatLogLine(DateTime.Now, source, ex));

    // ponytail: File.AppendAllText with no lock -- concurrent writers
    // (UI thread + thread-pool Perf scopes) can rarely interleave or hit a
    // sharing IOException, which is swallowed. Add a lock if lines go missing.
    internal static void AppendLog(string fileName, string line)
    {
        var path = Path.Combine(LogDir, fileName);
        try
        {
            Directory.CreateDirectory(LogDir);
            if (File.Exists(path) && ShouldRotate(new FileInfo(path).Length, MaxLogBytes))
                File.Delete(path); // ponytail: simple restart-on-overflow, no rotation (matches ambient.log)
            File.AppendAllText(path, line);
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
