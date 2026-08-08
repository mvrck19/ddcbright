namespace DdcBright;

/// <summary>
/// Trailing-edge debounce: each Trigger() call cancels any not-yet-fired
/// previous one and reschedules, so a burst of rapid calls collapses into a
/// single Action run `delay` after the last call, on a thread-pool thread
/// (not the caller's).
/// </summary>
internal sealed class Debouncer(TimeSpan delay)
{
    private CancellationTokenSource? _cts;

    public void Trigger(Action action)
    {
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _cts, cts)?.Cancel();
        _ = RunAsync(action, cts);
    }

    private async Task RunAsync(Action action, CancellationTokenSource cts)
    {
        // Deliberately not disposing `cts`: after a successful (uncancelled)
        // run, `_cts` still points at this same instance, so a later
        // Trigger() would call Cancel() on it -- disposing first would turn
        // that into an ObjectDisposedException thrown synchronously inside
        // whatever caller (here, a low-level mouse hook callback, where an
        // unhandled exception is fatal to the whole process). A short-lived
        // CancellationTokenSource with no registered WaitHandle is cheap to
        // just let the GC collect.
        try
        {
            await Task.Delay(delay, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        action();
    }
}
