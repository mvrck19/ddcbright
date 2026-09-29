using System.Collections.Concurrent;
using System.Threading;

namespace DdcBright.Tests;

public class MonitorWorkerTests
{
    // Generous: only reached when a test is actually broken.
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    private static MonitorWorker NewWorker(Func<int, bool> write, Func<int?>? read = null, Action? release = null, TimeSpan? hangTimeout = null) =>
        new("test", write, read ?? (() => 50), release ?? (() => { }), hangTimeout ?? TestTimeout);

    [Fact]
    public async Task SetBrightness_CoalescesABurstDownToTheNewestValue()
    {
        using var firstWriteStarted = new ManualResetEventSlim();
        using var gate = new ManualResetEventSlim();
        var written = new ConcurrentQueue<int>();
        var worker = NewWorker(value =>
        {
            written.Enqueue(value);
            firstWriteStarted.Set();
            gate.Wait();
            return true;
        });

        var first = worker.SetBrightnessAsync(0);
        Assert.True(firstWriteStarted.Wait(TestTimeout));
        // Queued while the first write is still in flight: only the newest
        // of these should ever reach the hardware.
        var burst = Enumerable.Range(1, 10).Select(worker.SetBrightnessAsync).ToList();
        gate.Set();

        Assert.True(await first);
        Assert.All(await Task.WhenAll(burst), Assert.True);
        Assert.Equal([0, 10], written);
    }

    [Fact]
    public async Task SetBrightness_ReportsFailureOnceTheHangTimeoutPasses()
    {
        using var gate = new ManualResetEventSlim();
        var worker = NewWorker(_ => { gate.Wait(); return true; }, hangTimeout: TimeSpan.FromMilliseconds(100));

        Assert.False(await worker.SetBrightnessAsync(40).WaitAsync(TestTimeout));
        gate.Set(); // let the stuck worker thread finish
    }

    [Fact]
    public async Task AHungMonitorDoesNotDelayAnotherOne()
    {
        using var gate = new ManualResetEventSlim();
        var hung = NewWorker(_ => { gate.Wait(); return true; });
        var healthy = NewWorker(_ => true);

        var stuck = hung.SetBrightnessAsync(40);
        Assert.True(await healthy.SetBrightnessAsync(40).WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.False(stuck.IsCompleted);
        gate.Set();
        Assert.True(await stuck);
    }

    [Fact]
    public async Task Retire_ReleasesOnlyAfterTheInFlightCallFinishes()
    {
        using var writeStarted = new ManualResetEventSlim();
        using var gate = new ManualResetEventSlim();
        using var released = new ManualResetEventSlim();
        var writeFinished = false;
        var releasedBeforeWriteFinished = false;
        var worker = NewWorker(
            _ => { writeStarted.Set(); gate.Wait(); writeFinished = true; return true; },
            release: () => { releasedBeforeWriteFinished = !writeFinished; released.Set(); });

        var inFlight = worker.SetBrightnessAsync(40);
        Assert.True(writeStarted.Wait(TestTimeout));
        worker.Retire();

        Assert.False(await worker.SetBrightnessAsync(60)); // fails fast once retired
        Assert.Null(await worker.GetBrightnessAsync());
        Assert.False(released.IsSet);

        gate.Set();
        Assert.True(await inFlight);
        Assert.True(released.Wait(TestTimeout));
        Assert.False(releasedBeforeWriteFinished);
    }

    [Fact]
    public async Task GetBrightness_ReturnsTheHardwareReadingOrNull()
    {
        Assert.Equal(73, await NewWorker(_ => true, read: () => 73).GetBrightnessAsync());
        Assert.Null(await NewWorker(_ => true, read: () => null).GetBrightnessAsync());
    }

    [Fact]
    public async Task AThrowingCallIsReportedAsAFailureAndTheWorkerKeepsGoing()
    {
        var calls = 0;
        var worker = NewWorker(_ => Interlocked.Increment(ref calls) == 1
            ? throw new InvalidOperationException("boom")
            : true);

        Assert.False(await worker.SetBrightnessAsync(10));
        Assert.True(await worker.SetBrightnessAsync(20));
    }
}
