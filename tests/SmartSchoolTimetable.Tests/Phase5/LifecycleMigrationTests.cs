using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartSchoolTimetable.Infrastructure;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>
/// The corrective migration <c>Phase5Lifecycle</c> on a temporary database: data written under the previous schema keeps its
/// meaning (the approved version becomes «معتمد», the others drafts), the new checks hold, and the migration reverses.
/// </summary>
public sealed class LifecycleMigrationTests
{
    private const string Previous = "20261009205646_Phase4SessionPlans";
    private const string Current = "20261010083248_Phase5Lifecycle";

    private static async Task<long> ScalarAsync(LocalDbContext db, string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task ExistingVersionsKeepTheirMeaningAndTheMigrationReverses()
    {
        var file = Path.Combine(Path.GetTempPath(), $"smart-school-migration-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite($"Data Source={file};Pooling=False").Options;
            await using var db = new LocalDbContext(options);
            await db.Database.OpenConnectionAsync();
            await db.GetService<IMigrator>().MigrateAsync(Previous);
            db.Add(SmartSchoolTimetable.Domain.SchoolSetup.AcademicYear.Create("2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("INSERT INTO \"TimetableVersions\" (\"AcademicYearId\", \"Number\", \"Source\", \"Mode\", \"InputHash\", \"InputJson\", \"CreatedAt\", \"IsApproved\", \"Version\") VALUES (1, 1, 1, 'standard', 'h', '[]', '2026-10-09 08:00:00', 0, 0), (1, 2, 2, 'standard', 'h', '[]', '2026-10-09 09:00:00', 1, 3);");

            await db.GetService<IMigrator>().MigrateAsync(Current);
            Assert.Equal(1, await ScalarAsync(db, "SELECT \"Status\" FROM \"TimetableVersions\" WHERE \"Number\" = 1"));
            Assert.Equal(2, await ScalarAsync(db, "SELECT \"Status\" FROM \"TimetableVersions\" WHERE \"Number\" = 2"));
            Assert.Equal(1, await ScalarAsync(db, "SELECT COUNT(*) FROM \"TimetableVersions\" WHERE \"IsApproved\" = 1 AND \"Status\" = 2"));
            Assert.Equal(0, await ScalarAsync(db, "SELECT COUNT(*) FROM \"GenerationRuns\" WHERE \"LockedLessons\" <> 0"));

            // The database itself refuses a status that disagrees with the approved flag, and an unknown source or status.
            foreach (var invalid in new[] { "(1, 3, 1, 'standard', 'h', '[]', '2026-10-09', 1, 0, 1)", "(1, 3, 1, 'standard', 'h', '[]', '2026-10-09', 0, 0, 2)",
                         "(1, 3, 4, 'standard', 'h', '[]', '2026-10-09', 0, 0, 1)", "(1, 3, 1, 'standard', 'h', '[]', '2026-10-09', 0, 0, 4)" })
            {
#pragma warning disable EF1002 // Fixed literals of this test, no user input.
                await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
                    $"INSERT INTO \"TimetableVersions\" (\"AcademicYearId\", \"Number\", \"Source\", \"Mode\", \"InputHash\", \"InputJson\", \"CreatedAt\", \"IsApproved\", \"Version\", \"Status\") VALUES {invalid};"));
#pragma warning restore EF1002
            }
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"TimetableVersions\" (\"AcademicYearId\", \"Number\", \"Source\", \"Mode\", \"InputHash\", \"InputJson\", \"CreatedAt\", \"IsApproved\", \"Version\", \"Status\") VALUES (1, 3, 3, 'standard', 'h', '[]', '2026-10-09', 0, 0, 3);");

            // Going back removes the new columns and keeps the rows.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"TimetableVersions\" WHERE \"Number\" = 3;");
            await db.GetService<IMigrator>().MigrateAsync(Previous);
            Assert.Equal(2, await ScalarAsync(db, "SELECT COUNT(*) FROM \"TimetableVersions\""));
            Assert.Equal(0, await ScalarAsync(db, "SELECT COUNT(*) FROM pragma_table_info('TimetableVersions') WHERE name = 'Status'"));
        }
        finally
        {
            SqliteConnectionCleanup(file);
        }
    }

    /// <summary>The test's own temporary database files only (they are created by this test in the temp folder).</summary>
    private static void SqliteConnectionCleanup(string file)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var path in new[] { file, file + "-wal", file + "-shm" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
