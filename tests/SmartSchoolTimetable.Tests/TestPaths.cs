namespace SmartSchoolTimetable.Tests;

internal static class TestPaths
{
    public static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SmartSchoolTimetable.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate SmartSchoolTimetable.sln from the test output directory.");
    }

    public static string FrontendFile(params string[] segments) =>
        Path.Combine([FindRepositoryRoot(), "frontend", .. segments]);

    /// <summary>Hand-written backend C# files under src/ (build output and EF migrations excluded).</summary>
    public static IEnumerable<string> BackendSourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var normalized = path.Replace('\\', '/');
                return !normalized.Contains("/bin/", StringComparison.Ordinal) &&
                    !normalized.Contains("/obj/", StringComparison.Ordinal) &&
                    !normalized.Contains("/Migrations/", StringComparison.Ordinal);
            });
}
