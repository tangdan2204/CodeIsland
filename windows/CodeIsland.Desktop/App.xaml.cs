
namespace CodeIsland.Desktop;

public partial class App : System.Windows.Application
{
    private NotifyIcon? tray;
    private MainWindow? window;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        window = new MainWindow();
        tray = new NotifyIcon
        {
            Text = "CodeIsland",
            Icon = ResolveTrayIcon(),
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        tray.ContextMenuStrip.Items.Add("Show", null, (_, _) => ShowWindow(expand: true));
        tray.ContextMenuStrip.Items.Add("Install hooks", null, async (_, _) => await window.InstallHooksAsync());
        tray.ContextMenuStrip.Items.Add("Settings", null, (_, _) => ShowSettings());
        tray.ContextMenuStrip.Items.Add("Export diagnostics", null, (_, _) => ExportDiagnostics());
        tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => ShutdownApp());
        tray.DoubleClick += (_, _) => ShowWindow(expand: true);
        ShowWindow(expand: false);
    }

    private void ShowSettings()
    {
        ShowWindow(expand: false);
        var settings = new SettingsWindow { Owner = window, Store = window?.Store };
        settings.Show();
        settings.Activate();
    }

    private void ExportDiagnostics()
    {
        if (window is null) return;
        var path = DiagnosticsExporter.Export(window.Store);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = true
        });
    }

    private void ShowWindow(bool expand)
    {
        if (window is null) return;
        window.Show();
        window.WindowState = System.Windows.WindowState.Normal;
        if (expand)
        {
            window.ShowExpanded();
            window.Activate();
        }
    }

    private void ShutdownApp()
    {
        window?.Store.SaveSessions();
        window?.Shutdown();
        tray?.Dispose();
        Shutdown();
    }

    private static System.Drawing.Icon ResolveTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "codeisland.ico");
        return File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : System.Drawing.SystemIcons.Application;
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        window?.Store.SaveSessions();
        tray?.Dispose();
        base.OnExit(e);
    }
}
