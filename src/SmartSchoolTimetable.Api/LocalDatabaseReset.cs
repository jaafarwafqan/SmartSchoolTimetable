namespace SmartSchoolTimetable.Api;

public static class LocalDatabaseReset
{
    public static bool DeleteAfterConfirmation(string databasePath, TextReader input, TextWriter output)
    {
        var fullPath = Path.GetFullPath(databasePath);
        output.WriteLine($"This permanently deletes the local database and its SQLite sidecar files: {fullPath}");
        output.Write("Type RESET to confirm: ");
        if (!string.Equals(input.ReadLine(), "RESET", StringComparison.Ordinal))
        {
            output.WriteLine("Reset cancelled. No files were deleted.");
            return false;
        }

        foreach (var path in new[] { fullPath, $"{fullPath}-wal", $"{fullPath}-shm", $"{fullPath}-journal" })
            File.Delete(path);

        output.WriteLine("Local database reset completed.");
        return true;
    }
}
