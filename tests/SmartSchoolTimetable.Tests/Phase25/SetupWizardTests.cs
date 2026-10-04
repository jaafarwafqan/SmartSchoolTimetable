using System.Net;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Curriculum;
using SmartSchoolTimetable.Application.Dashboard;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Setup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Tests.Phase2;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase25;

/// <summary>The setup wizard steps on the real database: one transaction each, idempotent, resumable (spec 2.5 §5).</summary>
public sealed class SetupWizardTests
{
    private static readonly int[] SundayToThursday = [7, 1, 2, 3, 4];
    private static readonly int[] SundayToWednesday = [7, 1, 2, 3];
    private static readonly int[] SchoolAndYear = [1, 2];
    private static readonly int[] FirstThreeSteps = [1, 2, 3];
    private static readonly int[] TeachersStep = [6];
    private static readonly string[] MathsOnly = ["الرياضيات"];

    private static object School(string mode) => new { name = "متوسطة الرافدين", schoolType = "intermediate", shiftMode = mode, principalName = "أ. علي" };

    private static readonly object Year = new
    {
        label = "2026-2027",
        startDate = "2026-09-01",
        endDate = "2027-06-30",
        terms = new[]
        {
            new { name = "الفصل الأول", startDate = "2026-09-01", endDate = "2027-01-15" },
            new { name = "الفصل الثاني", startDate = "2027-02-01", endDate = "2027-06-30" },
        },
    };

    private static object Timing(int[] days, int eveningLessons) => new
    {
        days,
        weekStartDay = 7,
        shifts = new object[]
        {
            new { kind = "morning", firstStartTime = "08:00", lessonMinutes = 45, lessonCount = 7, breaks = new[] { new { afterLesson = 3, minutes = 15 } }, dayLessons = new[] { new { day = 4, lessons = 6 } } },
            new { kind = "evening", firstStartTime = "13:00", lessonMinutes = 40, lessonCount = eveningLessons, breaks = Array.Empty<object>(), dayLessons = Array.Empty<object>() },
        },
    };

    [Fact]
    public async Task StepsSaveThroughTheServicesAndCanRunAgain()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/setup-wizard/review")).StatusCode);
        var (token, _) = await SetupOwnerAsync(host);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PostWithoutTokenAsync("/api/v1/setup-wizard/school", School("dual"))).StatusCode);

        var afterSchool = await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-wizard/school", School("dual"), token));
        Assert.Equal((2, "intermediate", "dual"), (afterSchool.CurrentStep, afterSchool.SchoolType, afterSchool.ShiftMode));
        await AssertApiErrorAsync(await host.PutAsync("/api/v1/setup-wizard/timing", Timing(SundayToThursday, 6), token), "NO_CURRENT_YEAR");
        var invalidSchool = await host.PutAsync("/api/v1/setup-wizard/school", new { name = "", schoolType = "college", shiftMode = "dual" }, token);
        Assert.Contains((await AssertApiErrorAsync(invalidSchool, "VALIDATION_FAILED")).Errors, issue => issue.Field == "SchoolType");

        for (var run = 0; run < 2; run++)
        {
            var afterYear = await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-wizard/year", Year, token));
            Assert.Equal(SchoolAndYear, afterYear.CompletedSteps);
        }
        var years = await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync("/api/v1/academic-years/"));
        var year = years.Items.Single();
        Assert.True(year.IsCurrent);
        Assert.Equal(2, year.Terms.Count);
        Assert.Single(year.Terms, term => term.IsCurrent);

        // A failing shift (13 lessons) rolls back the whole step, the working week included.
        await AssertApiErrorAsync(await host.PutAsync("/api/v1/setup-wizard/timing", Timing(SundayToWednesday, 13), token), "VALIDATION_FAILED");
        Assert.Equal(SundayToThursday, (await ReadAsync<WorkingWeekDto>(await host.Client.GetAsync("/api/v1/working-days/"))).Days);
        var shiftsPath = $"/api/v1/academic-years/{year.Id}/shifts/?pageSize=100";
        Assert.All((await ReadAsync<PagedResult<ShiftDto>>(await host.Client.GetAsync(shiftsPath))).Items, shift => Assert.Equal(0, shift.LessonCount));

        for (var run = 0; run < 2; run++)
            Assert.Equal(FirstThreeSteps, (await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-wizard/timing", Timing(SundayToThursday, 6), token))).CompletedSteps);
        var shifts = (await ReadAsync<PagedResult<ShiftDto>>(await host.Client.GetAsync(shiftsPath))).Items;
        Assert.Equal([("morning", 7, 34), ("evening", 6, 30)], shifts.Select(shift => (shift.Kind, shift.LessonCount, shift.WeeklyLessons)));
        Assert.Equal(8, shifts[0].Periods.Count); // 7 lessons + 1 break
        var duplicateKinds = new { days = SundayToThursday, weekStartDay = 7, shifts = new[] { new { kind = "evening", firstStartTime = "13:00", lessonMinutes = 40, lessonCount = 6 }, new { kind = "evening", firstStartTime = "13:00", lessonMinutes = 40, lessonCount = 6 } } };
        Assert.Contains((await AssertApiErrorAsync(await host.PutAsync("/api/v1/setup-wizard/timing", duplicateKinds, token), "VALIDATION_FAILED")).Errors, issue => issue.Field == "Shifts");

        // Step 4 (template endpoint): the third grade is taught in the evening.
        var root = $"/api/v1/academic-years/{year.Id}";
        var grades = new object[]
        {
            new { gradeKey = "intermediate-1", sections = 2, shiftId = shifts[0].Id },
            new { gradeKey = "intermediate-2", sections = 2, shiftId = shifts[0].Id },
            new { gradeKey = "intermediate-3", sections = 1, shiftId = shifts[1].Id },
        };
        Assert.Equal(3, (await ReadAsync<StagePlanDto>(await host.PostAsync($"{root}/templates/stages", new { schoolType = "intermediate", grades }, token))).Changes);

        // Switching the mode from step 1 is refused while evening sections exist; the profile change is rolled back.
        await AssertApiErrorAsync(await host.PutAsync("/api/v1/setup-wizard/school", School("morning"), token), "SHIFT_MODE_IN_USE");
        Assert.Equal("dual", (await ReadAsync<SetupProgressDto>(await host.Client.GetAsync("/api/v1/setup-progress/"))).ShiftMode);

        var cards = await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards"));
        await ReadAsync<SubjectPlanDto>(await host.PostAsync("/api/v1/templates/subjects", new { names = MathsOnly }, token));
        var table = await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum"));
        await ReadAsync<CurriculumTableDto>(await host.PutAsync($"{root}/curriculum/cell", new { stageId = cards[0].Stage.Id, subjectId = table.Rows.Single().SubjectId, weeklyLessons = 6 }, token));

        var review = await ReadAsync<SetupReviewDto>(await host.Client.GetAsync("/api/v1/setup-wizard/review"));
        Assert.Equal(("متوسطة الرافدين", "2026-2027", 2, 3, 5, 1, 1, 0), (review.SchoolName, review.YearLabel, review.Shifts, review.Stages, review.Sections, review.Subjects, review.CurriculumLines, review.Teachers));
        Assert.Contains(review.Warnings, warning => warning is { Code: "under", StageName: "الأول المتوسط", Value: 28 });
        Assert.Equal(2, review.Warnings.Count(warning => warning.Code == "emptyCurriculum"));

        var dashboard = await ReadAsync<DashboardSummaryDto>(await host.Client.GetAsync("/api/v1/dashboard-summary/"));
        Assert.False(dashboard.SetupFinished);
        Assert.Equal(6, dashboard.Curriculum[0].PlannedLessons);
        var progress = await ReadAsync<SetupProgressDto>(await host.Client.GetAsync("/api/v1/setup-progress/"));
        await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-progress/", new { currentStep = 7, completedSteps = progress.CompletedSteps, skippedSteps = TeachersStep, isFinished = true, version = progress.Version }, token));
        Assert.True((await ReadAsync<DashboardSummaryDto>(await host.Client.GetAsync("/api/v1/dashboard-summary/"))).SetupFinished);
    }
}
