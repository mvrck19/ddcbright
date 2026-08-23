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
}
