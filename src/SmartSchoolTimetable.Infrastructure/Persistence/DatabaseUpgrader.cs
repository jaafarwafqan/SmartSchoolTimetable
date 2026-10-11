using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SmartSchoolTimetable.Infrastructure.Persistence;

/// <summary>Why the start-up upgrade of the database stopped (the launcher shows an Arabic message for each).</summary>
public enum DatabaseUpgradeFailure
{
    /// <summary>The safety copy could not be written or did not pass the integrity check; the database was not touched.</summary>
    BackupFailed,

    /// <summary>A migration failed; the database was put back from the safety copy.</summary>
    MigrationFailedRestored,

    /// <summary>A migration failed and the safety copy could not be put back; the copy is kept and named in the message.</summary>
    MigrationFailedNotRestored,

    /// <summary>The data was written by a newer version of the program than this one; nothing was changed.</summary>
    DatabaseNewerThanProgram,
}

public sealed class DatabaseUpgradeException(DatabaseUpgradeFailure failure, string? backupFile, Exception? inner)
    : Exception($"The database upgrade stopped: {failure}.", inner)
{
    public DatabaseUpgradeFailure Failure { get; } = failure;

    /// <summary>The <c>pre-upgrade-*.db</c> safety copy, when one was made.</summary>
    public string? BackupFile { get; } = backupFile;
}

/// <summary>
/// Safe upgrade (M2, docs/UPGRADE_AR.md): before any pending migration is applied to a database that already holds data, a verified safety
/// copy <c>backups\pre-upgrade-yyyyMMdd-HHmmss.db</c> is written next to the database (never pruned, never overwritten). If a migration fails the
/// copy is put back, so the data is exactly as it was before the new version started.
/// </summary>
public static class DatabaseUpgrader
{
    public const string BackupPrefix = "pre-upgrade-";

    /// <param name="migrate">Applies the pending migrations (the production call is <c>db.Database.MigrateAsync</c>; tests inject a failing one).</param>
    /// <returns>The safety copy that was made, or null when there was nothing to upgrade.</returns>
    public static async Task<string?> MigrateSafelyAsync(LocalDbContext db, LocalDatabaseLocation location, DateTimeOffset now, Func<CancellationToken, Task>? migrate = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(location);
        migrate ??= db.Database.MigrateAsync;
        var known = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        var applied = (await db.Database.GetAppliedMigrationsAsync(token)).ToList();
        if (applied.Any(migration => !known.Contains(migration)))
            throw new DatabaseUpgradeException(DatabaseUpgradeFailure.DatabaseNewerThanProgram, null, null);
        var pending = (await db.Database.GetPendingMigrationsAsync(token)).Any();
        if (applied.Count == 0 || !pending)
        {
            // A new database has nothing to protect, and a current one nothing to upgrade.
            await migrate(token);
            return null;
        }

        var folder = Path.Combine(Path.GetDirectoryName(location.DatabasePath)!, "backups");
        string copy;
        try
        {
            Directory.CreateDirectory(folder);
            copy = UniqueName(folder, now);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DatabaseUpgradeException(DatabaseUpgradeFailure.BackupFailed, null, exception);
        }
        try
        {
            await WriteVerifiedCopyAsync(location, copy, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException)
        {
            TryDelete(copy); // the name did not exist a moment ago: this is only the partial file of this attempt
            throw new DatabaseUpgradeException(DatabaseUpgradeFailure.BackupFailed, null, exception);
        }

        try
        {
            await migrate(token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            try
            {
                await CopyIntoAsync(copy, location.ConnectionString, token);
            }
            catch (Exception restoreFailure) when (restoreFailure is IOException or UnauthorizedAccessException or SqliteException)
            {
                throw new DatabaseUpgradeException(DatabaseUpgradeFailure.MigrationFailedNotRestored, copy, exception);
            }
            throw new DatabaseUpgradeException(DatabaseUpgradeFailure.MigrationFailedRestored, copy, exception);
        }
        return copy;
    }

    /// <summary>A name no existing file has (a second start within the same second gets a numbered one); an existing file is never reused.</summary>
    private static string UniqueName(string folder, DateTimeOffset now)
    {
        var stem = $"{BackupPrefix}{now.ToLocalTime():yyyyMMdd-HHmmss}";
        var candidate = Path.Combine(folder, stem + ".db");
        for (var number = 2; File.Exists(candidate); number++)
            candidate = Path.Combine(folder, $"{stem}-{number}.db");
        return candidate;
    }

    private static async Task WriteVerifiedCopyAsync(LocalDatabaseLocation location, string copy, CancellationToken token)
    {
        await using (var source = new SqliteConnection(location.ConnectionString))
        {
            await source.OpenAsync(token);
            var destination = new SqliteConnectionStringBuilder { DataSource = copy, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString();
            await using var target = new SqliteConnection(destination);
            await target.OpenAsync(token);
            source.BackupDatabase(target);
        }
        var readOnly = new SqliteConnectionStringBuilder { DataSource = copy, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        await using var check = new SqliteConnection(readOnly);
        await check.OpenAsync(token);
        await using var command = check.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = (string?)await command.ExecuteScalarAsync(token);
        if (!string.Equals(result, "ok", StringComparison.Ordinal))
            throw new IOException("The safety copy did not pass the integrity check.");
    }

    private static async Task CopyIntoAsync(string copy, string liveConnectionString, CancellationToken token)
    {
        var readOnly = new SqliteConnectionStringBuilder { DataSource = copy, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        await using var source = new SqliteConnection(readOnly);
        await using var target = new SqliteConnection(liveConnectionString);
        await source.OpenAsync(token);
        await target.OpenAsync(token);
        source.BackupDatabase(target);
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file); // only the partial copy this attempt just created
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Left as it is: the next listing shows it as not restorable.
        }
    }
}
