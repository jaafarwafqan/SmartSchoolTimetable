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

/// <summary>The official Iraqi study plan 2026-2027 (ADR 0028–0030): data, matching, apply rules and the daily distribution.</summary>
public sealed class SuggestedCurriculumTests
{
    private static readonly int[] SundayToThursday = [7, 1, 2, 3, 4];
    private static readonly int[] SaturdayToThursday = [6, 7, 1, 2, 3, 4];
    private static readonly string[] French = ["اللغة الفرنسية"];
    private static readonly string[] Kurdish = ["اللغة الكردية"];
    private static readonly string[] KurdishAndFrench = ["اللغة الكردية", "اللغة الفرنسية"];
    private static readonly string[] BothBranches = ["scientific", "literary"];
    private static readonly string[] NoBranches = [];
    private static readonly long[] UnknownStage = [999L];
    private static readonly int[] NothingPlanned = [0, 0, 0, 0, 0, 0];
    private static readonly int[] PrimaryTotals = [30, 30, 30, 31, 30, 31];
    private static readonly int[] SecondaryDefault = [30, 30, 30, 28, 28];

    private static readonly int[] SecondaryWithKurdishAndFrench = [32, 32, 32, 32, 32];
    private static readonly int[] FourthAndFifthOfficial = [30, 30, 30, 31];
    private static readonly int[] FourthAndFifthWithoutKurdish = [28, 28, 29, 30];
    private static readonly string[] ReviewStages = ["الرابع الابتدائي", "الرابع العلمي"];
    private static readonly string[] OptionalSubjects = ["الحاسوب", "اللغة الفرنسية", "اللغة الكردية", "منهج جرائم حزب البعث"];
    private static readonly string[] AddedOnTop = ["الحاسوب", "اللغة الفرنسية", "منهج جرائم حزب البعث"];
    private static readonly HashSet<string> NoneChosen = [];
    private static readonly HashSet<string> AllOptional = [.. OptionalSubjects];

    /// <summary>The printed total of each stage in the official plan 2026-2027.</summary>
    private static readonly (string Stage, int Stated)[] PrintedTotals =
    [
        ("الأول الابتدائي", 30), ("الثاني الابتدائي", 30), ("الثالث الابتدائي", 30),
        ("الرابع الابتدائي", 30), ("الخامس الابتدائي", 30), ("السادس الابتدائي", 31),
        ("الأول المتوسط", 30), ("الثاني المتوسط", 30), ("الثالث المتوسط", 30),
        ("الرابع العلمي", 30), ("الخامس العلمي", 30), ("السادس العلمي", 33),
        ("الرابع الأدبي", 30), ("الخامس الأدبي", 31), ("السادس الأدبي", 31),
    ];

    [Fact]
    public void TheOfficialTemplateTotalsMatchThePrintedPlan()
    {
        var template = SuggestedCurriculumTemplate.Current;
        Assert.Equal(2, template.Version);
        Assert.Equal("official", template.Provenance.Status);
        Assert.Contains("2026-2027", template.Provenance.Source, StringComparison.Ordinal);
        Assert.Equal(PrintedTotals, template.Stages.Select(stage => (stage.Name, stage.StatedTotal)));
        Assert.All(template.Stages.SelectMany(stage => stage.Entries), entry => Assert.InRange(entry.Lessons, CurriculumEntry.MinWeeklyLessons, CurriculumEntry.MaxWeeklyLessons));

        // Mandatory rows + Kurdish (optional but counted) = the printed total, except الرابع الابتدائي: 31 against 30.
        foreach (var stage in template.Stages)
        {
            var counted = stage.Entries.Where(entry => !entry.Optional || entry.Subject == "اللغة الكردية").Sum(entry => entry.Lessons);
            Assert.Equal(counted, stage.OfficialTotal());
            Assert.Equal(counted, stage.ComputedTotal);
            if (stage.Name == "الرابع الابتدائي")
            {
                Assert.Equal((31, 30), (counted, stage.StatedTotal));
                Assert.True(stage.NeedsReview);
                Assert.False(stage.TotalMatchesPrinted);
                Assert.False(string.IsNullOrWhiteSpace(stage.VerificationNote));
            }
            else
            {
                Assert.Equal(stage.StatedTotal, counted);
                Assert.True(stage.TotalMatchesPrinted);
            }
        }
        // Flagged: الرابع الابتدائي (total) and الرابع العلمي (does منهج جرائم حزب البعث apply to the fifth too?).
        Assert.Equal(ReviewStages, template.Stages.Where(stage => stage.NeedsReview).Select(stage => stage.Name));
        Assert.All(template.Stages.Where(stage => stage.NeedsReview), stage => Assert.False(string.IsNullOrWhiteSpace(stage.VerificationNote)));

        // Optional rows: Kurdish counts in the official total; French, computing and حزب البعث are added on top of it.
        var optional = template.Stages.SelectMany(stage => stage.Entries).Where(entry => entry.Optional).ToArray();
        Assert.Equal(OptionalSubjects, optional.Select(entry => entry.Subject).Distinct().Order());
        Assert.All(optional, entry => Assert.Equal(entry.Subject == "اللغة الكردية", entry.CountsInStatedTotal));
        Assert.Equal(AddedOnTop, optional.Where(entry => entry.InStatedTotal == false).Select(entry => entry.Subject).Distinct().Order());
        Assert.All(template.Stages, stage => Assert.Equal(stage.Total(NoneChosen) + stage.Entries.Where(entry => entry.Optional).Sum(entry => entry.Lessons), stage.Total(AllOptional)));
        Assert.All(template.Stages.SelectMany(stage => stage.Entries).Where(entry => !entry.Optional), entry => Assert.True(entry.CountsInStatedTotal));

        // Every stage name is a stage the stage templates produce (primary 6, intermediate 3, preparatory 3 × 2 branches).
        var catalog = TemplateCatalog.Current;
        var templateStageNames = catalog.Grades.SelectMany(grade => grade.BranchStem is null
            ? [grade.Name]
            : catalog.Branches.Select(branch => TemplateCatalog.Stage(grade, branch).Name)).ToHashSet();
        Assert.All(template.Stages, stage => Assert.Contains(stage.Name, templateStageNames));
        Assert.Equal(15, template.Stages.Count);

        // Aliases fold the plan's spellings into one subject.
        Assert.True(template.SameSubject("اللغة الإنكليزية", "اللغة الانجليزية"));
        Assert.True(template.SameSubject("اللغة العربية", "اللغة العربية (قراءتي)"));
        Assert.True(template.SameSubject("اللغة العربية", "قراءتي"));
        Assert.True(template.SameSubject("التربية الفنية والنشيد", "التربية الفنية"));
        Assert.True(template.SameSubject("الاجتماع", "علم الاجتماع"));
        Assert.True(template.SameSubject("الاقتصاد", "مبادئ الاقتصاد"));
        Assert.Equal("اللغة العربية", template.Canonical("اللغة العربية (قراءتي)"));
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
        Assert.Equal((2, "official"), (preview.TemplateVersion, preview.Provenance.Status));
        Assert.Equal((false, "exists", "اللغة الإنجليزية"), preview.Subjects.Where(line => line.Name == "اللغة الإنكليزية").Select(line => (line.Optional, line.Action, line.ExistingName)).Single());
        Assert.Equal(PrimaryTotals, preview.Stages.Select(stage => stage.ResultingTotal));
        Assert.Equal([false, false, false, true, false, false], preview.Stages.Select(stage => stage.NeedsReview));
        Assert.Equal((30, 31), (preview.Stages[3].StatedTotal, preview.Stages[3].OfficialTotal));
        Assert.NotNull(preview.Stages[3].VerificationNote);
        // «اللغة العربية (قراءتي)» and «اللغة العربية» are one subject, as are «التربية الفنية والنشيد» and its alias.
        Assert.Single(preview.Subjects, line => line.Name == "اللغة العربية");
        Assert.DoesNotContain(preview.Subjects, line => line.Name == "اللغة العربية (قراءتي)");
        Assert.Equal(NothingPlanned, await PlannedAsync(host, root)); // preview saves nothing

        var applied = await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested", new { }, token));
        Assert.Equal(preview.Changes, applied.Changes);
        Assert.Equal(PrimaryTotals, await PlannedAsync(host, root));
        var subjects = await ReadAsync<PagedResult<SubjectDto>>(await host.Client.GetAsync("/api/v1/subjects/?pageSize=100"));
        Assert.Single(subjects.Items, subject => subject.Name.Contains("نجليزية", StringComparison.Ordinal) || subject.Name.Contains("نكليزية", StringComparison.Ordinal));
        Assert.Single(subjects.Items, subject => subject.Name.Contains("العربية", StringComparison.Ordinal));
        Assert.Equal(9, subjects.Items.Count); // primary: 9 distinct subjects, no optional ones
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
        Assert.Equal(30, (await PlannedAsync(host, root))[0]);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PostAsync($"{root}/curriculum/suggested/stages/999/reset/preview", new { }, token)).StatusCode);

        // Daily suggestion: 30 → 6,6,6,6,6; applied, every stage matches its capacity; manual counts are never overwritten.
        var daily = await ReadAsync<DailySuggestionDto>(await host.Client.GetAsync($"{root}/daily-suggestion"));
        Assert.Equal([6, 6, 6, 6, 6], daily.Stages[0].Suggested.Select(day => day.Lessons));
        Assert.All(daily.Stages, stage => Assert.Equal("apply", stage.Status));
        await ReadAsync<DailySuggestionDto>(await host.PostAsync($"{root}/daily-suggestion", new { stageIds = daily.Stages.Select(stage => stage.StageId) }, token));
        var totals = (await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum"))).Stages.Select(stage => stage.Totals.Single().Status);
        Assert.All(totals, status => Assert.Equal("equal", status));
        daily = await ReadAsync<DailySuggestionDto>(await host.Client.GetAsync($"{root}/daily-suggestion"));
        Assert.All(daily.Stages, stage => Assert.Equal("same", stage.Status));

        var cards = await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards"));
        await ReadAsync<StageDto>(await host.PutAsync($"{root}/stages/{cards[1].Stage.Id}/day-lessons", new { dayLessons = SundayToThursday.Select(day => new { day, lessons = 7 }), version = cards[1].Stage.Version }, token));
        daily = await ReadAsync<DailySuggestionDto>(await host.Client.GetAsync($"{root}/daily-suggestion"));
        Assert.Equal("manual", daily.Stages[1].Status);
        await ReadAsync<DailySuggestionDto>(await host.PostAsync($"{root}/daily-suggestion", new { stageIds = new[] { cards[1].Stage.Id } }, token));
        Assert.Equal(35, (await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards")))[1].Sections[0].WeeklyCapacity); // unchanged

        // A curriculum change after a suggestion shows "a new suggestion is available"; a total above the shift is refused.
        var first = (await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum")));
        var subjectId = first.Rows[0].SubjectId;
        await ReadAsync<CurriculumTableDto>(await host.PutAsync($"{root}/curriculum/cell", new { stageId = first.Stages[0].Id, subjectId, label = "إضافي", weeklyLessons = 8 }, token));
        daily = await ReadAsync<DailySuggestionDto>(await host.Client.GetAsync($"{root}/daily-suggestion"));
        Assert.Equal(("aboveCapacity", 38, 35), (daily.Stages[0].Status, daily.Stages[0].WeeklyTotal, daily.Stages[0].Capacity));
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
        // Every optional subject unchecked by default: Kurdish (counted in the official 30) leaves the fourth grades at 28.
        var preview = await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested/preview", new { }, token));
        Assert.Equal(SecondaryDefault, preview.Stages.Select(stage => stage.ResultingTotal));
        Assert.All(preview.Stages, stage => Assert.Equal((30, 30), (stage.StatedTotal, stage.OfficialTotal)));
        Assert.Equal([false, false, false, true, false], preview.Stages.Select(stage => stage.NeedsReview));
        Assert.Equal(OptionalSubjects, preview.OptionalSubjects.Order());
        Assert.Contains(preview.Subjects, line => line is { Name: "اللغة الفرنسية", Optional: true, Included: false, InStatedTotal: false });
        Assert.Contains(preview.Subjects, line => line is { Name: "اللغة الكردية", Optional: true, Included: false, InStatedTotal: true });
        Assert.Contains(preview.Stages[0].Entries, line => line is { Subject: "اللغة الفرنسية", Action: "skipped" });
        var withFrench = await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested/preview", new { optionalSubjects = French }, token));
        Assert.Equal([32, 32, 32], withFrench.Stages.Take(3).Select(stage => stage.ResultingTotal));
        await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested", new { }, token));
        Assert.Equal(SecondaryDefault, await PlannedAsync(host, root));
        var names = (await ReadAsync<PagedResult<SubjectDto>>(await host.Client.GetAsync("/api/v1/subjects/?pageSize=100"))).Items.Select(subject => subject.Name).ToArray();
        Assert.All(OptionalSubjects, subject => Assert.DoesNotContain(subject, names)); // an unchecked optional subject is never created
        await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested", new { optionalSubjects = KurdishAndFrench }, token)); // adds Kurdish and French only
        Assert.Equal(SecondaryWithKurdishAndFrench, await PlannedAsync(host, root));

        // A stage with sections in both shifts must fit the smaller one (morning 7, evening 6 → 30 a week).
        var cards = await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards"));
        var evening = shifts.Single(shift => shift.Kind == "evening");
        await ReadAsync<StageCardDto>(await host.PutAsync($"{root}/stage-cards/{cards[0].Stage.Id}/section-count", new { count = 2, shiftId = evening.Id }, token));
        var daily = await ReadAsync<DailySuggestionDto>(await host.Client.GetAsync($"{root}/daily-suggestion"));
        Assert.Equal((30, "aboveCapacity"), (daily.Stages[0].Capacity, daily.Stages[0].Status)); // 32 lessons with French
        Assert.Equal((35, "apply"), (daily.Stages[1].Capacity, daily.Stages[1].Status));
    }

    [Fact]
    public async Task KurdishIsOptionalButCountsInTheOfficialTotal()
    {
        var (host, token, root, _, _) = await SchoolAsync("secondary", "morning", [("preparatory-4", BothBranches), ("preparatory-5", BothBranches)]);
        await using var _ = host;
        // Off (default): the fourth grades 28 and the fifth 29/30 against the official 30/30/30/31.
        var off = await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested/preview", new { }, token));
        Assert.Equal(FourthAndFifthOfficial, off.Stages.Select(stage => stage.StatedTotal));
        Assert.Equal(FourthAndFifthWithoutKurdish, off.Stages.Select(stage => stage.SuggestedTotal));
        Assert.All(off.Stages, stage => Assert.Contains(stage.Entries, line => line is { Subject: "اللغة الكردية", Action: "skipped", InStatedTotal: true }));
        var on = await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested/preview", new { optionalSubjects = Kurdish }, token));
        Assert.Equal(on.Stages.Select(stage => stage.StatedTotal), on.Stages.Select(stage => stage.SuggestedTotal)); // on: exactly the official total
        Assert.Equal(on.Stages.Select(stage => stage.StatedTotal), on.Stages.Select(stage => stage.ResultingTotal));

        await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested", new { }, token));
        Assert.Equal(FourthAndFifthWithoutKurdish, await PlannedAsync(host, root));
        var applied = await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested", new { optionalSubjects = Kurdish }, token));
        Assert.Equal(1 + 4, applied.Changes); // the subject once, one line per stage
        Assert.Equal(FourthAndFifthOfficial, await PlannedAsync(host, root));
        Assert.Equal(0, (await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested", new { optionalSubjects = Kurdish }, token))).Changes);
        var subjects = (await ReadAsync<PagedResult<SubjectDto>>(await host.Client.GetAsync("/api/v1/subjects/?pageSize=100"))).Items;
        Assert.Single(subjects, subject => subject.Name == "اللغة الكردية");
        Assert.Equal(subjects.Count, subjects.Select(subject => subject.Name).Distinct().Count());
        // Unticking later never deletes what was applied (non-destructive).
        await ReadAsync<SuggestedCurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/suggested", new { }, token));
        Assert.Equal(FourthAndFifthOfficial, await PlannedAsync(host, root));
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
