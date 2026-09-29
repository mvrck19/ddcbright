using System.Diagnostics;
using Sentry;

namespace DdcBright;

/// <summary>
/// Times one operation as a Sentry span (a transaction when nothing on this
/// thread is already being timed, a child span otherwise) and appends a line
/// to %APPDATA%\ddcbright\perf.log when it took longer than
/// <see cref="SlowThresholdMs"/> -- so slow flyout opens / DDC/CI calls are
/// visible locally even without Sentry. Usage: <c>using var _ = Perf.Measure("flyout.open");</c>
/// </summary>
internal sealed class Perf : IDisposable
{
    internal const int SlowThresholdMs = 200;

    // ponytail: [ThreadStatic] nesting only covers synchronous call chains --
    // work handed off to Task.Run starts its own transaction. Fine for this
    // app (no async/await on the hot paths); switch to AsyncLocal if that changes.
    [ThreadStatic] private static Perf? _current;

    private readonly Perf? _parent;
    private readonly ISpan _span;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private Perf(string operation)
    {
        _parent = _current;
        _span = _parent is null
            ? SentrySdk.StartTransaction(operation, operation)
            : _parent._span.StartChild(operation);
        _current = this;
    }

    public static Perf Measure(string operation) => new(operation);

    public void Dispose()
    {
        _current = _parent;
        _span.Finish();
        if (_stopwatch.ElapsedMilliseconds > SlowThresholdMs)
            CrashReporting.AppendLog("perf.log", FormatLogLine(DateTime.Now, _span.Operation, _stopwatch.ElapsedMilliseconds));
    }

    internal static string FormatLogLine(DateTime timestamp, string operation, long elapsedMs) =>
        $"{timestamp:yyyy-MM-dd HH:mm:ss}  SLOW  {operation}  {elapsedMs}ms{Environment.NewLine}";
}
