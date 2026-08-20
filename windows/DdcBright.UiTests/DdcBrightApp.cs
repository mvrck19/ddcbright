using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace DdcBright.UiTests;

/// <summary>
/// Launches ddcbright.exe with a given CLI flag and wraps FlaUI's
/// Application/Automation/Window so tests can drive it with real synthetic
/// input, the same way a user would -- unlike raw UI Automation property
/// pokes (TogglePattern.Toggle(), etc.), FlaUI's Click() goes through an
/// actual click sequence that the app's real Click-event handlers see.
/// </summary>
public sealed class DdcBrightApp : IDisposable
{
    private readonly Application _app;
    private readonly UIA3Automation _automation;
    private readonly string _isolatedSettingsPath;

    public Window MainWindow { get; }

    private DdcBrightApp(Application app, UIA3Automation automation, Window mainWindow, string isolatedSettingsPath)
    {
        _app = app;
        _automation = automation;
        MainWindow = mainWindow;
        _isolatedSettingsPath = isolatedSettingsPath;
    }

    /// <summary>
    /// Launches the app built alongside this test assembly with the given
    /// CLI argument (e.g. "--ui-test-settings") and waits for its window.
    /// Runs with DDCBRIGHT_SETTINGS_PATH pointed at a throwaway file -- the
    /// app's Settings.Save() always writes to one fixed path regardless of
    /// which Settings instance calls it, so without this override a test
    /// clicking through modes would overwrite the real settings.json (this
    /// happened for real the first time this harness ran).
    /// </summary>
    public static DdcBrightApp Launch(string args)
    {
        var isolatedSettingsPath = Path.Combine(Path.GetTempPath(), $"ddcbright_uitest_settings_{Guid.NewGuid():N}.json");

        var startInfo = new ProcessStartInfo(FindExePath(), args) { UseShellExecute = false };
        startInfo.Environment["DDCBRIGHT_SETTINGS_PATH"] = isolatedSettingsPath;

        var app = Application.Launch(startInfo);
        var automation = new UIA3Automation();
        var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(10))
            ?? throw new TimeoutException("ddcbright.exe did not show a window within 10s.");

        BringToForeground(window);

        return new DdcBrightApp(app, automation, window, isolatedSettingsPath);
    }

    /// <summary>
    /// FlaUI doesn't bring a freshly-launched window to the foreground on its
    /// own, and Windows' LockSetForegroundWindow policy silently no-ops a
    /// background process's SetForegroundWindow call outright -- a click can
    /// then land on whatever window is actually on top at that screen
    /// position instead of the target (this really happened here: clicks
    /// landed on an editor window sitting over the same screen region).
    /// Tapping Alt first is the standard workaround -- it resets the input
    /// idle time Windows checks, which then allows the very next
    /// SetForegroundWindow to actually succeed.
    /// </summary>
    private static void BringToForeground(Window window)
    {
        Keyboard.Press(VirtualKeyShort.ALT);
        Keyboard.Release(VirtualKeyShort.ALT);
        window.SetForeground();
    }

    /// <summary>
    /// Clicks <paramref name="button"/> and waits for <paramref name="condition"/>
    /// to become true, retrying the click a few times first -- even with
    /// <see cref="BringToForeground"/>, an occasional click still doesn't
    /// land (observed directly while building this harness). Retrying is
    /// safe: it only papers over the OS-level focus race, not an app bug --
    /// if the app were actually broken, retries would still never satisfy
    /// <paramref name="condition"/> and this throws.
    /// </summary>
    public void ClickUntil(AutomationElement button, Func<bool> condition, int maxAttempts = 5)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            BringToForeground(MainWindow);
            Thread.Sleep(200);
            button.Click();
            if (Retry.WhileFalse(condition, TimeSpan.FromSeconds(2)).Success)
                return;
        }
        throw new TimeoutException($"Condition not met after {maxAttempts} click attempts on {button.Name}.");
    }

    public void Dispose()
    {
        _automation.Dispose();
        _app.Kill(); // tests always launch via a --ui-test-* flag that never
                     // calls Shutdown() itself, so nothing else closes this
        _app.Dispose();
        try { File.Delete(_isolatedSettingsPath); } catch (IOException) { }
    }

    private static string FindExePath()
    {
        // Test bin layout: windows/DdcBright.UiTests/bin/<Config>/net8.0-windows/
        // App bin layout:  windows/DdcBright/bin/<Config>/net8.0-windows10.0.19041.0/win-x64/ddcbright.exe
        // The ProjectReference in DdcBright.UiTests.csproj (build-order only)
        // guarantees the app is freshly built under the same <Config>.
        var configDir = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!;
        var config = configDir.Name;
        var windowsDir = configDir.Parent!.Parent!.Parent!;
        var exePath = Path.Combine(windowsDir.FullName, "DdcBright", "bin", config,
            "net8.0-windows10.0.19041.0", "win-x64", "ddcbright.exe");

        if (!File.Exists(exePath))
            throw new FileNotFoundException(
                $"Built app not found at {exePath} -- run `dotnet build windows/DdcBright/DdcBright.csproj -c {config}` first.",
                exePath);
        return exePath;
    }
}
