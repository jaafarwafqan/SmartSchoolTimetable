using System.Net;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.SchoolSetup;
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
            Assert.Throws<DomainValidationException>(() => progress.Record(8, [0], [9], false, DateTimeOffset.UnixEpoch)).Errors.Select(error => error.Field));

        var profile = SchoolProfile.CreateDefault(DateTimeOffset.UnixEpoch);
        profile.SetStudyType(StudyType.Dual, DateTimeOffset.UnixEpoch);
        profile.SetStudyType(StudyType.Dual, DateTimeOffset.UnixEpoch);
        profile.SetSchoolType(SchoolType.Secondary, DateTimeOffset.UnixEpoch);
        profile.SetSchoolType(SchoolType.Secondary, DateTimeOffset.UnixEpoch);
        Assert.Equal((StudyType.Dual, SchoolType.Secondary, 3), (profile.StudyType, profile.SchoolType, profile.Version));
        Assert.Throws<DomainValidationException>(() => profile.SetStudyType((StudyType)9, DateTimeOffset.UnixEpoch));
        Assert.Throws<DomainValidationException>(() => profile.SetSchoolType((SchoolType)9, DateTimeOffset.UnixEpoch));
    }

    private static async Task<(ShiftModeService Mode, FakeDataStore Store, AcademicYear Year)> SeedModeAsync(bool withYear = true)
    {
        var store = new FakeDataStore();
        store.Add(SchoolProfile.CreateDefault(DateTimeOffset.UnixEpoch));
        store.Add(WorkingWeek.CreateDefault());
        var year = AcademicYear.Create("2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        year.MarkCurrent(withYear);
        store.Add(year);
        await store.SaveChangesAsync(default);
        return (new ShiftModeService(store, TimeProvider.System), store, year);
    }

    [Fact]
    public async Task ShiftModeCreatesAdoptsAndRemovesShiftsSafely()
    {
        var (noYear, _, _) = await SeedModeAsync(withYear: false);
        Assert.Equal(ErrorCodes.NoCurrentYear, (await noYear.GetImpactAsync("morning", default)).ErrorCode);

        var (service, store, year) = await SeedModeAsync();
        Assert.Contains((await service.GetImpactAsync("weekend", default)).FieldErrors, error => error is { Field: "Mode", Code: ErrorCodes.InvalidOption });
        store.Add(Shift.Create(year.Id, ShiftModeService.MorningName, 1)); // a Phase 2 shift with the standard name is adopted
        await store.SaveChangesAsync(default);

        var dual = (await service.SetAsync(new SetShiftModeCommand("dual", 1), default)).Value!;
        Assert.Equal(["morning", "evening"], dual.Shifts.Select(shift => shift.Kind));
        Assert.Equal(ErrorCodes.Conflict, (await service.SetAsync(new SetShiftModeCommand("morning", 1), default)).ErrorCode);

        var evening = store.Query<Shift>().Single(shift => shift.Kind == ShiftKind.Evening);
        var stage = Stage.Create(year.Id, "الأول المتوسط", 1);
        store.Add(stage);
        await store.SaveChangesAsync(default);
        store.Add(Section.Create(stage.Id, evening.Id, "أ", null));
        await store.SaveChangesAsync(default);

        var impact = (await service.GetImpactAsync("morning", default)).Value!;
        Assert.False(impact.Allowed);
        Assert.Equal(["الدوام المسائي"], impact.ShiftsToRemove);
        Assert.Equal(("الأول المتوسط", "أ"), (impact.AffectedSections[0].StageName, impact.AffectedSections[0].Label));
        Assert.Equal(ErrorCodes.ShiftModeInUse, (await service.SetAsync(new SetShiftModeCommand("morning", dual.ProfileVersion), default)).ErrorCode);

        store.Remove(store.Query<Section>().Single());
        await store.SaveChangesAsync(default);
        var morning = (await service.SetAsync(new SetShiftModeCommand("morning", dual.ProfileVersion), default)).Value!;
        Assert.Equal(["morning"], morning.Shifts.Select(shift => shift.Kind));
        Assert.Equal("evening", (await service.GetImpactAsync("evening", default)).Value!.ShiftsToCreate.Single());
    }

    [Fact]
    public async Task DayLessonsAndSetupProgressRoutesValidateAndPersist()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/setup-progress/")).StatusCode);
        var (token, _) = await SetupOwnerAsync(host);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PostWithoutTokenAsync("/api/v1/shift-mode/", new { mode = "morning" })).StatusCode);
        await AssertApiErrorAsync(await host.Client.GetAsync("/api/v1/shift-mode/impact?mode=morning"), "NO_CURRENT_YEAR");

        var year = await AcademicYearApiTests.CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");
        var progress = await ReadAsync<SetupProgressDto>(await host.Client.GetAsync("/api/v1/setup-progress/"));
        var mode = await ReadAsync<ShiftModeDto>(await host.PutAsync("/api/v1/shift-mode/", new { mode = "dual", version = 1 }, token));
        Assert.Equal(2, mode.Shifts.Count);
        var morning = mode.Shifts.Single(shift => shift.Kind == "morning");
        var lessons = Enumerable.Range(0, 7).Select(index => new { kind = "lesson", startTime = $"{8 + index:00}:00", endTime = $"{8 + index:00}:45", startBell = true, endBell = true }).ToArray();
        var withPeriods = await ReadAsync<ShiftDto>(await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{morning.Id}/periods", new { periods = lessons, version = morning.Version }, token));

        var invalid = await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{morning.Id}/day-lessons", new { dayLessons = new[] { new { day = 4, lessons = 9 } }, version = withPeriods.Version }, token);
        Assert.Contains((await AssertApiErrorAsync(invalid, "VALIDATION_FAILED")).Errors, issue => issue is { Field: "DayLessons", Code: "VALUE_OUT_OF_RANGE" });
        var saved = await ReadAsync<ShiftDto>(await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{morning.Id}/day-lessons", new { dayLessons = new[] { new { day = 4, lessons = 6 } }, version = withPeriods.Version }, token));
        Assert.Equal((34, 6), (saved.WeeklyLessons, saved.DayLessons.Single(day => day.Day == 4).Lessons));
        Assert.Equal(HttpStatusCode.Conflict, (await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{morning.Id}/day-lessons", new { dayLessons = Array.Empty<object>(), version = withPeriods.Version }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/999/day-lessons", new { dayLessons = Array.Empty<object>(), version = 1 }, token)).StatusCode);
        var grid = await ReadAsync<ScheduleGridDto>(await host.Client.GetAsync("/api/v1/schedule-grid/"));
        Assert.Equal((7, 6, 34), (grid.LessonsPerDay, grid.LessonsByDay.Single(day => day.Day == 4).Lessons, grid.MaxWeeklyLessons));

        var stepped = await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-progress/", new { currentStep = 3, completedSteps = FirstTwoSteps, skippedSteps = Array.Empty<int>(), isFinished = false, version = progress.Version }, token));
        Assert.Equal((3, "dual"), (stepped.CurrentStep, stepped.ShiftMode));
        Assert.Equal(HttpStatusCode.Conflict, (await host.PutAsync("/api/v1/setup-progress/", new { currentStep = 4, isFinished = false, version = progress.Version }, token)).StatusCode);
        var outOfRange = await host.PutAsync("/api/v1/setup-progress/", new { currentStep = 9, isFinished = false, version = stepped.Version }, token);
        Assert.Contains((await AssertApiErrorAsync(outOfRange, "VALIDATION_FAILED")).Errors, issue => issue.Field == "CurrentStep");
    }
}
