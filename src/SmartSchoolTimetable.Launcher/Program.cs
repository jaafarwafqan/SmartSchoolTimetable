using SmartSchoolTimetable.Launcher;

internal static class Program
{
    private const string InstanceName = @"Local\SmartSchoolTimetable.Launcher";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (!Environment.Is64BitOperatingSystem)
        {
            MessageBox.Show(Texts.Needs64Bit, Texts.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return;
        }
        // One copy per Windows session: a second start asks the first to show the program and ends.
        using var instance = new Mutex(true, InstanceName, out var first);
        if (!first)
        {
            using var request = new EventWaitHandle(false, EventResetMode.AutoReset, LauncherContext.ShowEventName);
            request.Set();
            return;
        }
        Application.Run(new LauncherContext(startHidden: args.Contains("--tray", StringComparer.OrdinalIgnoreCase)));
    }
}
