using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Backup;
using SmartSchoolTimetable.Infrastructure.Persistence;

namespace SmartSchoolTimetable.Api;

/// <summary>The last log lines of this run, kept in memory only so «تصدير سجل الدعم» can include them. Nothing is written to disk.</summary>
public sealed class SupportLogBuffer
{
    private const int Capacity = 300;
    private readonly ConcurrentQueue<string> lines = new();

    public void Add(string line)
    {
        lines.Enqueue(line);
        while (lines.Count > Capacity && lines.TryDequeue(out _))
        {
            // Oldest lines drop out.
        }
    }

    public IReadOnlyList<string> Snapshot() => [.. lines];
}

/// <summary>Feeds Information-and-above log messages into the <see cref="SupportLogBuffer"/> (messages never contain secrets, see <c>LocalLog</c>).</summary>
public sealed class SupportLogProvider(SupportLogBuffer buffer, TimeProvider clock) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new BufferLogger(buffer, clock, categoryName);

    public void Dispose()
    {
    }

    private sealed class BufferLogger(SupportLogBuffer buffer, TimeProvider clock, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (!IsEnabled(logLevel))
                return;
            var name = category.Contains('.', StringComparison.Ordinal) ? category[(category.LastIndexOf('.') + 1)..] : category;
            var line = $"{clock.GetUtcNow():yyyy-MM-dd HH:mm:ss}Z [{logLevel}] {name}: {formatter(state, exception)}";
            if (exception is not null)
                line += $" ({exception.GetType().Name}: {exception.Message})";
            buffer.Add(line.Replace('\r', ' ').Replace('\n', ' '));
        }
    }
}

/// <summary>Removes what identifies this computer or person (the Windows user name, the machine name, home-folder paths) from text that leaves the app.</summary>
public static partial class PathRedactor
{
    // The profile folder name may contain spaces, so it runs to the next separator or the end of the line (over-redacting is the safe direction).
    [GeneratedRegex(@"(?i)[A-Z]:[\\/]+Users[\\/]+[^\\/""'<>|:*?\r\n]+")]
    private static partial Regex UserProfile();

    public static string Redact(string text, string? userName = null, string? machineName = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = UserProfile().Replace(text, @"C:\Users\<user>");
        foreach (var (value, replacement) in new[] { (userName ?? Environment.UserName, "<user>"), (machineName ?? Environment.MachineName, "<machine>") })
        {
            if (value.Length >= 3)
                result = result.Replace(value, replacement, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
}

/// <summary>
/// «تصدير سجل الدعم»: a plain-text report for the developer: versions, environment, database size and per-table row counts (numbers only, never
/// content), backup settings, and the recent log lines. Every user-specific path or name is redacted; no password, code, cookie or token is ever
/// present in the data it reads.
/// </summary>
public sealed class SupportLogBuilder(LocalDatabaseLocation location, IDatabaseBackup backup, SupportLogBuffer buffer, BackupService backups, TimeProvider clock)
{
    public async Task<string> BuildAsync(CancellationToken token)
    {
        var text = new StringBuilder();
        text.AppendLine("Smart School Timetable - support log");
        text.AppendLine(CultureInfo.InvariantCulture, $"Generated (UTC): {clock.GetUtcNow():yyyy-MM-dd HH:mm:ss}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Application version: {AppInfo.Version} (build {AppInfo.BuildDate})");
        text.AppendLine(CultureInfo.InvariantCulture, $"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture}), process {RuntimeInformation.ProcessArchitecture}");
        text.AppendLine(CultureInfo.InvariantCulture, $".NET: {RuntimeInformation.FrameworkDescription}");
        text.AppendLine(CultureInfo.InvariantCulture, $"CPU cores: {Environment.ProcessorCount}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Memory (working set, MB): {Environment.WorkingSet / 1_048_576}");
        text.AppendLine(CultureInfo.InvariantCulture, $"GC heap (MB): {GC.GetTotalMemory(false) / 1_048_576}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Culture: {CultureInfo.CurrentCulture.Name}; time zone: {TimeZoneInfo.Local.Id}");
        text.AppendLine();
        var migrations = backup.KnownMigrations;
        text.AppendLine(CultureInfo.InvariantCulture, $"Schema version: {migrations.Count} migrations, last: {(migrations.Count > 0 ? migrations[^1] : "none")}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Data folder: {DisplayFolder(Path.GetDirectoryName(location.DatabasePath)!)}");
        if (File.Exists(location.DatabasePath))
            text.AppendLine(CultureInfo.InvariantCulture, $"Database size (KB): {new FileInfo(location.DatabasePath).Length / 1024}");
        var settings = await backups.GetSettingsAsync(token);
        text.AppendLine(CultureInfo.InvariantCulture, $"Automatic backup: {(settings.AutoBackupEnabled ? "on" : "off")}, keep last {settings.KeepLast}, last at {settings.LastAutoBackupAt?.ToString("u", CultureInfo.InvariantCulture) ?? "never"}");
        text.AppendLine();
        text.AppendLine("Row counts per table (numbers only):");
        foreach (var (table, count) in await RowCountsAsync(token))
            text.AppendLine(CultureInfo.InvariantCulture, $"  {table}: {count}");
        text.AppendLine();
        text.AppendLine("Recent log lines (this run):");
        var lines = buffer.Snapshot();
        if (lines.Count == 0)
            text.AppendLine("  (none)");
        foreach (var line in lines)
            text.Append("  ").AppendLine(line);
        return PathRedactor.Redact(text.ToString());
    }

    /// <summary>The data folder as %LOCALAPPDATA%\... when it lives there, so the file never shows a user name.</summary>
    internal static string DisplayFolder(string folder)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return !string.IsNullOrEmpty(local) && folder.StartsWith(local, StringComparison.OrdinalIgnoreCase)
            ? "%LOCALAPPDATA%" + folder[local.Length..]
            : folder;
    }

    private async Task<List<(string Table, long Count)>> RowCountsAsync(CancellationToken token)
    {
        var result = new List<(string, long)>();
        try
        {
            await using var connection = new SqliteConnection(location.ConnectionString);
            await connection.OpenAsync(token);
            var tables = new List<string>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
                await using var reader = await list.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                    tables.Add(reader.GetString(0));
            }
            foreach (var table in tables.Where(name => !name.StartsWith("__", StringComparison.Ordinal) && !name.Equals("Users", StringComparison.OrdinalIgnoreCase)))
            {
                await using var count = connection.CreateCommand();
                count.CommandText = $"SELECT COUNT(*) FROM \"{table.Replace("\"", "\"\"", StringComparison.Ordinal)}\";";
                result.Add((table, Convert.ToInt64(await count.ExecuteScalarAsync(token), CultureInfo.InvariantCulture)));
            }
        }
        catch (SqliteException)
        {
            // A report with fewer details is better than none.
        }
        return result;
    }
}
