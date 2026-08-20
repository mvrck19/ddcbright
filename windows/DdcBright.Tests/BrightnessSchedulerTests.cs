namespace DdcBright.Tests;

public class BrightnessSchedulerTests
{
    [Theory]
    [InlineData("09:00", "07:00", "19:00", true)]  // normal range, inside
    [InlineData("06:00", "07:00", "19:00", false)] // normal range, before day start
    [InlineData("20:00", "07:00", "19:00", false)] // normal range, after night start
    [InlineData("07:00", "07:00", "19:00", true)]  // inclusive at day start
    [InlineData("19:00", "07:00", "19:00", false)] // exclusive at night start
    [InlineData("23:00", "22:00", "06:00", true)]  // wraps past midnight, inside (before midnight)
    [InlineData("02:00", "22:00", "06:00", true)]  // wraps past midnight, inside (after midnight)
    [InlineData("10:00", "22:00", "06:00", false)] // wraps past midnight, outside
    public void IsDayPeriod_HandlesNormalAndMidnightWrappingRanges(string nowStr, string dayStr, string nightStr, bool expected)
    {
        var now = TimeOnly.Parse(nowStr);
        var day = TimeOnly.Parse(dayStr);
        var night = TimeOnly.Parse(nightStr);

        Assert.Equal(expected, BrightnessScheduler.IsDayPeriod(now, day, night));
    }

    [Theory]
    [InlineData(20, 80, 0.0, 20)]
    [InlineData(20, 80, 1.0, 80)]
    [InlineData(20, 80, 0.5, 50)]
    [InlineData(80, 20, 0.25, 65)] // fading down, not just up
    [InlineData(50, 50, 0.5, 50)]  // no-op fade (same start/target)
    public void InterpolateBrightness_LinearlyInterpolatesAndRounds(int start, int target, double t, int expected)
    {
        Assert.Equal(expected, BrightnessScheduler.InterpolateBrightness(start, target, t));
    }
}
