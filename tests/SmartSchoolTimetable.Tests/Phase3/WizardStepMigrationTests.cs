using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartSchoolTimetable.Infrastructure;

namespace SmartSchoolTimetable.Tests.Phase3;

/// <summary>
/// Data migration Phase3EWorkloadWizardStep (Phase 3 finish A5/C5): the wizard got step 7 «الأنصبة», so the old
/// review step 7 (bit 128) moves to step 8 (bit 256). Checked on a finished wizard, one in the middle, a database
/// without progress, and back down.
/// </summary>
public sealed class WizardStepMigrationTests
{
    private const string Previous = "_Phase3CWorkload";
    private const string Target = "_Phase3EWorkloadWizardStep";

    private sealed record Progress(int Current, int Completed, int Skipped, int Version);

    private static async Task<Progress?> RunAsync(Func<LocalDbContext, Task> seed, Func<IMigrator, LocalDbContext, Task> act)
    {
        var directory = Directory.CreateTempSubdirectory("wizard-migration-");
        try
        {
            var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite($"Data Source={Path.Combine(directory.FullName, "test.db")};Pooling=False").Options;
            await using var db = new LocalDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(Name(db, Previous));
            await seed(db);
            await act(migrator, db);
            var rows = await db.Database.SqlQueryRaw<Progress>("SELECT \"CurrentStep\" AS \"Current\", \"CompletedMask\" AS \"Completed\", \"SkippedMask\" AS \"Skipped\", \"Version\" FROM \"SetupProgress\"").ToListAsync();
            return rows.SingleOrDefault();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            directory.Delete(recursive: true);
        }
    }

    private static string Name(LocalDbContext db, string suffix) => db.Database.GetMigrations().Single(name => name.EndsWith(suffix, StringComparison.Ordinal));

    private static Func<LocalDbContext, Task> Row(int current, int completed, int skipped, bool finished) => db =>
        db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"SetupProgress\" (\"Id\", \"CurrentStep\", \"CompletedMask\", \"SkippedMask\", \"IsFinished\", \"UpdatedAt\", \"Version\") VALUES (1, {0}, {1}, {2}, {3}, '2026-10-05 10:00:00+00:00', 1)",
            current, completed, skipped, finished);

    private static Task Up(IMigrator migrator, LocalDbContext db) => migrator.MigrateAsync(Name(db, Target));

    private static async Task UpThenDown(IMigrator migrator, LocalDbContext db)
    {
        await Up(migrator, db);
        await migrator.MigrateAsync(Name(db, Previous));
    }

    // Old layout: bit (1 << step) for steps 1–7; step 6 (teachers) skipped, 7 = review.
    private const int OldFinishedCompleted = 2 | 4 | 8 | 16 | 32 | 128;
    private const int TeachersSkipped = 64;

    [Fact]
    public async Task AFinishedWizardKeepsItsReviewAsStepEight()
    {
        var progress = await RunAsync(Row(7, OldFinishedCompleted, TeachersSkipped, true), Up);
        Assert.Equal(new Progress(8, 2 | 4 | 8 | 16 | 32 | 256, TeachersSkipped, 2), progress); // the new step 7 is still to do
    }

    [Fact]
    public async Task AWizardInTheMiddleIsNotTouched()
    {
        var progress = await RunAsync(Row(4, 2 | 4 | 8, 0, false), Up);
        Assert.Equal(new Progress(4, 2 | 4 | 8, 0, 1), progress);
    }

    [Fact]
    public async Task ADatabaseWithoutProgressStaysEmpty()
    {
        Assert.Null(await RunAsync(_ => Task.CompletedTask, Up));
    }

    [Fact]
    public async Task DownRestoresTheSevenStepMasks()
    {
        var progress = await RunAsync(Row(7, OldFinishedCompleted, TeachersSkipped, true), UpThenDown);
        Assert.Equal(new Progress(7, OldFinishedCompleted, TeachersSkipped, 3), progress);
    }

    [Fact]
    public async Task DownDoesNotTurnADoneWorkloadStepIntoADoneReview()
    {
        // New layout after Up: steps 1–5 done, the workload step 7 (bit 128) done, the review (bit 256) not yet.
        var progress = await RunAsync(Row(8, 2 | 4 | 8 | 16 | 32 | 128, 0, false), async (migrator, db) =>
        {
            await migrator.MigrateAsync(Name(db, Target));
            await db.Database.ExecuteSqlRawAsync("UPDATE \"SetupProgress\" SET \"CompletedMask\" = {0}, \"CurrentStep\" = 8", 2 | 4 | 8 | 16 | 32 | 128);
            await migrator.MigrateAsync(Name(db, Previous));
        });
        Assert.Equal((7, 2 | 4 | 8 | 16 | 32), (progress!.Current, progress.Completed)); // the old review is NOT marked done
    }
}
