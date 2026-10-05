using System.Net;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Dashboard;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase2;

public sealed class SubjectsTests
{
    private static readonly ScheduleGrid Grid = ScheduleGrid.Uniform([7, 1, 2, 3, 4], 6);

    private static SubjectDetails Details(string? name = "الرياضيات", int color = 3, int priority = 5, string? notes = null, params BlockedPeriod[] blocked) =>
        new(name, color, priority, true, true, false, true, notes, blocked);

    [Fact]
    public void GridContainsOnlyWorkingDaysAndDefinedLessons()
    {
        Assert.True(Grid.Contains(new BlockedPeriod(7, 1)));
        Assert.True(Grid.Contains(new BlockedPeriod(4, 6)));
        Assert.False(Grid.Contains(new BlockedPeriod(5, 1)));  // Friday is not a working day
        Assert.False(Grid.Contains(new BlockedPeriod(1, 7)));  // beyond the lessons per day
        Assert.False(Grid.Contains(new BlockedPeriod(1, 0)));
        Assert.Equal(30, Grid.MaxWeeklyLessons);
        Assert.False(ScheduleGrid.Uniform([7], 0).Contains(new BlockedPeriod(7, 1))); // no periods defined yet
    }

    [Fact]
    public void SubjectValidatesFieldsAndNormalizesBlockedPeriods()
    {
        var errors = Assert.Throws<DomainValidationException>(() =>
            Subject.Create(Details(" ", 11, 0, new string('x', 501), new BlockedPeriod(5, 1)), Grid)).Errors;
        Assert.Equal(
            ["Name", "Notes", "ColorIndex", "Priority", "BlockedPeriods"],
            errors.Select(error => error.Field));

        var subject = Subject.Create(Details("  اللغة   العربية ", notes: "  ", blocked: [new BlockedPeriod(2, 3), new BlockedPeriod(1, 6), new BlockedPeriod(2, 3)]), Grid);
        Assert.Equal("اللغة العربية", subject.Name);
        Assert.Null(subject.Notes);
        Assert.Equal([new BlockedPeriod(1, 6), new BlockedPeriod(2, 3)], subject.BlockedPeriods);
        Assert.True(subject.RequiresDoublePeriod);

        subject.Update(Details("العربية", 10, 1, "ملاحظة"), Grid);
        Assert.Empty(subject.BlockedPeriods);
        Assert.Equal((10, 1, "ملاحظة", 2), (subject.ColorIndex, subject.Priority, subject.Notes!, subject.Version));
        subject.Archive(DateTimeOffset.UnixEpoch);
        subject.Archive(DateTimeOffset.UtcNow);
        Assert.Equal(DateTimeOffset.UnixEpoch, subject.ArchivedAt);
        subject.Restore();
        subject.Restore();
        Assert.Equal(4, subject.Version);
    }

    private static async Task<(SubjectsService Service, FakeDataStore Store)> SeedAsync()
    {
        var store = new FakeDataStore();
        var year = AcademicYear.Create("2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        year.MarkCurrent(true);
        store.Add(year);
        store.Add(WorkingWeek.CreateDefault());
        store.Add(SchoolProfile.CreateDefault(DateTimeOffset.UnixEpoch));
        await store.SaveChangesAsync(default);
        var shift = Shift.Create(year.Id, "صباحي", 1);
        shift.ReplacePeriods(Enumerable.Range(0, 6).Select(index =>
            new PeriodDraft(PeriodKind.Lesson, new TimeOnly(8, 0).AddMinutes(index * 45), new TimeOnly(8, 45).AddMinutes(index * 45))).ToArray());
        store.Add(shift);
        await store.SaveChangesAsync(default);
        return (new SubjectsService(store, TimeProvider.System), store);
    }

    private static SaveSubjectCommand Command(string name = "الفيزياء", int version = 0, params BlockedPeriodDto[] blocked) =>
        new(name, 2, 4, true, false, true, false, blocked, null, version);

    [Fact]
    public async Task ServiceChecksGridDuplicatesVersionsAndArchive()
    {
        var (service, store) = await SeedAsync();
        Assert.Contains((await service.CreateAsync(Command(blocked: new BlockedPeriodDto(1, 7)), default)).FieldErrors,
            error => error is { Field: "BlockedPeriods", Code: ErrorCodes.BlockedPeriodInvalid });
        var subject = (await service.CreateAsync(Command(blocked: new BlockedPeriodDto(1, 6)), default)).Value!;
        Assert.Equal([new BlockedPeriodDto(1, 6)], subject.BlockedPeriods);
        Assert.Contains((await service.CreateAsync(Command("الفيزياء "), default)).FieldErrors, error => error.Code == ErrorCodes.DuplicateName);
        await service.CreateAsync(Command("الكيمياء"), default);

        Assert.Equal(ErrorCodes.Conflict, (await service.UpdateAsync(subject.Id, Command(version: 99), default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.UpdateAsync(999, Command(version: 1), default)).ErrorCode);
        Assert.Contains((await service.UpdateAsync(subject.Id, Command("الكيمياء", subject.Version), default)).FieldErrors, error => error.Code == ErrorCodes.DuplicateName);
        var archived = (await service.SetArchivedAsync(subject.Id, subject.Version, true, default)).Value!;
        Assert.True(archived.IsArchived);
        Assert.Single((await service.ListAsync(new ListQuery(null, "-priority", null, null, null), default)).Items);
        Assert.Equal(1, (await service.ListAsync(new ListQuery("فيزيا", null, null, null, true), default)).Total);
        Assert.Equal(ErrorCodes.Conflict, (await service.SetArchivedAsync(subject.Id, subject.Version, false, default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.SetArchivedAsync(999, 1, false, default)).ErrorCode);

        var summary = await new DashboardService(store).GetSummaryAsync(default);
        Assert.Contains(summary.Counts, count => count is { Key: "subjects", Value: 1 });
        Assert.Contains(summary.Checklist, item => item is { Key: "subjects", Done: true });

        Assert.Equal(ErrorCodes.Conflict, (await service.DeleteAsync(subject.Id, subject.Version, default)).ErrorCode);
        Assert.True((await service.DeleteAsync(subject.Id, archived.Version, default)).Succeeded);
        Assert.Equal(ErrorCodes.NotFound, (await service.DeleteAsync(subject.Id, archived.Version, default)).ErrorCode);
        store.NextSaveFailure = new DataConflictException();
        Assert.Contains((await service.CreateAsync(Command("الأحياء"), default)).FieldErrors, error => error is { Field: "Name", Code: ErrorCodes.DuplicateName });
    }

    [Fact]
    public async Task SubjectRoutesPersistBlockedPeriodsAndRequireTheSession()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/subjects/")).StatusCode);
        var (token, _) = await SetupOwnerAsync(host);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PostWithoutTokenAsync("/api/v1/subjects/", new { name = "x" })).StatusCode);

        var empty = await ReadAsync<ScheduleGridDto>(await host.Client.GetAsync("/api/v1/schedule-grid/"));
        Assert.Equal((5, 0), (empty.Days.Count, empty.LessonsPerDay));
        var blockedBeforePeriods = await host.PostAsync("/api/v1/subjects/", new { name = "الرياضيات", colorIndex = 1, priority = 3, blockedPeriods = new[] { new { day = 7, lessonNumber = 1 } }, version = 0 }, token);
        Assert.Contains((await AssertApiErrorAsync(blockedBeforePeriods, "VALIDATION_FAILED")).Errors, issue => issue.Code == "BLOCKED_PERIOD_INVALID");

        var year = await AcademicYearApiTests.CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");
        var shift = await ReadAsync<ShiftDto>(await host.PostAsync($"/api/v1/academic-years/{year.Id}/shifts/", new { name = "صباحي", displayOrder = 1, version = 0 }, token));
        var lessons = Enumerable.Range(0, 2).Select(index => new { kind = "lesson", startTime = $"0{8 + index}:00", endTime = $"0{8 + index}:45", startBell = true, endBell = true }).ToArray();
        Assert.Equal(HttpStatusCode.OK, (await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{shift.Id}/periods", new { periods = lessons, version = shift.Version }, token)).StatusCode);
        Assert.Equal(2, (await ReadAsync<ScheduleGridDto>(await host.Client.GetAsync("/api/v1/schedule-grid/"))).LessonsPerDay);

        var created = await host.PostAsync("/api/v1/subjects/", new { name = "الرياضيات", colorIndex = 4, priority = 5, heavy = true, blockedPeriods = new[] { new { day = 7, lessonNumber = 1 }, new { day = 1, lessonNumber = 2 } }, version = 0 }, token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var subject = await ReadAsync<SubjectDto>(created);
        Assert.Equal(2, subject.BlockedPeriods.Count);
        var updated = await ReadAsync<SubjectDto>(await host.PutAsync($"/api/v1/subjects/{subject.Id}", new { name = "الرياضيات", colorIndex = 4, priority = 5, blockedPeriods = new[] { new { day = 2, lessonNumber = 1 } }, version = subject.Version }, token));
        Assert.Equal([new BlockedPeriodDto(2, 1)], updated.BlockedPeriods);
        Assert.Equal(HttpStatusCode.Conflict, (await host.PutAsync($"/api/v1/subjects/{subject.Id}", new { name = "x", colorIndex = 1, priority = 1, version = subject.Version }, token)).StatusCode);
        var listed = await ReadAsync<PagedResult<SubjectDto>>(await host.Client.GetAsync("/api/v1/subjects/?search=رياضيات"));
        Assert.Equal([new BlockedPeriodDto(2, 1)], Assert.Single(listed.Items).BlockedPeriods);

        var archived = await ReadAsync<SubjectDto>(await host.PostAsync($"/api/v1/subjects/{subject.Id}/archive", new { version = updated.Version }, token));
        Assert.Empty((await ReadAsync<PagedResult<SubjectDto>>(await host.Client.GetAsync("/api/v1/subjects/"))).Items);
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendJsonAsync(HttpMethod.Delete, $"/api/v1/subjects/{subject.Id}?version={archived.Version}", new { }, token)).StatusCode);
    }
}
