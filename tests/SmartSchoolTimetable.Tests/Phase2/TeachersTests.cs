using System.Net;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Dashboard;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Teachers;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Teachers;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase2;

public sealed class TeachersTests
{
    private static readonly ScheduleGrid Grid = new([7, 1, 2, 3, 4], 6);
    private static readonly string[] OneName = ["x"];
    private static readonly int[] Monday = [1];
    private static readonly string[] PreviewNames = ["حسن جواد", "أحمد علي"];
    private static readonly string[] NewName = ["حسن جواد"];

    private static TeacherDetails Details(
        string? fullName = "أحمد علي حسن",
        string? shortName = "أحمد علي",
        int[]? offDays = null,
        BlockedPeriod[]? blocked = null,
        bool released = false,
        DateOnly? from = null,
        DateOnly? to = null,
        int? perDay = null,
        int? perWeek = null) =>
        new(fullName, shortName, offDays, blocked, released, released ? "إجازة" : "سبب يُهمل", from, to, perDay, perWeek, null);

    [Fact]
    public void TeacherValidatesConstraintsAgainstTheGrid()
    {
        var errors = Assert.Throws<DomainValidationException>(() => Teacher.Create(
            Details(" ", "", [5], [new BlockedPeriod(1, 7)], true, new DateOnly(2027, 1, 2), new DateOnly(2027, 1, 1), 7, 31), Grid)).Errors;
        Assert.Equal(
            [("FullName", DomainErrorCode.Required), ("ShortName", DomainErrorCode.Required), ("OffDays", DomainErrorCode.InvalidOption),
             ("ReleaseTo", DomainErrorCode.InvalidDateRange), ("MaxLessonsPerDay", DomainErrorCode.MaxPerDayExceedsPeriods),
             ("MaxLessonsPerWeek", DomainErrorCode.MaxPerWeekExceedsCapacity), ("BlockedPeriods", DomainErrorCode.BlockedPeriodInvalid)],
            errors.Select(error => (error.Field, error.Code)));
        Assert.Contains(Assert.Throws<DomainValidationException>(() => Teacher.Create(Details(perDay: 5, perWeek: 4), Grid)).Errors,
            error => error is { Field: "MaxLessonsPerDay", Code: DomainErrorCode.OutOfRange });
        Assert.Contains(Assert.Throws<DomainValidationException>(() => Teacher.Create(Details(perDay: 0, perWeek: 0), Grid)).Errors,
            error => error is { Field: "MaxLessonsPerWeek", Code: DomainErrorCode.OutOfRange });

        // Before periods exist, limits are accepted (DECISIONS_PENDING #5) but blocked periods are not.
        Assert.Equal(50, Teacher.Create(Details(perDay: 10, perWeek: 50), new ScheduleGrid([7], 0)).MaxLessonsPerWeek);

        var teacher = Teacher.Create(Details(offDays: [2, 7], blocked: [new BlockedPeriod(1, 2), new BlockedPeriod(1, 2)], perDay: 6, perWeek: 30), Grid);
        Assert.Equal([2, 7], teacher.OffDays);
        Assert.Single(teacher.BlockedPeriods);
        Assert.Null(teacher.ReleaseReason); // ignored while not released
        teacher.Update(Details(released: true, from: new DateOnly(2026, 10, 1), to: new DateOnly(2026, 12, 31)), Grid);
        Assert.Equal(("إجازة", new DateOnly(2026, 10, 1)), (teacher.ReleaseReason!, teacher.ReleaseFrom!.Value));
        Assert.Empty(teacher.OffDays);
        teacher.Archive(DateTimeOffset.UnixEpoch);
        teacher.Archive(DateTimeOffset.UtcNow);
        teacher.Restore();
        teacher.Restore();
        Assert.Equal(4, teacher.Version);
    }

    [Fact]
    public void ShortNameProposalsWidenUntilFree()
    {
        var taken = new HashSet<string> { "احمد علي" };
        Assert.Equal("أحمد علي حسن", TeacherNames.ProposeShortName("أحمد علي حسن كاظم", taken.Contains));
        Assert.Equal("زينب", TeacherNames.ProposeShortName("  زينب ", taken.Contains));
        Assert.Null(TeacherNames.ProposeShortName("أحمد علي", taken.Contains));
        Assert.Null(TeacherNames.ProposeShortName("   ", taken.Contains));
    }

    private static async Task<(TeachersService Service, FakeDataStore Store)> SeedAsync()
    {
        var store = new FakeDataStore();
        store.Add(WorkingWeek.CreateDefault());
        store.Add(SchoolProfile.CreateDefault(DateTimeOffset.UnixEpoch));
        await store.SaveChangesAsync(default);
        return (new TeachersService(store, TimeProvider.System), store);
    }

    private static SaveTeacherCommand Command(string fullName = "سارة محمود", string shortName = "سارة", int version = 0, bool released = false, string? from = null) =>
        new(fullName, shortName, [7], null, released, null, from, null, 4, 20, "ملاحظة", version);

    [Fact]
    public async Task ServiceHandlesCrudReleaseDatesAndDuplicates()
    {
        var (service, store) = await SeedAsync();
        Assert.Contains((await service.CreateAsync(Command(released: true, from: "1-10-2026"), default)).FieldErrors, error => error is { Field: "ReleaseFrom", Code: ErrorCodes.InvalidDate });
        var teacher = (await service.CreateAsync(Command(released: true, from: "2026-10-01"), default)).Value!;
        Assert.Equal(("2026-10-01", 4, 20), (teacher.ReleaseFrom!, teacher.MaxLessonsPerDay!.Value, teacher.MaxLessonsPerWeek!.Value));
        Assert.Contains((await service.CreateAsync(Command("سارة أخرى", " سارة "), default)).FieldErrors, error => error is { Field: "ShortName", Code: ErrorCodes.DuplicateName });
        await service.CreateAsync(Command("ليلى حسين", "ليلى"), default);

        Assert.Single((await service.ListAsync(new ListQuery(null, null, null, null, null), true, default)).Items);
        Assert.Equal(["ليلى", "سارة"], (await service.ListAsync(new ListQuery(null, "-shortName", null, null, null), null, default)).Items.Select(item => item.ShortName));
        Assert.Single((await service.ListAsync(new ListQuery("حسين", "-name", null, null, null), null, default)).Items);

        Assert.Equal(ErrorCodes.Conflict, (await service.UpdateAsync(teacher.Id, Command(version: 99), default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.UpdateAsync(999, Command(version: 1), default)).ErrorCode);
        Assert.Contains((await service.UpdateAsync(teacher.Id, Command(shortName: "ليلى", version: teacher.Version), default)).FieldErrors, error => error.Code == ErrorCodes.DuplicateName);
        Assert.Contains((await service.UpdateAsync(teacher.Id, Command(released: true, from: "x", version: teacher.Version), default)).FieldErrors, error => error.Field == "ReleaseFrom");
        var updated = (await service.UpdateAsync(teacher.Id, Command(version: teacher.Version), default)).Value!;
        Assert.False(updated.FullyReleased);
        var archived = (await service.SetArchivedAsync(teacher.Id, updated.Version, true, default)).Value!;
        Assert.Equal(ErrorCodes.Conflict, (await service.SetArchivedAsync(teacher.Id, updated.Version, false, default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.SetArchivedAsync(999, 1, true, default)).ErrorCode);
        Assert.Contains((await new DashboardService(store).GetSummaryAsync(default)).Counts, count => count is { Key: "teachers", Value: 1 });
        Assert.Equal(ErrorCodes.Conflict, (await service.DeleteAsync(teacher.Id, updated.Version, default)).ErrorCode);
        Assert.True((await service.DeleteAsync(teacher.Id, archived.Version, default)).Succeeded);
        Assert.Equal(ErrorCodes.NotFound, (await service.DeleteAsync(teacher.Id, archived.Version, default)).ErrorCode);
    }

    [Fact]
    public async Task BulkAddPreviewsEveryLineAndCreatesOnlyReadyBatches()
    {
        var (service, store) = await SeedAsync();
        await service.CreateAsync(Command("علي كريم جاسم", "علي كريم"), default);
        string[] pasted = ["علي كريم جاسم", "", "مريم سعد", " مريم   سعد ", "علي كريم عباس", new string('ع', 151)];
        var preview = (await service.PreviewBulkAsync(new BulkTeachersCommand(pasted), default)).Value!;
        Assert.Equal(
            [(1, "exists", (string?)null), (3, "ready", "مريم سعد"), (4, "duplicateInList", null), (5, "ready", "علي كريم عباس"), (6, "tooLong", null)],
            preview.Lines.Select(line => (line.Line, line.Status, line.ShortName)));
        Assert.Equal(2, preview.ReadyCount);

        Assert.Contains((await service.CreateBulkAsync(new BulkTeachersCommand(pasted), default)).FieldErrors, error => error is { Field: "Names[0]", Code: ErrorCodes.DuplicateName });
        Assert.Contains((await service.CreateBulkAsync(new BulkTeachersCommand(pasted), default)).FieldErrors, error => error is { Field: "Names[5]", Code: ErrorCodes.ValueTooLong });
        Assert.Equal(2, (await service.CreateBulkAsync(new BulkTeachersCommand(["مريم سعد", "علي كريم عباس"]), default)).Value!.Created);
        Assert.Equal(3, await store.CountAsync(store.Query<Teacher>(), default));
        Assert.Equal(ErrorCodes.ValidationFailed, (await service.PreviewBulkAsync(new BulkTeachersCommand([" ", ""]), default)).ErrorCode);
        Assert.Contains((await service.CreateBulkAsync(new BulkTeachersCommand(Enumerable.Repeat("x", 201).ToArray()), default)).FieldErrors, error => error.Code == ErrorCodes.ValueOutOfRange);
    }

    [Fact]
    public async Task TeacherRoutesRequireTheSessionAndValidateLimits()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/teachers/")).StatusCode);
        var (token, _) = await SetupOwnerAsync(host);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PostWithoutTokenAsync("/api/v1/teachers/bulk", new { names = OneName })).StatusCode);
        var year = await AcademicYearApiTests.CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");
        var shift = await ReadAsync<ShiftDto>(await host.PostAsync($"/api/v1/academic-years/{year.Id}/shifts/", new { name = "صباحي", displayOrder = 1, version = 0 }, token));
        var lessons = new[] { new { kind = "lesson", startTime = "08:00", endTime = "08:45", startBell = true, endBell = true } };
        await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{shift.Id}/periods", new { periods = lessons, version = shift.Version }, token);

        var tooMany = await host.PostAsync("/api/v1/teachers/", new { fullName = "أحمد", shortName = "أحمد", maxLessonsPerDay = 2, maxLessonsPerWeek = 6, version = 0 }, token);
        var errors = (await AssertApiErrorAsync(tooMany, "VALIDATION_FAILED")).Errors;
        Assert.Contains(errors, issue => issue is { Field: "MaxLessonsPerDay", Code: "MAX_PER_DAY_EXCEEDS_PERIODS" });
        Assert.Contains(errors, issue => issue is { Field: "MaxLessonsPerWeek", Code: "MAX_PER_WEEK_EXCEEDS_CAPACITY" });

        var created = await ReadAsync<TeacherDto>(await host.PostAsync("/api/v1/teachers/", new
        {
            fullName = "أحمد علي", shortName = "أحمد", offDays = Monday, blockedPeriods = new[] { new { day = 7, lessonNumber = 1 } },
            fullyReleased = true, releaseReason = "تفرغ", releaseFrom = "2026-10-01", releaseTo = "2026-10-31", maxLessonsPerDay = 1, maxLessonsPerWeek = 5, version = 0,
        }, token));
        Assert.Equal([new BlockedPeriodDto(7, 1)], created.BlockedPeriods);
        Assert.Equal("2026-10-31", created.ReleaseTo);
        var preview = await ReadAsync<BulkPreviewDto>(await host.PostAsync("/api/v1/teachers/bulk/preview", new { names = PreviewNames }, token));
        Assert.Equal(1, preview.ReadyCount);
        Assert.Equal(1, (await ReadAsync<BulkCreatedDto>(await host.PostAsync("/api/v1/teachers/bulk", new { names = NewName }, token))).Created);
        Assert.Equal(1, (await ReadAsync<PagedResult<TeacherDto>>(await host.Client.GetAsync("/api/v1/teachers/?released=true"))).Total);
        Assert.Equal(2, (await ReadAsync<PagedResult<TeacherDto>>(await host.Client.GetAsync("/api/v1/teachers/"))).Total);
    }
}
