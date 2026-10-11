using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartSchoolTimetable.Api;
using SmartSchoolTimetable.Infrastructure;
using SmartSchoolTimetable.Infrastructure.Persistence;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>
/// M2 safe upgrade on temporary databases only: a verified pre-upgrade copy before any migration, the data put back when a migration fails,
/// a database from a newer program refused untouched, and the Arabic startup messages.
/// </summary>
public sealed class SafeUpgradeTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "sst-upgrade-" + Guid.NewGuid().ToString("N"));

    public SafeUpgradeTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(folder, recursive: true); // this test's own temporary folder
    }

    private string DatabasePath => Path.Combine(folder, "timetable.db");

    private LocalDatabaseLocation Location => new(DatabasePath, new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString());

    private LocalDbContext NewContext() => new(new DbContextOptionsBuilder<LocalDbContext>().UseSqlite(Location.ConnectionString).Options);

    /// <summary>An older database: migrated only up to the second-to-last migration, with one marker row in a table every version has.</summary>
    private async Task<string> CreateOlderDatabaseAsync()
    {
        await using var db = NewContext();
        var all = db.Database.GetMigrations().ToList();
        await db.GetService<IMigrator>().MigrateAsync(all[^2]);
        await ExecuteAsync(Location.ConnectionString, "CREATE TABLE \"UpgradeMarker\" (\"Value\" TEXT NOT NULL); INSERT INTO \"UpgradeMarker\" VALUES ('بيانات المدرسة');");
        return all[^1];
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    [Fact]
    public async Task APendingMigrationIsPrecededByAVerifiedCopyThatKeepsTheOldSchemaAndData()
    {
        var newest = await CreateOlderDatabaseAsync();
        await using var db = NewContext();
        var copy = await DatabaseUpgrader.MigrateSafelyAsync(db, Location, new DateTimeOffset(2026, 10, 11, 9, 30, 0, TimeSpan.Zero));

        Assert.NotNull(copy);
        Assert.StartsWith("pre-upgrade-", Path.GetFileName(copy), StringComparison.Ordinal);
        Assert.Equal(Path.Combine(folder, "backups"), Path.GetDirectoryName(copy));
        var copyConnection = new SqliteConnectionStringBuilder { DataSource = copy, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        Assert.Equal("ok", await ScalarAsync(copyConnection, "PRAGMA integrity_check;"));
        Assert.Equal("بيانات المدرسة", await ScalarAsync(copyConnection, "SELECT \"Value\" FROM \"UpgradeMarker\";"));
        var newestCount = $"SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{newest}';";
        Assert.Equal("0", await ScalarAsync(copyConnection, newestCount)); // the copy is the OLD schema
        Assert.Equal("1", await ScalarAsync(Location.ConnectionString, newestCount)); // the live one is upgraded
    }

    [Fact]
    public async Task ACurrentOrNewDatabaseNeedsNoCopy()
    {
        await using (var fresh = NewContext())
            Assert.Null(await DatabaseUpgrader.MigrateSafelyAsync(fresh, Location, DateTimeOffset.UtcNow)); // new: nothing to protect
        await using var current = NewContext();
        Assert.Null(await DatabaseUpgrader.MigrateSafelyAsync(current, Location, DateTimeOffset.UtcNow)); // nothing pending
        Assert.False(Directory.Exists(Path.Combine(folder, "backups")));
    }

    [Fact]
    public async Task AFailedMigrationPutsTheDataBackFromTheCopyAndKeepsTheCopy()
    {
        await CreateOlderDatabaseAsync();
        await using var db = NewContext();
        var failure = await Assert.ThrowsAsync<DatabaseUpgradeException>(() => DatabaseUpgrader.MigrateSafelyAsync(db, Location, DateTimeOffset.UtcNow, async token =>
        {
            // A migration that changes the data part-way and then fails.
            await ExecuteAsync(Location.ConnectionString, "UPDATE \"UpgradeMarker\" SET \"Value\" = 'damaged'; CREATE TABLE \"HalfMigrated\" (\"Id\" INTEGER);");
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The migration failed (simulated).");
        }));

        Assert.Equal(DatabaseUpgradeFailure.MigrationFailedRestored, failure.Failure);
        Assert.True(File.Exists(failure.BackupFile));
        Assert.Equal("بيانات المدرسة", await ScalarAsync(Location.ConnectionString, "SELECT \"Value\" FROM \"UpgradeMarker\";"));
        Assert.Equal("0", await ScalarAsync(Location.ConnectionString, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'HalfMigrated';"));
        Assert.Contains("أُعيدت بياناتك", StartupFailureMessages.For(failure), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADatabaseFromANewerProgramIsRefusedWithoutChangingAnything()
    {
        await using (var db = NewContext())
            await db.Database.MigrateAsync();
        await ExecuteAsync(Location.ConnectionString, "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('29990101000000_FromTheFuture', '99.0.0');");
        var before = await File.ReadAllBytesAsync(DatabasePath);
        await using var context = NewContext();
        var failure = await Assert.ThrowsAsync<DatabaseUpgradeException>(() => DatabaseUpgrader.MigrateSafelyAsync(context, Location, DateTimeOffset.UtcNow));
        Assert.Equal(DatabaseUpgradeFailure.DatabaseNewerThanProgram, failure.Failure);
        Assert.Null(failure.BackupFile);
        Assert.Equal(before, await File.ReadAllBytesAsync(DatabasePath));
        Assert.False(Directory.Exists(Path.Combine(folder, "backups")));
    }

    [Fact]
    public async Task EveryFailureHasAnArabicMessage()
    {
        foreach (var kind in Enum.GetValues<DatabaseUpgradeFailure>())
        {
            var message = StartupFailureMessages.For(new DatabaseUpgradeException(kind, Path.Combine(folder, "backups", "pre-upgrade-x.db"), null));
            Assert.Matches("[؀-ۿ]{3}", message);
        }
        StartupFailureMessages.Write(DatabasePath, "رسالة");
        Assert.Equal("رسالة", (await File.ReadAllTextAsync(Path.Combine(folder, StartupFailureMessages.FileName))).TrimStart('﻿'));
    }
}
