namespace DdcBright.Tests;

public class CrashReportingTests
{
    [Fact]
    public void FormatLogLine_IncludesTimestampSourceAndException()
    {
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5);
        var ex = new InvalidOperationException("boom");

        var line = CrashReporting.FormatLogLine(timestamp, "Dispatcher", ex);

        Assert.Contains("2026-01-02 03:04:05", line);
        Assert.Contains("[Dispatcher]", line);
        Assert.Contains("InvalidOperationException", line);
        Assert.Contains("boom", line);
    }

    [Theory]
    [InlineData(0, 1000, false)]
    [InlineData(1000, 1000, false)]
    [InlineData(1001, 1000, true)]
    public void ShouldRotate_ComparesAgainstMaxBytes(long currentLength, long maxBytes, bool expected)
    {
        Assert.Equal(expected, CrashReporting.ShouldRotate(currentLength, maxBytes));
    }
}
