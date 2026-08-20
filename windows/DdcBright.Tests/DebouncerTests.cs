using System.Threading;

namespace DdcBright.Tests;

public class DebouncerTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(60);

    // Debouncer is internal (not public), reached via InternalsVisibleTo.

    [Fact]
    public async Task Trigger_CollapsesABurstIntoASingleTrailingCall()
    {
        var debouncer = new Debouncer(Delay);
        var fireCount = 0;
        var lastValue = -1;

        for (var i = 0; i < 10; i++)
        {
            var value = i;
            debouncer.Trigger(() =>
            {
                Interlocked.Increment(ref fireCount);
                lastValue = value;
            });
            await Task.Delay(5); // faster than Delay, so each call cancels the previous
        }

        await Task.Delay(Delay + Delay); // let the trailing call settle and fire

        Assert.Equal(1, fireCount);
        Assert.Equal(9, lastValue); // only the last Trigger() in the burst should have fired
    }

    [Fact]
    public async Task Trigger_FiresAgainAfterAPreviousBurstHasSettled()
    {
        var debouncer = new Debouncer(Delay);
        var fireCount = 0;

        debouncer.Trigger(() => Interlocked.Increment(ref fireCount));
        await Task.Delay(Delay + Delay);
        Assert.Equal(1, fireCount);

        debouncer.Trigger(() => Interlocked.Increment(ref fireCount));
        await Task.Delay(Delay + Delay);
        Assert.Equal(2, fireCount);
    }

    [Fact]
    public async Task Trigger_NeverFiresBeforeTheDelayElapses()
    {
        var debouncer = new Debouncer(Delay);
        var fired = false;

        debouncer.Trigger(() => fired = true);
        await Task.Delay(TimeSpan.FromMilliseconds(Delay.TotalMilliseconds / 3));

        Assert.False(fired);
    }
}
