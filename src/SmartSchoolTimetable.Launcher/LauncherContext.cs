using System.Diagnostics;

namespace SmartSchoolTimetable.Launcher;

/// <summary>
/// The launcher's lifetime: starts the server, shows the program (own window, or the default browser when the WebView2 runtime is missing), keeps a tray
/// icon with «فتح البرنامج / نسخة احتياطية الآن / إيقاف», and brings the program forward when a second copy is started.
/// </summary>
internal sealed class LauncherContext : ApplicationContext
{
    public const string ShowEventName = @"Local\SmartSchoolTimetable.Show";

    private readonly ServerHost server = new();
    private readonly NotifyIcon tray;
    private readonly Icon icon;
    private readonly EventWaitHandle showRequest = new(false, EventResetMode.AutoReset, ShowEventName);
    private readonly bool startHidden;
    private AppWindow? window;
    private bool useWindow;
    private bool stopping;

    public LauncherContext(bool startHidden)
    {
        this.startHidden = startHidden;
        icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        tray = new NotifyIcon { Icon = icon, Text = Texts.AppName, Visible = true, ContextMenuStrip = BuildMenu() };
        tray.DoubleClick += (_, _) => Open();
        var waiter = new Thread(WaitForSecondCopy) { IsBackground = true };
        waiter.Start();
        // The UI thread is free as soon as the form loop starts: the server is started from the first idle moment.
        Application.Idle += StartOnce;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
        menu.Items.Add(Texts.Open, null, (_, _) => Open());
        menu.Items.Add(Texts.BackupNow, null, async (_, _) => await BackupNowAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Texts.Stop, null, async (_, _) => await StopAsync());
        return menu;
    }

    private async void StartOnce(object? sender, EventArgs e)
    {
        Application.Idle -= StartOnce;
        tray.ShowBalloonTip(2000, Texts.AppName, Texts.Starting, ToolTipIcon.Info);
        var problem = await server.StartAsync();
        if (problem is not null)
        {
            MessageBox.Show(problem, Texts.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            Quit();
            return;
        }
        useWindow = AppWindow.RuntimeAvailable();
        if (!useWindow)
            tray.ShowBalloonTip(5000, Texts.AppName, Texts.WebViewMissing, ToolTipIcon.Info);
        if (!startHidden)
            Open();
    }

    /// <summary>Opens (or brings forward) the program: the own window, or the default browser.</summary>
    public void Open()
    {
        if (server.Port == 0)
            return;
        if (!useWindow)
        {
            Process.Start(new ProcessStartInfo(server.Origin + "/") { UseShellExecute = true });
            return;
        }
        if (window is null)
        {
            window = new AppWindow(server.Origin, Path.Combine(ServerHost.DataFolder, "WebView2"), icon);
            window.FormClosed += (_, _) => window = null;
            window.HiddenToTray += (_, _) => tray.ShowBalloonTip(2500, Texts.AppName, Texts.RunningInTray, ToolTipIcon.Info);
            window.Show();
            _ = window.LoadAsync();
        }
        window.Bring();
    }

    private async Task BackupNowAsync()
    {
        var done = await server.BackupNowAsync();
        tray.ShowBalloonTip(3000, Texts.AppName, done ? Texts.BackupDone : Texts.BackupFailed, done ? ToolTipIcon.Info : ToolTipIcon.Warning);
    }

    private async Task StopAsync()
    {
        var answer = MessageBox.Show(Texts.StopConfirm, Texts.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        if (answer != DialogResult.Yes)
            return;
        await server.StopAsync();
        Quit();
    }

    private void Quit()
    {
        if (stopping)
            return;
        stopping = true;
        tray.Visible = false;
        if (window is not null)
        {
            window.FormClosing += (_, e) => e.Cancel = false;
            window.Dispose();
        }
        server.Dispose();
        ExitThread();
    }

    private void WaitForSecondCopy()
    {
        while (!stopping)
        {
            if (showRequest.WaitOne(1000))
                tray.ContextMenuStrip?.BeginInvoke(Open);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            tray.Dispose();
            showRequest.Dispose();
            icon.Dispose();
            server.Dispose();
        }
        base.Dispose(disposing);
    }
}
