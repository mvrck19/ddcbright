namespace DdcBright.Tests;

public class MonitorControlTests
{
    // ClampPercent is internal (not public), reached via InternalsVisibleTo.

    [Theory]
    [InlineData(-50, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(255, 100)]
    public void ClampPercent_ClampsToZeroToOneHundred(int input, int expected)
    {
        Assert.Equal(expected, MonitorControl.ClampPercent(input));
    }

    // Guards the handle-leak fix: repeated calls must reuse the cached
    // physical-monitor handles, and only invalidation opens fresh ones.
    // (Trivially passes with no DDC/CI monitors attached, e.g. on CI.)
    [Fact]
    public void GetMonitors_ReusesHandlesUntilInvalidated()
    {
        var first = MonitorControl.GetMonitors();
        Assert.Equal(first, MonitorControl.GetMonitors());

        MonitorControl.InvalidateMonitors();
        Assert.Equal(first.Count, MonitorControl.GetMonitors().Count);
    }
}
