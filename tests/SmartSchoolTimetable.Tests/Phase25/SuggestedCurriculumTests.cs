using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Curriculum;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Setup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Infrastructure;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase25;

/// <summary>The owner's suggested Iraqi curriculum (ADR 0028–0030): data, matching, apply rules and the daily distribution.</summary>
public sealed class SuggestedCurriculumTests
{
    private static readonly int[] SundayToThursday = [7, 1, 2, 3, 4];
    private static readonly int[] SaturdayToThursday = [6, 7, 1, 2, 3, 4];
    private static readonly string[] French = ["اللغة الفرنسية"];
    private static readonly string[] BothBranches = ["scientific", "literary"];
    private static readonly string[] NoBranches = [];
    private static readonly long[] UnknownStage = [999L];
    private static readonly int[] NothingPlanned = [0, 0, 0, 0, 0, 0];
    private static readonly int[] PrimaryTotals = [28, 28, 27, 29, 30, 30];
    private static readonly int[] SecondaryWithFrench = [33, 33, 34, 28, 25];
    private static readonly string[] LiteraryStages = ["الرابع الأدبي", "الخامس الأدبي", "السادس الأدبي"];

    /// <summary>Section 5 of the owner's instruction: totals computed from the rows (all, without optional).</summary>
    private static readonly (string Stage, int All, int WithoutOptional)[] ExpectedTotals =
    [
        ("الأول الابتدائي", 28, 28), ("الثاني الابتدائي", 28, 28), ("الثالث الابتدائي", 27, 27),
        ("الرابع الابتدائي", 29, 29), ("الخامس الابتدائي", 30, 30), ("السادس الابتدائي", 30, 30),
        ("الأول المتوسط", 33, 30), ("الثاني المتوسط", 33, 30), ("الثالث المتوسط", 34, 31),
        ("الرابع العلمي", 30, 28), ("الخامس العلمي", 31, 30), ("السادس العلمي", 33, 33),
        ("الرابع الأدبي", 27, 25), ("الخامس الأدبي", 30, 29), ("السادس الأدبي", 30, 30),
    ];

    [Fact]
    public void TheTemplateMatchesTheStagesAndTheExpectedTotals()
    {
        var template = SuggestedCurriculumTemplate.Current;
        Assert.Equal(1, template.Version);
        Assert.Contains("NOT verified", template.Provenance, StringComparison.Ordinal);
        Assert.Equal(ExpectedTotals.Select(item => (item.Stage, item.All, item.WithoutOptional)),
            template.Stages.Select(stage => (stage.Name, stage.Total(includeOptional: true), stage.Total(includeOptional: false))));
        Assert.All(template.Stages, stage => Assert.Equal((stage.ComputedTotal, stage.ComputedTotalWithoutOptional), (stage.Total(true), stage.Total(false))));
        Assert.All(template.Stages.SelectMany(stage => stage.Entries), entry => Assert.InRange(entry.Lessons, CurriculumEntry.MinWeeklyLessons, CurriculumEntry.MaxWeeklyLessons));

        // Exactly the three literary stages: their stated total matches neither computed total.
        Assert.Equal(LiteraryStages, template.Stages.Where(stage => stage.NeedsReview).Select(stage => stage.Name));
        Assert.All(template.Stages, stage => Assert.Equal(stage.NeedsReview, stage.StatedTotal != stage.Total(true) && stage.StatedTotal != stage.Total(false)));

        // Every stage name is a stage the stage templates produce (primary 6, intermediate 3, preparatory 3 × 2 branches).
        var catalog = TemplateCatalog.Current;
        var templateStageNames = catalog.Grades.SelectMany(grade => grade.BranchStem is null
            ? [grade.Name]
            : catalog.Branches.Select(branch => TemplateCatalog.Stage(grade, branch).Name)).ToHashSet();
        Assert.All(template.Stages, stage => Assert.Contains(stage.Name, templateStageNames));
        Assert.Equal(15, template.Stages.Count);

        // Optional subjects: Kurdish (fourth and fifth, both branches) and French (all intermediate grades).
        Assert.Equal(["اللغة الفرنسية", "اللغة الكردية"], template.Stages.SelectMany(stage => stage.Entries).Where(entry => entry.Optional).Select(entry => entry.Subject).Distinct().Order());
        Assert.True(template.SameSubject("اللغة الإنكليزية", "اللغة الإنجليزية"));
        Assert.True(template.SameSubject("الجغرافية", "الجغرافيا"));
        Assert.True(template.SameSubject("التربية الفنية والنشيد", "التربية الفنية و النشيد"));
        Assert.False(template.SameSubject("الجغرافية", "التاريخ"));
        Assert.Null(template.Canonical("مادة غير موجودة"));
    }

    [Theory]
    [InlineData(28, new[] { 6, 6, 6, 5, 5 })]
    [InlineData(27, new[] { 6, 6, 5, 5, 5 })]
    [InlineData(29, new[] { 6, 6, 6, 6, 5 })]
    [InlineData(30, new[] { 6, 6, 6, 6, 6 })]
    [InlineData(31, new[] { 7, 6, 6, 6, 6 })]
    [InlineData(33, new[] { 7, 7, 7, 6, 6 })]
    public void DistributionPutsExtraLessonsOnEarlierDays(int total, int[] expected)
    {
        var suggestion = DailyDistribution.Suggest(total, SundayToThursday, _ => 7);
        Assert.True(suggestion.Possible);
        Assert.Equal(SundayToThursday, suggestion.Days.Select(day => day.Day));
        Assert.Equal(expected, suggestion.Days.Select(day => day.Lessons));
    }

    [Fact]
    public void DistributionRespectsTheShiftAndReportsWhyItCannot()
    {
        Assert.Equal([6, 5, 5, 5, 5, 5], DailyDistribution.Suggest(31, SaturdayToThursday, _ => 7).Days.Select(day => day.Lessons)); // six working days
        Assert.Equal([7, 7, 7, 7, 5], DailyDistribution.Suggest(33, SundayToThursday, day => day == 4 ? 5 : 7).Days.Select(day => day.Lessons)); // short Thursday
        Assert.Equal(DistributionProblem.AboveShiftCapacity, DailyDistribution.Suggest(36, SundayToThursday, _ => 7).Problem);
        Assert.Equal(DistributionProblem.NoCurriculum, DailyDistribution.Suggest(0, SundayToThursday, _ => 7).Problem);
        Assert.Equal(DistributionProblem.BelowWorkingDays, DailyDistribution.Suggest(3, SundayToThursday, _ => 7).Problem);
        Assert.Equal(SaturdayToThursday, DailyDistribution.InWeekOrder([1, 2, 3, 4, 6, 7], 6));
    }

    [Fact]
    public void SuggestionFlagsClearOnOwnerEdits()
    {
        var entry = CurriculumEntry.CreateSuggested(1, 2, 5);
        Assert.True(entry.IsSuggested);
        Assert.True(entry.CopyTo(3).IsSuggested);
        entry.SetWeeklyLessons(4);
        Assert.False(entry.IsSuggested);
        entry.ResetToSuggestion(5);
        Assert.Equal((5, true), (entry.WeeklyLessons, entry.IsSuggested));

        var stage = Stage.Create(1, "الأول الابتدائي", 1);
        stage.ApplySuggestedDayLessons([new DayLessons(7, 6)], SundayToThursday, _ => 7);
        Assert.True(stage.DayLessonsSuggested);
        Assert.True(stage.CopyTo(2).DayLessonsSuggested);
        stage.SetDayLessons([new DayLessons(7, 5)], SundayToThursday, _ => 7);
        Assert.False(stage.DayLessonsSuggested);
    }

    private static async Task<(TestHost Host, string Token, string Root, long YearId, IReadOnlyList<ShiftDto> Shifts)> SchoolAsync(string schoolType, string mode, (string Key, string[] Branches)[] grades)
    {
        var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        await host.PutAsync("/api/v1/setup-wizard/school", new { name = "مدرسة المنهج", schoolType, shiftMode = mode }, token);
        await host.PutAsync("/api/v1/setup-wizard/year", new { label = "2026-2027", startDate = "2026-09-01", endDate = "2027-06-30", terms = Array.Empty<object>() }, token);
        var kinds = mode == "dual" ? new[] { ("morning", 7), ("evening", 6) } : new[] { (mode, 7) };
        await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-wizard/timing", new
        {
            days = SundayToThursday,
            weekStartDay = 7,
            shifts = kinds.Select(kind => new { kind = kind.Item1, firstStartTime = kind.Item1 == "evening" ? "13:00" : "08:00", lessonMinutes = 40, lessonCount = kind.Item2, breaks = Array.Empty<object>(), dayLessons = Array.Empty<object>() }),
        }, token));
        var year = (await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync("/api/v1/academic-years/"))).Items.Single();
        var root = $"/api/v1/academic-years/{year.Id}";
        var shifts = (await ReadAsync<PagedResult<ShiftDto>>(await host.Client.GetAsync($"{root}/shifts/?pageSize=100"))).Items;
        var templateGrades = grades.Select(grade => new { gradeKey = grade.Key, branches = grade.Branches, sections = 1, shiftId = shifts[0].Id }).ToArray();
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"{root}/templates/stages", new { schoolType, grades = templateGrades }, token)).StatusCode);
        return (host, token, root, year.Id, shifts);
    }

    private static async Task<int[]> PlannedAsync(TestHost host, string root) =>
        (await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum"))).Stages.Select(stage => stage.PlannedLessons).ToArray();

    [Fact]
    public async Task PrimaryApplyIsIdempotentNonDestructiveAndMatchesAliases()
    {
        var (host, token, root, _, _) = await SchoolAsync("primary", "morning", Enumerable.Range(1, 6).Select(grade => ($"primary-{grade}", NoBranches)).ToArray());
        await using var _ = host;
        Assert.Equal(HttpStatusCode.Forbidden, (await host.CreateClientWithoutCookies().PostAsync($"{root}/curriculum/suggested/preview", JsonContent(new { }))).StatusCode); // no launch token
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClientWithoutCookies().GetAsync($"{root}/daily-suggestion")).StatusCode); // not signed in
        // An existing subject spelled differently is matched, never duplicated.
        await ReadAsync<SubjectDto>(await host.PostAsync("/api/v1/subjects/", new { name = "اللغة الإنجليزية", colorIndex = 0, priority = 0, distributionEnabled = true, version = 0 }, token));

        var preview = await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested/preview", new { }, token));
        Assert.Contains("NOT verified", preview.Provenance, StringComparison.Ordinal);
        Assert.Equal((false, "exists", "اللغة الإنجليزية"), preview.Subjects.Where(line => line.Name == "اللغة الإنكليزية").Select(line => (line.Optional, line.Action, line.ExistingName)).Single());
        Assert.Equal([28, 28, 27, 29, 30, 30], preview.Stages.Select(stage => stage.ResultingTotal));
        Assert.Equal(NothingPlanned, await PlannedAsync(host, root)); // preview saves nothing

        var applied = await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested", new { }, token));
        Assert.Equal(preview.Changes, applied.Changes);
        Assert.Equal(PrimaryTotals, await PlannedAsync(host, root));
        var subjects = await ReadAsync<PagedResult<SubjectDto>>(await host.Client.GetAsync("/api/v1/subjects/?pageSize=100"));
        Assert.Single(subjects.Items, subject => subject.Name.Contains("نجليزية", StringComparison.Ordinal) || subject.Name.Contains("نكليزية", StringComparison.Ordinal));
        Assert.All(subjects.Items, subject => Assert.Equal(3, subject.Priority));

        // Apply twice = no change.
        Assert.Equal(0, (await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested", new { }, token))).Changes);

        // An owner edit clears «مقترح» and survives a later apply; the reset needs confirmation and shows before/after.
        var table = await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum"));
        var cell = table.Rows.First(row => row.Cells[0].EntryId is not null).Cells[0];
        Assert.True(cell.IsSuggested);
        table = await ReadAsync<CurriculumTableDto>(await host.PutAsync($"{root}/curriculum/cell", new { stageId = cell.StageId, subjectId = table.Rows.First(row => row.Cells[0].EntryId is not null).SubjectId, weeklyLessons = 1, entryId = cell.EntryId, version = cell.Version }, token));
        var edited = table.Rows.SelectMany(row => row.Cells).Single(item => item.EntryId == cell.EntryId);
        Assert.Equal((1, false), (edited.WeeklyLessons, edited.IsSuggested));
        await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested", new { }, token));
        Assert.Equal(1, (await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum"))).Rows.SelectMany(row => row.Cells).Single(item => item.EntryId == cell.EntryId).WeeklyLessons);

        var resetPath = $"{root}/curriculum/suggested/stages/{cell.StageId}/reset";
        var before = await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{resetPath}/preview", new { }, token));
        Assert.Contains(before.Stages.Single().Entries, line => line is { Action: "update", CurrentLessons: 1 });
        Assert.Contains((await AssertApiErrorAsync(await host.PostAsync(resetPath, new { }, token), "VALIDATION_FAILED")).Errors, issue => issue.Field == "Confirm");
        await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync(resetPath, new { confirm = true }, token));
        var reset = (await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum"))).Rows.SelectMany(row => row.Cells).Single(item => item.EntryId == cell.EntryId);
        Assert.True(reset.IsSuggested);
        Assert.Equal(28, (await PlannedAsync(host, root))[0]);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PostAsync($"{root}/curriculum/suggested/stages/999/reset/preview", new { }, token)).StatusCode);

        // Daily suggestion: 28 → 6,6,6,5,5; applied, every stage matches its capacity; manual counts are never overwritten.
        var daily = await ReadAsync<DailySuggestionDto>(await host.Client.GetAsync($"{root}/daily-suggestion"));
        Assert.Equal([6, 6, 6, 5, 5], daily.Stages[0].Suggested.Select(day => day.Lessons));
        Assert.All(daily.Stages, stage => Assert.Equal("apply", stage.Status));
        await ReadAsync<DailySuggestionDto>(await host.PostAsync($"{root}/daily-suggestion", new { stageIds = daily.Stages.Select(stage => stage.StageId) }, token));
        var totals = (await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum"))).Stages.Select(stage => stage.Totals.Single().Status);
        Assert.All(totals, status => Assert.Equal("equal", status));
        daily = await ReadAsync<DailySuggestionDto>(await host.Client.GetAsync($"{root}/daily-suggestion"));
        Assert.All(daily.Stages, stage => Assert.Equal("same", stage.Status));

        var cards = await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards"));
        await ReadAsync<StageDto>(await host.PutAsync($"{root}/stages/{cards[1].Stage.Id}/day-lessons", new { dayLessons = SundayToThursday.Select(day => new { day, lessons = 6 }), version = cards[1].Stage.Version }, token));
        daily = await ReadAsync<DailySuggestionDto>(await host.Client.GetAsync($"{root}/daily-suggestion"));
        Assert.Equal("manual", daily.Stages[1].Status);
        await ReadAsync<DailySuggestionDto>(await host.PostAsync($"{root}/daily-suggestion", new { stageIds = new[] { cards[1].Stage.Id } }, token));
        Assert.Equal(30, (await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards")))[1].Sections[0].WeeklyCapacity); // unchanged

        // A curriculum change after a suggestion shows "a new suggestion is available"; a total above the shift is refused.
        var first = (await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum")));
        var subjectId = first.Rows[0].SubjectId;
        await ReadAsync<CurriculumTableDto>(await host.PutAsync($"{root}/curriculum/cell", new { stageId = first.Stages[0].Id, subjectId, label = "إضافي", weeklyLessons = 8 }, token));
        daily = await ReadAsync<DailySuggestionDto>(await host.Client.GetAsync($"{root}/daily-suggestion"));
        Assert.Equal(("aboveCapacity", 36, 35), (daily.Stages[0].Status, daily.Stages[0].WeeklyTotal, daily.Stages[0].Capacity));
        await AssertApiErrorAsync(await host.PostAsync($"{root}/daily-suggestion", new { stageIds = new[] { first.Stages[0].Id } }, token), "DAILY_TOTAL_ABOVE_SHIFT");
        await ReadAsync<CurriculumTableDto>(await host.PutAsync($"{root}/curriculum/cell", new { stageId = first.Stages[2].Id, subjectId, label = "إضافي", weeklyLessons = 1 }, token));
        daily = await ReadAsync<DailySuggestionDto>(await host.Client.GetAsync($"{root}/daily-suggestion"));
        Assert.True(daily.Stages[2].ChangedSinceSuggestion);
        Assert.Contains((await AssertApiErrorAsync(await host.PostAsync($"{root}/daily-suggestion", new { stageIds = UnknownStage }, token), "VALIDATION_FAILED")).Errors, issue => issue.Field == "StageIds");
    }

    [Fact]
    public async Task OptionalSubjectsAndReviewWarningsFollowTheSchool()
    {
        var (host, token, root, _, shifts) = await SchoolAsync("secondary", "dual",
            [("intermediate-1", NoBranches), ("intermediate-2", NoBranches), ("intermediate-3", NoBranches), ("preparatory-4", BothBranches)]);
        await using var _ = host;
        // French unchecked by default: 30/30/31; checked: 33/33/34. Kurdish stays out unless chosen.
        var preview = await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested/preview", new { }, token));
        Assert.Equal([30, 30, 31, 28, 25], preview.Stages.Select(stage => stage.ResultingTotal));
        Assert.Equal([false, false, false, false, true], preview.Stages.Select(stage => stage.NeedsReview));
        Assert.Contains(preview.Subjects, line => line is { Name: "اللغة الفرنسية", Optional: true, Included: false });
        Assert.Contains(preview.Stages[0].Entries, line => line is { Subject: "اللغة الفرنسية", Action: "skipped" });
        var withFrench = await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested/preview", new { optionalSubjects = French }, token));
        Assert.Equal([33, 33, 34], withFrench.Stages.Take(3).Select(stage => stage.ResultingTotal));
        await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested", new { }, token));
        await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested", new { optionalSubjects = French }, token)); // adds French only
        Assert.Equal(SecondaryWithFrench, await PlannedAsync(host, root));

        // A stage with sections in both shifts must fit the smaller one (morning 7, evening 6 → 30 a week).
        var cards = await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards"));
        var evening = shifts.Single(shift => shift.Kind == "evening");
        await ReadAsync<StageCardDto>(await host.PutAsync($"{root}/stage-cards/{cards[0].Stage.Id}/section-count", new { count = 2, shiftId = evening.Id }, token));
        var daily = await ReadAsync<DailySuggestionDto>(await host.Client.GetAsync($"{root}/daily-suggestion"));
        Assert.Equal((30, "aboveCapacity"), (daily.Stages[0].Capacity, daily.Stages[0].Status)); // 33 lessons with French
        Assert.Equal((35, "apply"), (daily.Stages[1].Capacity, daily.Stages[1].Status));
    }

    [Fact]
    public async Task TheMigrationAddsTheFlagsToAnExistingDatabase()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"smart-school-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite($"Data Source={Path.Combine(directory, "old.db")};Pooling=False").Options;
            await using (var old = new LocalDbContext(options))
            {
                var previous = old.Database.GetMigrations().Last(name => name.EndsWith("_Phase25FixStageDayLessons", StringComparison.Ordinal));
                await old.GetService<IMigrator>().MigrateAsync(previous);
            }
            await using var upgraded = new LocalDbContext(options);
            await upgraded.Database.MigrateAsync();
            Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync());
            var year = AcademicYear.Create("2025-2026", new DateOnly(2025, 9, 1), new DateOnly(2026, 6, 30));
            upgraded.Add(year);
            await upgraded.SaveChangesAsync();
            var stage = Stage.Create(year.Id, "الأول الابتدائي", 1);
            stage.ApplySuggestedDayLessons([new DayLessons(7, 6)], SundayToThursday, _ => 7);
            upgraded.Add(stage);
            await upgraded.SaveChangesAsync();
            Assert.True((await upgraded.Set<Stage>().AsNoTracking().SingleAsync()).DayLessonsSuggested);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static StringContent JsonContent(object body) => new(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
}
