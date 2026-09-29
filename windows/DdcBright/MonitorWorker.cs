namespace DdcBright;

/// <summary>
/// Owns one physical monitor's DDC/CI traffic on its own dedicated
/// background thread. Every monitor gets one, so monitors are driven in
/// parallel, and a slow or wedged monitor can only ever stall itself --
/// never the UI thread, the tray scroll hook, or the other monitors.
///
/// Requests coalesce instead of queueing up: only the newest requested
/// brightness is ever pending, so a fast slider drag or scroll burst costs
/// at most one write in flight plus one waiting, however many values were
/// requested in between -- and every caller's task completes with the
/// result of the write that covered it. Concurrent reads likewise share one
/// hardware read.
///
/// Callers never wait longer than the hang timeout: a DDC/CI call that
/// hasn't returned by then reports as failed (false/null, same as an
/// unresponsive monitor) while this worker keeps waiting on it. The native
/// call itself can't be cancelled, but because everything for one monitor
/// funnels through one thread, a wedged driver pins exactly that thread --
/// instead of every caller piling up blocked thread-pool threads behind it.
/// </summary>
internal sealed class MonitorWorker
{
    // Real DDC/CI calls take ~50-150ms; anything past this is a monitor
    // that's asleep, mid-input-switch, or behind a flaky hub.
    internal static readonly TimeSpan DefaultHangTimeout = TimeSpan.FromSeconds(2);

    private readonly Func<int, bool> _write;
    private readonly Func<int?> _read;
    private readonly Action _release;
    private readonly TimeSpan _hangTimeout;
    private readonly object _lock = new();

    // All guarded by _lock.
    private int _pendingWrite;
    private TaskCompletionSource<bool>? _pendingWriteDone;
    private TaskCompletionSource<int?>? _pendingRead;
    private bool _retired;

    /// <param name="write">Blocking hardware write; returns whether the monitor accepted it.</param>
    /// <param name="read">Blocking hardware read; null if the monitor didn't respond.</param>
    /// <param name="release">Runs once on the worker thread after <see cref="Retire"/>, when nothing is left in flight.</param>
    public MonitorWorker(string name, Func<int, bool> write, Func<int?> read, Action release, TimeSpan? hangTimeout = null)
    {
        _write = write;
        _read = read;
        _release = release;
        _hangTimeout = hangTimeout ?? DefaultHangTimeout;
        new Thread(Run) { IsBackground = true, Name = $"DDC/CI: {name}" }.Start();
    }

    public Task<bool> SetBrightnessAsync(int percent)
    {
        TaskCompletionSource<bool> done;
        lock (_lock)
        {
            if (_retired) return Task.FromResult(false);
            _pendingWrite = percent;
            done = _pendingWriteDone ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            Monitor.Pulse(_lock);
        }
        return WithTimeout(done.Task, false);
    }

    public Task<int?> GetBrightnessAsync()
    {
        TaskCompletionSource<int?> done;
        lock (_lock)
        {
            if (_retired) return Task.FromResult<int?>(null);
            done = _pendingRead ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            Monitor.Pulse(_lock);
        }
        return WithTimeout(done.Task, null);
    }

    /// <summary>
    /// Stops accepting requests (they fail fast from here on), finishes
    /// whatever was already accepted, then calls the release callback --
    /// so the monitor handle is never destroyed under an in-flight call.
    /// </summary>
    public void Retire()
    {
        lock (_lock)
        {
            _retired = true;
            Monitor.Pulse(_lock);
        }
    }

    private void Run()
    {
        while (true)
        {
            int write;
            TaskCompletionSource<bool>? writeDone;
            TaskCompletionSource<int?>? readDone;
            lock (_lock)
            {
                while (_pendingWriteDone is null && _pendingRead is null && !_retired)
                    Monitor.Wait(_lock);
                if (_pendingWriteDone is null && _pendingRead is null)
                    break; // retired and fully drained

                write = _pendingWrite;
                writeDone = _pendingWriteDone;
                readDone = _pendingRead;
                _pendingWriteDone = null;
                _pendingRead = null;
            }

            // Write before read, so a read queued alongside a write sees it.
            writeDone?.TrySetResult(Guard(() => _write(write), false));
            readDone?.TrySetResult(Guard(_read, null));
        }
        Guard(() => { _release(); return true; }, false);
    }

    private async Task<T> WithTimeout<T>(Task<T> task, T onTimeout)
    {
        try
        {
            return await task.WaitAsync(_hangTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return onTimeout;
        }
    }

    // An exception escaping a plain Thread kills the whole process, so a
    // throwing call is logged and reported as a failed one instead.
    private static T Guard<T>(Func<T> call, T onError)
    {
        try
        {
            return call();
        }
        catch (Exception ex)
        {
            CrashReporting.AppendLog("crash.log", CrashReporting.FormatLogLine(DateTime.Now, "ddc-worker", ex));
            return onError;
        }
    }
}
