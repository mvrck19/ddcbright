using System.Diagnostics;

namespace DdcBright;

/// <summary>
/// Times one operation and appends it to %APPDATA%\ddcbright\perf.log --
/// always when <c>logAlways</c> is set (the flyout open and its phases, so
/// typical-vs-slow distributions can be read straight off the file), and
/// otherwise only when it took longer than <see cref="SlowThresholdMs"/>
/// (DDC/CI calls and scheduler ticks run too often to log every one).
/// Usage: <c>using var _ = Perf.Measure("flyout.open", logAlways: true);</c>
/// </summary>
internal sealed class Perf : IDisposable
{
    internal const int SlowThresholdMs = 200;

    private readonly string _operation;
    private readonly bool _logAlways;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private Perf(string operation, bool logAlways)
    {
        _operation = operation;
        _logAlways = logAlways;
    }

    public static Perf Measure(string operation, bool logAlways = false) => new(operation, logAlways);

    public void Dispose()
    {
        var elapsedMs = _stopwatch.ElapsedMilliseconds;
        if (_logAlways || elapsedMs > SlowThresholdMs)
            CrashReporting.AppendLog("perf.log", FormatLogLine(DateTime.Now, _operation, elapsedMs));
    }

    internal static string FormatLogLine(DateTime timestamp, string operation, long elapsedMs) =>
        $"{timestamp:yyyy-MM-dd HH:mm:ss.fff}  {operation}  {elapsedMs}ms{(elapsedMs > SlowThresholdMs ? "  SLOW" : "")}{Environment.NewLine}";
}
