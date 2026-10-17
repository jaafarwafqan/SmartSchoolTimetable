using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SmartSchoolTimetable.Launcher;

/// <summary>
/// «شاشة العرض» (ADR 0056): the staff-room screen in its own borderless full-screen window, on the second screen when one is
/// connected (the school computer usually drives the TV as an extended display), so the owner keeps working in the main window. It
/// shows only /kiosk of the local server; every other navigation and every new window is refused. Esc or the page's «إغلاق» closes it.
/// </summary>
internal sealed class KioskWindow : Form
{
    private readonly WebView2 view = new() { Dock = DockStyle.Fill };
    private readonly string origin;

    public KioskWindow(string origin, Icon icon)
    {
        this.origin = origin;
        Text = Texts.KioskName;
        Icon = icon;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        var screen = Screen.AllScreens.FirstOrDefault(item => !item.Primary) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0];
        Bounds = screen.Bounds;
        Controls.Add(view);
    }

    public async Task LoadAsync(CoreWebView2Environment environment, string uri)
    {
        ArgumentNullException.ThrowIfNull(environment);
        await view.EnsureCoreWebView2Async(environment);
        var core = view.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsWebMessageEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.NavigationStarting += (_, e) => e.Cancel = !IsKiosk(e.Uri);
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.WindowCloseRequested += (_, _) => Close();
        view.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };
        core.Navigate(uri);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.KeyCode == Keys.Escape)
            Close();
        base.OnKeyDown(e);
    }

    public bool IsKiosk(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var target)
        && string.Equals(target.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase)
        && string.Equals(target.AbsolutePath, "/kiosk", StringComparison.Ordinal);
}
