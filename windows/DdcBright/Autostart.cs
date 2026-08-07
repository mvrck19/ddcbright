using Microsoft.Win32;
using System.IO;

namespace DdcBright;

public static class Autostart
{
    private const string AppName = "ddcbright";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Best-effort; a failure here should never stop the app from starting.</summary>
    public static void Register()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;

            var command = $"\"{exePath}\"";

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (key?.GetValue(AppName) as string == command)
                return;

            key?.SetValue(AppName, command);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
        }
    }
}
