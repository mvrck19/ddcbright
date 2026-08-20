namespace DdcBright.Tests;

public class TrayIconScrollHookTests
{
    private static readonly TrayIconScrollHook.Rect IconRect = new() { Left = 100, Top = 100, Right = 116, Bottom = 116 };

    private static TrayIconScrollHook.MouseLowLevelHookStruct MakeEvent(int x, int y, short wheelDelta) => new()
    {
        Pt = new TrayIconScrollHook.Point { X = x, Y = y },
        MouseData = unchecked((uint)(wheelDelta << 16)),
    };

    [Fact]
    public void TryGetScrollDirection_ReturnsFalse_WhenCursorIsOutsideTheIconRect()
    {
        var data = MakeEvent(x: 500, y: 500, wheelDelta: 120);

        var found = TrayIconScrollHook.TryGetScrollDirection(data, IconRect, out var direction);

        Assert.False(found);
        Assert.Equal(0, direction);
    }

    [Theory]
    [InlineData((short)120, 1)]  // scroll up/forward
    [InlineData((short)-120, -1)] // scroll down/backward
    public void TryGetScrollDirection_ReturnsTheWheelSign_WhenCursorIsOverTheIcon(short wheelDelta, int expectedDirection)
    {
        var data = MakeEvent(x: 108, y: 108, wheelDelta: wheelDelta);

        var found = TrayIconScrollHook.TryGetScrollDirection(data, IconRect, out var direction);

        Assert.True(found);
        Assert.Equal(expectedDirection, direction);
    }

    [Fact]
    public void TryGetScrollDirection_IsFastEnoughToRunInsideAGlobalInputHook()
    {
        // Direct regression guard for the reported "whole desktop slows
        // down" symptom: TrayIconScrollHook's callback runs synchronously
        // on Windows' global low-level mouse pipeline, so this decision
        // logic must stay far below Windows' hook responsiveness budget
        // (HKCU\Control Panel\Desktop\LowLevelHooksTimeout) or system-wide
        // mouse input can stall.
        var data = MakeEvent(x: 108, y: 108, wheelDelta: 120);

        for (var i = 0; i < 1000; i++) // warm up the JIT before timing
            TrayIconScrollHook.TryGetScrollDirection(data, IconRect, out _);

        const int iterations = 100_000;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
            TrayIconScrollHook.TryGetScrollDirection(data, IconRect, out _);
        sw.Stop();

        var averageMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / iterations;
        Assert.True(averageMicroseconds < 50,
            $"TryGetScrollDirection averaged {averageMicroseconds:F2}us/call, expected well under 50us -- a global mouse hook callback must stay cheap.");
    }
}
