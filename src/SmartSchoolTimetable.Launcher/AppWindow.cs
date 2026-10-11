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

    public AppWindow(string origin, string userData, Icon icon)
    {
        this.origin = origin;
        this.userData = userData;
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
        var environment = await CoreWebView2Environment.CreateAsync(null, userData);
        await view.EnsureCoreWebView2Async(environment);
        var core = view.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsWebMessageEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.NavigationStarting += (_, e) => e.Cancel = !SameOrigin(e.Uri);
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true; // never a second window; a link inside the program opens in this one
            if (SameOrigin(e.Uri))
                core.Navigate(e.Uri);
        };
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.Navigate(origin + "/");
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
