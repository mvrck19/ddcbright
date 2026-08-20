using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;

namespace DdcBright.UiTests;

/// <summary>
/// Drives the real Settings window via FlaUI/UI Automation, launched through
/// `ddcbright.exe --ui-test-settings` (an isolated in-memory Settings object
/// -- never touches the real settings.json). Covers the exact mode-switching
/// behavior that turned out brittle to poke by hand with raw UI Automation.
///
/// Note: AmbientCard/TransitionCard (plain XAML &lt;Border&gt; elements) never
/// get a WPF automation peer at all -- Border doesn't override
/// OnCreateAutomationPeer(), so it's invisible to UI Automation regardless of
/// Visibility. Assertions below check a real Control inside each card
/// instead (e.g. AmbientCameraSelector, TransitionInstantBtn).
/// </summary>
public class SettingsWindowTests
{
    [Fact]
    public void SwitchingToAmbientMode_ShowsCameraCardAndHidesOffHelperText()
    {
        using var app = DdcBrightApp.Launch("--ui-test-settings");
        var window = app.MainWindow;

        var ambientBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("AutoAmbientBtn"))!;
        app.ClickUntil(ambientBtn,
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("AmbientCameraSelector")) is not null);

        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("AmbientTestButton")));
        Assert.Null(window.FindFirstDescendant(cf => cf.ByAutomationId("OffModeHelperText")));
    }

    [Fact]
    public void SwitchingToScheduleMode_ShowsTransitionCard()
    {
        using var app = DdcBrightApp.Launch("--ui-test-settings");
        var window = app.MainWindow;

        var scheduleBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("AutoScheduleBtn"))!;
        app.ClickUntil(scheduleBtn,
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("TransitionInstantBtn")) is not null);

        Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("TransitionGradualBtn")));
        Assert.Null(window.FindFirstDescendant(cf => cf.ByAutomationId("OffModeHelperText")));
    }

    [Fact]
    public void ClickingTestNow_UpdatesResultText()
    {
        using var app = DdcBrightApp.Launch("--ui-test-settings");
        var window = app.MainWindow;

        var ambientBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("AutoAmbientBtn"))!;
        app.ClickUntil(ambientBtn,
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("AmbientTestButton")) is not null);

        var testButton = window.FindFirstDescendant(cf => cf.ByAutomationId("AmbientTestButton"))!;
        var resultElement = window.FindFirstDescendant(cf => cf.ByAutomationId("AmbientTestResultText"))!;

        // AmbientTestButton_Click sets the text to "Testing…" synchronously,
        // before the (slow, up to 8s) capture even starts -- a fast, cheap
        // signal that the click itself landed, independent of how long the
        // real capture then takes to settle.
        app.ClickUntil(testButton, () => !string.IsNullOrEmpty(resultElement.Name));

        // The sensor's own capture timeout is 8s -- give it comfortable room
        // for the result text to settle past the "Testing…" placeholder.
        Retry.WhileTrue(() => string.IsNullOrEmpty(resultElement.Name) || resultElement.Name == "Testing…",
            TimeSpan.FromSeconds(15));

        Assert.False(string.IsNullOrEmpty(resultElement.Name));
        Assert.NotEqual("Testing…", resultElement.Name);
    }

    [Fact]
    public void ClickingAlreadySelectedSegment_StaysChecked()
    {
        using var app = DdcBrightApp.Launch("--ui-test-settings");
        var window = app.MainWindow;

        // Off is the default in a fresh in-memory Settings -- click it again
        // and confirm SegmentedControlHelper's "keep it checked" branch holds,
        // rather than a plain ToggleButton's default click-to-uncheck behavior.
        var offBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("AutoOffBtn"))!;
        Assert.Equal(ToggleState.On, offBtn.Patterns.Toggle.Pattern.ToggleState.Value);

        app.ClickUntil(offBtn, () => offBtn.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On);

        Assert.Equal(ToggleState.On, offBtn.Patterns.Toggle.Pattern.ToggleState.Value);
    }
}
