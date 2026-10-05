using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Curriculum;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Setup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Infrastructure;
using SmartSchoolTimetable.Tests.Phase2;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase25;

/// <summary>Owner model changes M1 (editable breaks, ADR 0026) and M2 (lessons per day per stage, ADR 0027).</summary>
public sealed class StageLessonsAndBreaksTests
{
    private static readonly int[] SundayToThursday = [7, 1, 2, 3, 4];

    [Fact]
    public void BreaksHaveTheirOwnDurationsAndLessonsMayHaveAGap()
    {
        var plan = new PeriodPlan(new TimeOnly(8, 0), 40, 4, [new BreakSlot(2, 20)]) { GapMinutes = 5 };
        var rows = PeriodGenerator.Generate(plan);
        Assert.Equal(
            ["08:00-08:40", "08:45-09:25", "09:25-09:45", "09:45-10:25", "10:30-11:10"],
            rows.Select(row => $"{row.StartTime:HH\\:mm}-{row.EndTime:HH\\:mm}"));
        Assert.Equal(PeriodKind.Break, rows[2].Kind);

        var shift = Shift.Create(1, "صباحي", 1);
        shift.ReplacePeriods(rows); // gaps between periods are valid rows for a shift
        Assert.Equal(4, shift.LessonCount);

        Assert.Equal(["Breaks"], Assert.Throws<DomainValidationException>(() => PeriodGenerator.Generate(new PeriodPlan(new TimeOnly(8, 0), 40, 6,
            [new BreakSlot(1, 5), new BreakSlot(2, 5), new BreakSlot(3, 5), new BreakSlot(4, 5)]))).Errors.Select(error => error.Field));
        Assert.Equal(["GapMinutes"], Assert.Throws<DomainValidationException>(() => PeriodGenerator.Generate(plan with { GapMinutes = 31 })).Errors.Select(error => error.Field));
        Assert.Throws<DomainValidationException>(() => PeriodGenerator.Generate(plan with { GapMinutes = -1 }));
        Assert.All(TemplateCatalog.Current.BreakDefaults.Minutes.Values, minutes => Assert.Equal(15, minutes)); // suggestions, not official values
    }

    [Fact]
    public void StagesInheritTheShiftAndNeverExceedIt()
    {
        var shift = DayLessonsAndShiftModeTests.ShiftWithLessons(1, 7);
        shift.SetDayLessons([new DayLessons(4, 6)], SundayToThursday);
        var stage = Stage.Create(1, "الأول الابتدائي", 1);
        Assert.Equal(34, stage.WeeklyLessons(SundayToThursday, shift)); // inherits: 7×4 + 6
        Assert.Equal(34, Section.WeeklyCapacity(WorkingWeek.CreateDefault(), shift, stage));

        stage.SetDayLessons([new DayLessons(7, 5), new DayLessons(1, 5), new DayLessons(2, 5), new DayLessons(3, 5), new DayLessons(4, 5)], SundayToThursday, shift.LessonsOn);
        Assert.Equal(25, Section.WeeklyCapacity(WorkingWeek.CreateDefault(), shift, stage));
        Assert.Equal(34, Section.WeeklyCapacity(WorkingWeek.CreateDefault(), shift, null));
        Assert.Equal(0, Section.WeeklyCapacity(null, shift, stage));

        var errors = Assert.Throws<DomainValidationException>(() =>
            stage.SetDayLessons([new DayLessons(5, 3), new DayLessons(4, 7), new DayLessons(1, 0), new DayLessons(1, 2)], SundayToThursday, shift.LessonsOn)).Errors;
        Assert.Equal([DomainErrorCode.InvalidOption, DomainErrorCode.Duplicate, DomainErrorCode.OutOfRange], errors.Select(error => error.Code));

        // A shortened shift caps the stage at run time; clamping stores the lower value.
        shift.SetDayLessons([new DayLessons(7, 4)], SundayToThursday);
        Assert.Equal(4, stage.LessonsOn(7, shift));
        Assert.True(stage.ClampDayLessons(7, 4));
        Assert.False(stage.ClampDayLessons(7, 4));
        Assert.False(stage.ClampDayLessons(5, 1));
        Assert.Equal(4, stage.DayLessonCounts.Single(entry => entry.Day == 7).Lessons);
        Assert.Equal(5, stage.CopyTo(2).DayLessonCounts.Count);
        stage.SetDayLessons([], SundayToThursday, shift.LessonsOn);
        Assert.Empty(stage.DayLessonCounts);
    }

    [Fact]
    public async Task RoutesSetStageLessonsAndGuardShorteningAShift()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        await host.PutAsync("/api/v1/setup-wizard/school", new { name = "ابتدائية الفرات", schoolType = "primary", shiftMode = "morning" }, token);
        await host.PutAsync("/api/v1/setup-wizard/year", new { label = "2026-2027", startDate = "2026-09-01", endDate = "2027-06-30", terms = Array.Empty<object>() }, token);
        var timing = new { days = SundayToThursday, weekStartDay = 7, shifts = new[] { new { kind = "morning", firstStartTime = "08:00", lessonMinutes = 40, lessonCount = 6, breaks = new[] { new { afterLesson = 3, minutes = 20 } }, dayLessons = Array.Empty<object>(), gapMinutes = 5 } } };
        await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-wizard/timing", timing, token));
        var year = (await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync("/api/v1/academic-years/"))).Items.Single();
        var root = $"/api/v1/academic-years/{year.Id}";
        var shift = (await ReadAsync<PagedResult<ShiftDto>>(await host.Client.GetAsync($"{root}/shifts/?pageSize=100"))).Items.Single();
        Assert.Equal("08:45", shift.Periods[1].StartTime); // gap of 5 minutes after lesson 1
        Assert.Equal(20, (int)(TimeOnly.Parse(shift.Periods[3].EndTime, System.Globalization.CultureInfo.InvariantCulture) - TimeOnly.Parse(shift.Periods[3].StartTime, System.Globalization.CultureInfo.InvariantCulture)).TotalMinutes);

        await host.PostAsync($"{root}/templates/stages", new { schoolType = "primary", grades = new[] { new { gradeKey = "primary-1", sections = 2, shiftId = shift.Id }, new { gradeKey = "primary-6", sections = 1, shiftId = shift.Id } } }, token);
        var cards = await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards"));
        var first = cards[0].Stage;
        Assert.Equal(30, cards[0].Sections[0].WeeklyCapacity);

        Assert.Equal(HttpStatusCode.Forbidden, (await host.SendJsonAsync(HttpMethod.Put, $"{root}/stages/{first.Id}/day-lessons", new { dayLessons = Array.Empty<object>(), version = first.Version }, "wrong-token")).StatusCode);
        var tooMany = await host.PutAsync($"{root}/stages/{first.Id}/day-lessons", new { dayLessons = new[] { new { day = 7, lessons = 7 } }, version = first.Version }, token);
        Assert.Contains((await AssertApiErrorAsync(tooMany, "VALIDATION_FAILED")).Errors, issue => issue is { Field: "DayLessons", Code: "VALUE_OUT_OF_RANGE" });
        var fewer = SundayToThursday.Select(day => new { day, lessons = 4 }).ToArray();
        var saved = await ReadAsync<StageDto>(await host.PutAsync($"{root}/stages/{first.Id}/day-lessons", new { dayLessons = fewer, version = first.Version }, token));
        Assert.Equal(5, saved.DayLessons.Count);
        Assert.Equal(HttpStatusCode.Conflict, (await host.PutAsync($"{root}/stages/{first.Id}/day-lessons", new { dayLessons = fewer, version = first.Version }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PutAsync($"{root}/stages/999/day-lessons", new { dayLessons = fewer, version = 1 }, token)).StatusCode);

        // The stage's sections and its curriculum totals use its own capacity (20), the other stage keeps 30.
        cards = await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards"));
        Assert.Equal([20, 20, 30], cards.SelectMany(card => card.Sections).Select(section => section.WeeklyCapacity));
        var table = await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum"));
        Assert.Equal([20, 30], table.Stages.Select(stage => stage.Totals.Single().WeeklyCapacity));

        // Shortening the shift below the stage's own count: preview lists it, saving needs confirmation, then clamps.
        var shifted = (await ReadAsync<PagedResult<ShiftDto>>(await host.Client.GetAsync($"{root}/shifts/?pageSize=100"))).Items.Single();
        var shorter = new { dayLessons = new[] { new { day = 4, lessons = 3 } }, version = shifted.Version };
        var impact = await ReadAsync<List<StageLessonsImpactDto>>(await host.PostAsync($"{root}/shifts/{shifted.Id}/day-lessons/impact", shorter, token));
        Assert.Equal((first.Id, 4, 4, 3), (impact.Single().StageId, impact.Single().Day, impact.Single().StageLessons, impact.Single().ShiftLessons));
        await AssertApiErrorAsync(await host.PutAsync($"{root}/shifts/{shifted.Id}/day-lessons", shorter, token), "STAGE_LESSONS_ABOVE_SHIFT");
        await ReadAsync<ShiftDto>(await host.PutAsync($"{root}/shifts/{shifted.Id}/day-lessons", new { shorter.dayLessons, shorter.version, confirmStageChanges = true }, token));
        var stage = (await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards")))[0].Stage;
        Assert.Equal(3, stage.DayLessons.Single(entry => entry.Day == 4).Lessons);
        Assert.Equal(15, (await ReadAsync<TemplateCatalogDto>(await host.Client.GetAsync("/api/v1/templates/"))).BreakDefaults.Minutes["primary"]);
    }

    [Fact]
    public async Task TheMigrationUpgradesAnExistingDatabase()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"smart-school-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite($"Data Source={Path.Combine(directory, "old.db")};Pooling=False").Options;
            await using (var old = new LocalDbContext(options))
            {
                var migrator = old.GetService<IMigrator>();
                var previous = old.Database.GetMigrations().Last(name => name.EndsWith("_Phase25CCurriculumTemplates", StringComparison.Ordinal));
                await migrator.MigrateAsync(previous);
                var year = AcademicYear.Create("2025-2026", new DateOnly(2025, 9, 1), new DateOnly(2026, 6, 30));
                old.Add(year);
                await old.SaveChangesAsync();
                old.Add(Stage.Create(year.Id, "الأول المتوسط", 1));
                await old.SaveChangesAsync();
            }
            await using var upgraded = new LocalDbContext(options);
            await upgraded.Database.MigrateAsync();
            Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync());
            var stage = await upgraded.Set<Stage>().SingleAsync();
            Assert.Empty(stage.DayLessonCounts); // existing stages inherit the shift
            stage.SetDayLessons([new DayLessons(7, 5)], SundayToThursday, _ => 7);
            await upgraded.SaveChangesAsync();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
