using System.Net;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Setup;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Tests.Phase2;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase25;

/// <summary>Per-day lesson counts, the grid built from them, shift mode and setup progress (spec 2.5 §3).</summary>
public sealed class DayLessonsAndShiftModeTests
{
    internal static readonly int[] SundayToThursday = [7, 1, 2, 3, 4];
    private static readonly int[] FirstTwoSteps = [1, 2];

    internal static Shift ShiftWithLessons(long yearId, int lessons, string name = "صباحي", ShiftKind kind = ShiftKind.Other)
    {
        var shift = Shift.Create(yearId, name, 1, kind);
        shift.ReplacePeriods(Enumerable.Range(0, lessons)
            .Select(index => new PeriodDraft(PeriodKind.Lesson, new TimeOnly(8, 0).AddMinutes(index * 45), new TimeOnly(8, 45).AddMinutes(index * 45)))
            .ToArray());
        return shift;
    }

    [Fact]
    public void PerDayCountsDriveCapacityAndOnlyExceptionsAreStored()
    {
        var shift = ShiftWithLessons(1, 7);
        Assert.Equal(35, shift.WeeklyLessons(SundayToThursday));
        shift.SetDayLessons([new DayLessons(4, 6), new DayLessons(7, 7)], SundayToThursday);
        Assert.Equal([new DayLessons(4, 6)], shift.DayLessonOverrides); // Sunday equals the full count: not stored
        Assert.Equal((7, 6), (shift.LessonsOn(7), shift.LessonsOn(4)));
        Assert.Equal(34, Section.WeeklyCapacity(WorkingWeek.CreateDefault(), shift));
        Assert.Equal(0, Section.WeeklyCapacity(null, shift));

        var errors = Assert.Throws<DomainValidationException>(() =>
            shift.SetDayLessons([new DayLessons(5, 3), new DayLessons(1, 8), new DayLessons(2, 1), new DayLessons(2, 2)], SundayToThursday)).Errors;
        Assert.Equal([DomainErrorCode.InvalidOption, DomainErrorCode.Duplicate, DomainErrorCode.OutOfRange], errors.Select(error => error.Code));

        shift.ReplacePeriods(ShiftWithLessons(1, 5).Periods.Select(period => new PeriodDraft(period.Kind, period.StartTime, period.EndTime)).ToArray());
        Assert.Equal(5, shift.LessonsOn(4)); // a stored count above the new lesson count is capped
        var copy = shift.CopyTo(2);
        Assert.Equal((ShiftKind.Other, 1), (copy.Kind, copy.DayLessonOverrides.Count));
        shift.SetKind(ShiftKind.Evening);
        shift.SetKind(ShiftKind.Evening);
        Assert.Throws<DomainValidationException>(() => shift.SetKind((ShiftKind)9));
    }

    [Fact]
    public void GridUsesTheLessonsOfEachDayAndTheLargestShiftForTeacherLimits()
    {
        var morning = ShiftWithLessons(1, 7);
        morning.SetDayLessons([new DayLessons(4, 5)], SundayToThursday);
        var evening = ShiftWithLessons(1, 6, "مسائي");
        var grid = ScheduleGrid.From(SundayToThursday, [morning, evening]);
        Assert.Equal((7, 6), (grid.LessonsOn(7), grid.LessonsOn(4))); // Thursday: max(5, 6)
        Assert.Equal(33, grid.MaxWeeklyLessons); // morning 7*4+5 = 33, evening 30
        Assert.True(grid.Contains(new BlockedPeriod(4, 6)));
        Assert.False(grid.Contains(new BlockedPeriod(4, 7)));
        Assert.Equal(0, ScheduleGrid.From(SundayToThursday, []).LessonsPerDay);

        TeacherDetails Limits(int perDay, int perWeek) => new("أحمد علي", "أحمد", null, null, false, null, null, null, perDay, perWeek, null);
        Assert.Equal(33, Teacher.Create(Limits(7, 33), grid).MaxLessonsPerWeek);
        var errors = Assert.Throws<DomainValidationException>(() => Teacher.Create(Limits(8, 34), grid)).Errors;
        Assert.Equal([DomainErrorCode.MaxPerDayExceedsPeriods, DomainErrorCode.MaxPerWeekExceedsCapacity], errors.Select(error => error.Code));
    }

    [Fact]
    public void SetupProgressRecordsStepsAndProfileChoices()
    {
        var progress = SetupProgress.CreateDefault(DateTimeOffset.UnixEpoch);
        progress.Record(3, [1, 2], [2, 6], false, DateTimeOffset.UnixEpoch);
        Assert.Equal([1, 2], progress.CompletedSteps);
        Assert.Equal([6], progress.SkippedSteps); // step 2 is done, so it is no longer skipped
        Assert.Equal(3, progress.CurrentStep);
        Assert.Equal(["CurrentStep", "CompletedSteps", "SkippedSteps"],
            Assert.Throws<DomainValidationException>(() => progress.Record(9, [0], [9], false, DateTimeOffset.UnixEpoch)).Errors.Select(error => error.Field));

        var profile = SchoolProfile.CreateDefault(DateTimeOffset.UnixEpoch);
        profile.SetStudyType(StudyType.Dual, DateTimeOffset.UnixEpoch);
        profile.SetStudyType(StudyType.Dual, DateTimeOffset.UnixEpoch);
        profile.SetSchoolType(SchoolType.Secondary, DateTimeOffset.UnixEpoch);
        profile.SetSchoolType(SchoolType.Secondary, DateTimeOffset.UnixEpoch);
        Assert.Equal((StudyType.Dual, SchoolType.Secondary, 3), (profile.StudyType, profile.SchoolType, profile.Version));
        Assert.Throws<DomainValidationException>(() => profile.SetStudyType((StudyType)9, DateTimeOffset.UnixEpoch));
        Assert.Throws<DomainValidationException>(() => profile.SetSchoolType((SchoolType)9, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task DayLessonsAndSetupProgressRoutesValidateAndPersist()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/setup-progress/")).StatusCode);
        var (token, _) = await SetupOwnerAsync(host);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PostWithoutTokenAsync("/api/v1/shift-system/convert", new { targetSystem = "morning" })).StatusCode);
        await AssertApiErrorAsync(await host.PutAsync("/api/v1/shift-system/", new { system = "morning" }, token), "NO_CURRENT_YEAR");

        var year = await AcademicYearApiTests.CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");
        var progress = await ReadAsync<SetupProgressDto>(await host.Client.GetAsync("/api/v1/setup-progress/"));
        var system = await ReadAsync<ShiftSystemDto>(await host.PutAsync("/api/v1/shift-system/", new
        {
            system = "morning",
            main = new { kind = "morning", firstStartTime = "08:00", lessonMinutes = 45, lessonCount = 7, breaks = Array.Empty<object>(), dayLessons = Array.Empty<object>() },
        }, token));
        Assert.Equal(("morning", false), (system.System, system.Legacy));
        var morning = (await ReadAsync<PagedResult<ShiftDto>>(await host.Client.GetAsync($"/api/v1/academic-years/{year.Id}/shifts/"))).Items.Single();
        Assert.Equal("morning", morning.Kind);
        var withPeriods = morning;

        var invalid = await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{morning.Id}/day-lessons", new { dayLessons = new[] { new { day = 4, lessons = 9 } }, version = withPeriods.Version }, token);
        Assert.Contains((await AssertApiErrorAsync(invalid, "VALIDATION_FAILED")).Errors, issue => issue is { Field: "DayLessons", Code: "VALUE_OUT_OF_RANGE" });
        var saved = await ReadAsync<ShiftDto>(await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{morning.Id}/day-lessons", new { dayLessons = new[] { new { day = 4, lessons = 6 } }, version = withPeriods.Version }, token));
        Assert.Equal((34, 6), (saved.WeeklyLessons, saved.DayLessons.Single(day => day.Day == 4).Lessons));
        Assert.Equal(HttpStatusCode.Conflict, (await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{morning.Id}/day-lessons", new { dayLessons = Array.Empty<object>(), version = withPeriods.Version }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/999/day-lessons", new { dayLessons = Array.Empty<object>(), version = 1 }, token)).StatusCode);
        var grid = await ReadAsync<ScheduleGridDto>(await host.Client.GetAsync("/api/v1/schedule-grid/"));
        Assert.Equal((7, 6, 34), (grid.LessonsPerDay, grid.LessonsByDay.Single(day => day.Day == 4).Lessons, grid.MaxWeeklyLessons));

        var stepped = await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-progress/", new { currentStep = 3, completedSteps = FirstTwoSteps, skippedSteps = Array.Empty<int>(), isFinished = false, version = progress.Version }, token));
        Assert.Equal((3, "morning"), (stepped.CurrentStep, stepped.ShiftMode));
        Assert.Equal(HttpStatusCode.Conflict, (await host.PutAsync("/api/v1/setup-progress/", new { currentStep = 4, isFinished = false, version = progress.Version }, token)).StatusCode);
        var outOfRange = await host.PutAsync("/api/v1/setup-progress/", new { currentStep = 9, isFinished = false, version = stepped.Version }, token);
        Assert.Contains((await AssertApiErrorAsync(outOfRange, "VALIDATION_FAILED")).Errors, issue => issue.Field == "CurrentStep");
    }
}
