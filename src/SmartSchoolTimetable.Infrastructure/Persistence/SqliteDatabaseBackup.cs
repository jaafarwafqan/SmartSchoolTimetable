using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartSchoolTimetable.Application.Backup;

namespace SmartSchoolTimetable.Infrastructure.Persistence;

/// <summary>Where the live database is (registered once with the connection string).</summary>
public sealed record LocalDatabaseLocation(string DatabasePath, string ConnectionString);

/// <summary>
/// SQLite backup and restore (ADR 0042). A backup is <c>VACUUM INTO</c> a new file: a consistent copy even while the
/// app runs. A restore copies the chosen file into the live database with SQLite's online backup API (no file is
/// deleted or moved), then applies any newer migrations and turns WAL back on.
/// </summary>
public sealed class SqliteDatabaseBackup(LocalDatabaseLocation location, LocalDbContext db) : IDatabaseBackup
{
    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();

    public string AutomaticBackupFolder => Path.Combine(Path.GetDirectoryName(location.DatabasePath)!, "backups");

    public IReadOnlyList<string> KnownMigrations => db.Database.GetMigrations().ToArray();

    public async Task CreateAsync(string targetFile, CancellationToken token)
    {
        if (File.Exists(targetFile))
            throw new IOException("The backup target already exists.");
        try
        {
            // SQLite's online backup API: a consistent copy of the live database, never a raw file copy.
            await using var source = new SqliteConnection(location.ConnectionString);
            await source.OpenAsync(token);
            var destinationConnection = new SqliteConnectionStringBuilder { DataSource = targetFile, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString();
            await using var destination = new SqliteConnection(destinationConnection);
            await destination.OpenAsync(token);
            await Task.Run(() => source.BackupDatabase(destination), token);
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            // The file was created by this attempt (it did not exist before): leave no half-written "backup" behind.
            TryDeletePartial(targetFile);
            throw exception is SqliteException ? new IOException("The database backup could not be written.", exception) : exception;
        }
    }

    private static void TryDeletePartial(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing more can be done; the next listing shows it as not restorable.
        }
    }

    public async Task<BackupInspection> InspectAsync(string file, CancellationToken token)
    {
        var header = new byte[SqliteHeader.Length];
        await using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            if (await stream.ReadAsync(header, token) != header.Length || !header.AsSpan().SequenceEqual(SqliteHeader))
                return new BackupInspection(false, [], false);
        }
        try
        {
            var readOnly = new SqliteConnectionStringBuilder { DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
            await using var connection = new SqliteConnection(readOnly);
            await connection.OpenAsync(token);
            var migrations = new List<string>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";";
                await using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                    migrations.Add(reader.GetString(0));
            }
            await using var owners = connection.CreateCommand();
            owners.CommandText = "SELECT COUNT(*) FROM \"Users\";";
            var hasOwner = Convert.ToInt64(await owners.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) > 0;
            // PRAGMA integrity_check on this copy: one row "ok", or a list of problems.
            await using var integrity = connection.CreateCommand();
            integrity.CommandText = "PRAGMA integrity_check;";
            var rows = new List<string>();
            await using (var reader = await integrity.ExecuteReaderAsync(token))
            {
                while (await reader.ReadAsync(token))
                    rows.Add(reader.GetString(0));
            }
            return new BackupInspection(true, migrations, hasOwner, rows is ["ok"], await ReadAppVersionAsync(connection, token));
        }
        catch (SqliteException)
        {
            return new BackupInspection(false, [], false);
        }
    }

    /// <summary>The application version that last ran on the database the copy was made from (older copies have none).</summary>
    private static async Task<string?> ReadAppVersionAsync(SqliteConnection connection, CancellationToken token)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"LastAppVersion\" FROM \"BackupSettings\" LIMIT 1;";
            return await command.ExecuteScalarAsync(token) as string;
        }
        catch (SqliteException)
        {
            return null; // a copy made before the setting existed
        }
    }

    public async Task RestoreAsync(string file, CancellationToken token)
    {
        try
        {
            var readOnly = new SqliteConnectionStringBuilder { DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
            await using (var source = new SqliteConnection(readOnly))
            await using (var target = new SqliteConnection(location.ConnectionString))
            {
                await source.OpenAsync(token);
                await target.OpenAsync(token);
                source.BackupDatabase(target);
            }
            await db.Database.MigrateAsync(token);
            await using var connection = new SqliteConnection(location.ConnectionString);
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            await command.ExecuteNonQueryAsync(token);
        }
        catch (SqliteException exception)
        {
            // The Application layer knows only IOException (it has no SQLite types).
            throw new IOException("The database could not be restored from the chosen file.", exception);
        }
    }
}
