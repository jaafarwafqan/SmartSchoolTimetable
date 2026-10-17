using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SmartSchoolTimetable.Launcher;

/// <summary>
/// The program in its own window (WebView2, the Edge engine). It shows only the local server's origin: navigation anywhere else is cancelled, new
/// windows and permission requests are refused, developer tools are off. Closing the window only hides it (the tray keeps the program running).
/// </summary>
internal sealed class AppWindow : Form
{
    /// <summary>Raised when closing the window only hid it (the tray keeps the program running).</summary>
    public event EventHandler? HiddenToTray;

    private readonly WebView2 view = new() { Dock = DockStyle.Fill };
    private readonly string origin;
    private readonly string userData;
    private readonly Icon icon;
    private CoreWebView2Environment? environment;
    private KioskWindow? kiosk;

    public AppWindow(string origin, string userData, Icon icon)
    {
        this.origin = origin;
        this.userData = userData;
        this.icon = icon;
        Text = Texts.AppName;
        Icon = icon;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(480, 600);
        ClientSize = new Size(1280, 820);
        Controls.Add(view);
    }

    /// <summary>True when the WebView2 runtime (Edge) is installed on this computer; nothing is downloaded when it is not.</summary>
    public static bool RuntimeAvailable()
    {
        try
        {
            return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return false;
        }
    }

    public async Task LoadAsync()
    {
        environment = await CoreWebView2Environment.CreateAsync(null, userData);
        await view.EnsureCoreWebView2Async(environment);
        var core = view.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsWebMessageEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.NavigationStarting += (_, e) =>
        {
            if (SameOrigin(e.Uri))
                return;
            e.Cancel = true;
            OpenSupportLink(e.Uri);
        };
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true; // never a second browser window; a link inside the program opens in this one
            if (SameOrigin(e.Uri) && IsKioskPath(e.Uri))
                _ = OpenKioskAsync(e.Uri);
            else if (SameOrigin(e.Uri))
                core.Navigate(e.Uri);
            else
                OpenSupportLink(e.Uri);
        };
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.Navigate(origin + "/");
    }

    /// <summary>The two contact links of «حول البرنامج» (WhatsApp and Telegram) open in the default browser; every other outside address is ignored.</summary>
    private static readonly string[] SupportLinks = ["https://wa.me/9647806421980", "https://t.me/jaafar_Wafqan"];

    private static void OpenSupportLink(string uri)
    {
        var trimmed = uri.TrimEnd('/');
        if (!SupportLinks.Any(link => string.Equals(link, trimmed, StringComparison.OrdinalIgnoreCase)))
            return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(trimmed) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No default browser: the number and the user name are also shown as text on the page.
        }
    }

    private static bool IsKioskPath(string uri) => Uri.TryCreate(uri, UriKind.Absolute, out var target) && target.AbsolutePath == "/kiosk";

    /// <summary>«شاشة العرض» in its own full-screen window (one at a time: opening again shows the new key in the same window).</summary>
    private async Task OpenKioskAsync(string uri)
    {
        if (environment is null)
            return;
        kiosk?.Close();
        kiosk = new KioskWindow(origin, icon);
        kiosk.FormClosed += (_, _) => kiosk = null;
        kiosk.Show();
        try
        {
            await kiosk.LoadAsync(environment, uri);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            kiosk?.Close();
        }
    }

    private bool SameOrigin(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var target) && string.Equals(target.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase);

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            HiddenToTray?.Invoke(this, EventArgs.Empty);
        }
        base.OnFormClosing(e);
    }

    public void Bring()
    {
        Show();
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        Activate();
    }
}
