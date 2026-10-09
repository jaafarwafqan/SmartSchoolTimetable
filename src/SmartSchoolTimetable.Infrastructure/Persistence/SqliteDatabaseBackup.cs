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
        await using var connection = new SqliteConnection(location.ConnectionString);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $path;";
        command.Parameters.AddWithValue("$path", targetFile);
        await command.ExecuteNonQueryAsync(token);
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
            return new BackupInspection(true, migrations, hasOwner);
        }
        catch (SqliteException)
        {
            return new BackupInspection(false, [], false);
        }
    }

    public async Task RestoreAsync(string file, CancellationToken token)
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
}
