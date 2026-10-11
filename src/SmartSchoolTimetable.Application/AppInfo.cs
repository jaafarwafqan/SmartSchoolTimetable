using System.Reflection;

namespace SmartSchoolTimetable.Application;

/// <summary>The application version (from the assembly), shown in the app, recorded with backups and in the support log.</summary>
public static class AppInfo
{
    /// <summary>For example "5.0.0": the informational version without any source-control suffix.</summary>
    public static string Version { get; } = (typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0").Split('+')[0];
}
