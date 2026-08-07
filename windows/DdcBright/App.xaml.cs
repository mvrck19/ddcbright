using System.Windows;

namespace DdcBright;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private FlyoutWindow? _flyout;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Autostart.Register();

        _flyout = new FlyoutWindow();

        using var iconStream = System.Reflection.Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("sun.ico")!;
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream),
            Visible = true,
            Text = "DDCBright",
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("About", null, (_, _) => ShowAbout());
        menu.Items.Add("Quit", null, (_, _) => Shutdown());
        _trayIcon.ContextMenuStrip = menu;

        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
            {
                _flyout!.ShowNearCursor();
            }
        };
    }

    private static void ShowAbout()
    {
        System.Windows.MessageBox.Show(
            "This is a brightness control application.\n\n" +
            "For more information, please visit the project's GitHub page:\n" +
            "https://github.com/mvrck19/ddcbright\n\n" +
            "Icon made by Iconic Panda from Flaticon:\n" +
            "https://www.flaticon.com/free-icons/brightness",
            "About DDCBright");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
