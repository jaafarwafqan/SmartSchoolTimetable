using System.Net;
using System.Text.Json;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Setup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Tests.Phase3;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>
/// MF7 (owner decision, reverses #82): sections and teachers never differ by shift; only times differ. One shift per year,
/// one control «نظام الدوام: صباحي / مسائي / مزدوج», and a guided one-time conversion of the old two-shift layout.
/// </summary>
public sealed class ShiftSystemTests
{
    private static readonly int[] Days = [7, 1, 2, 3, 4];

    private static object Timing(string kind, int lessons = 6, string start = "08:00") =>
        new { kind, firstStartTime = start, lessonMinutes = 40, lessonCount = lessons, breaks = new[] { new { afterLesson = 3, minutes = 15 } }, dayLessons = Array.Empty<object>() };

    private static object[] Mapping() => [.. Days.SelectMany((day, index) => new object[]
    {
        new { term = 1, day, session = index < 2 ? "morning" : "evening" },
        new { term = 2, day, session = index < 2 ? "evening" : "morning" },
    })];

    private static async Task<IReadOnlyList<ShiftDto>> ShiftsAsync(TestHost host, string root) =>
        (await ReadAsync<PagedResult<ShiftDto>>(await host.Client.GetAsync($"{root}/shifts/?pageSize=100"))).Items;

    private static async Task<IReadOnlyList<SectionDto>> SectionsAsync(TestHost host, string root, long stageId) =>
        (await ReadAsync<PagedResult<SectionDto>>(await host.Client.GetAsync($"{root}/stages/{stageId}/sections?pageSize=100&includeArchived=true"))).Items;

    [Fact]
    public async Task SwitchingTheSystemKeepsOneShiftAndNeverMovesTheSections()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host);
        var sections = await SectionsAsync(host, school.Root, school.Stage.Id);
        Assert.Equal(("morning", false), ((await ReadAsync<ShiftSystemDto>(await host.Client.GetAsync("/api/v1/shift-system/"))).System, false));

        // Evening only: the same shift becomes the evening one, with the evening clock.
        var evening = await ReadAsync<ShiftSystemDto>(await host.PutAsync("/api/v1/shift-system/", new { system = "evening", main = Timing("evening", 6, "13:00") }, school.Token));
        var shift = (await ShiftsAsync(host, school.Root)).Single();
        Assert.Equal(("evening", false, shift.Id), (evening.System, evening.Legacy, evening.ShiftId!.Value));
        Assert.Equal(("evening", "الدوام المسائي", "13:00"), (shift.Kind, shift.Name, shift.Periods[0].StartTime));
        Assert.Equal(sections.Select(section => section.Id), (await SectionsAsync(host, school.Root, school.Stage.Id)).Where(section => section.ShiftId == shift.Id).Select(section => section.Id));

        // Double: morning timing on the shift, an evening session with the same lessons, and the mapping per semester.
        var dual = await ReadAsync<ShiftSystemDto>(await host.PutAsync("/api/v1/shift-system/", new
        {
            system = "dual", main = Timing("morning"), evening = Timing("evening", 3, "13:00"), sessionDays = Mapping(),
        }, school.Token));
        Assert.Equal("dual", dual.System);
        shift = (await ShiftsAsync(host, school.Root)).Single();
        Assert.Equal(("morning", "الدوام المزدوج", 6), (shift.Kind, shift.Name, shift.LessonCount));
        var plan = await ReadAsync<SessionPlanDto>(await host.Client.GetAsync("/api/v1/session-plan/"));
        Assert.Equal("twoSessions", plan.System);
        var eveningTiming = plan.Timings.Single(timing => timing.Session == "evening").Periods;
        Assert.Equal((6, "13:00"), (eveningTiming.Count(period => period.Kind == "lesson"), eveningTiming[0].StartTime)); // the main lesson count, not 3
        Assert.Equal("evening", plan.Days.Single(day => day is { Term: 1, Day: 2 }).Session);
        Assert.Equal("morning", plan.Days.Single(day => day is { Term: 2, Day: 2 }).Session);

        // Back to morning: the evening session goes, the sections stay where they are.
        Assert.Equal("morning", (await ReadAsync<ShiftSystemDto>(await host.PutAsync("/api/v1/shift-system/", new { system = "morning", main = Timing("morning") }, school.Token))).System);
        Assert.Equal("oneSession", (await ReadAsync<SessionPlanDto>(await host.Client.GetAsync("/api/v1/session-plan/"))).System);
        Assert.Equal(("morning", "الدوام الصباحي"), ((await ShiftsAsync(host, school.Root)).Single().Kind, (await ShiftsAsync(host, school.Root)).Single().Name));
        Assert.All(await SectionsAsync(host, school.Root, school.Stage.Id), section => Assert.Equal(shift.Id, section.ShiftId));

        // Validation: an unknown system, and «مزدوج» without its evening timing and mapping.
        Assert.Contains((await AssertApiErrorAsync(await host.PutAsync("/api/v1/shift-system/", new { system = "weekend" }, school.Token), ErrorCodes.ValidationFailed)).Errors,
            issue => issue.Field == "System");
        Assert.Contains((await AssertApiErrorAsync(await host.PutAsync("/api/v1/shift-system/", new { system = "dual", main = Timing("morning") }, school.Token), ErrorCodes.ValidationFailed)).Errors,
            issue => issue.Field == "Evening");
        Assert.Equal("oneSession", (await ReadAsync<SessionPlanDto>(await host.Client.GetAsync("/api/v1/session-plan/"))).System); // rolled back
    }

    /// <summary>An old database: two shifts with sections on each (the layout removed from the UI).</summary>
    private static async Task<(ReferenceProtectionTests.School School, long EveningId)> LegacyAsync(TestHost host)
    {
        var school = await ReferenceProtectionTests.SeedAsync(host);
        var created = await host.PostAsync($"{school.Root}/shifts/", new { name = "الدوام المسائي", displayOrder = 2, version = 0 }, school.Token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var evening = await ReadAsync<ShiftDto>(created);
        var lessons = Enumerable.Range(0, 5).Select(index => new { kind = "lesson", startTime = $"{13 + index:00}:00", endTime = $"{13 + index:00}:40", startBell = true, endBell = true }).ToArray();
        await ReadAsync<ShiftDto>(await host.PutAsync($"{school.Root}/shifts/{evening.Id}/periods", new { periods = lessons, version = evening.Version }, school.Token));
        var second = (await SectionsAsync(host, school.Root, school.Stage.Id)).OrderBy(section => section.Id).Last();
        var moved = await host.PutAsync($"{school.Root}/stages/{school.Stage.Id}/sections/{second.Id}", new { label = second.Label, shiftId = evening.Id, studentCount = (int?)null, version = second.Version }, school.Token);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        return (school, evening.Id);
    }

    [Fact]
    public async Task TheOldTwoShiftLayoutIsDetectedAndBlockedUntilConverted()
    {
        await using var host = new TestHost();
        var (school, _) = await LegacyAsync(host);
        var state = await ReadAsync<ShiftSystemDto>(await host.Client.GetAsync("/api/v1/shift-system/"));
        Assert.True(state.Legacy);
        Assert.Equal([1, 1], state.LegacyShifts.Select(shift => shift.Sections));
        await AssertApiErrorAsync(await host.PutAsync("/api/v1/shift-system/", new { system = "morning", main = Timing("morning") }, school.Token), ErrorCodes.ShiftSystemLegacy);
        Assert.Contains((await AssertApiErrorAsync(await host.PostAsync("/api/v1/shift-system/convert", new { targetSystem = "twice" }, school.Token), ErrorCodes.ValidationFailed)).Errors,
            issue => issue.Field == "TargetSystem");
    }

    [Fact]
    public async Task ConvertingToDoubleBacksUpFirstMovesEverySectionAndKeepsTheEveningClock()
    {
        await using var host = new TestHost();
        var (school, eveningId) = await LegacyAsync(host);
        var converted = await host.PostAsync("/api/v1/shift-system/convert", new { targetSystem = "dual" }, school.Token);
        Assert.Equal(HttpStatusCode.OK, converted.StatusCode);
        using var body = JsonDocument.Parse(await converted.Content.ReadAsStringAsync());
        var backup = body.RootElement.GetProperty("automaticBackupPath").GetString()!;
        Assert.True(File.Exists(backup));
        Assert.StartsWith("pre-conversion-", Path.GetFileName(backup), StringComparison.Ordinal);
        Assert.Equal(("dual", false), (body.RootElement.GetProperty("system").GetProperty("system").GetString(), body.RootElement.GetProperty("system").GetProperty("legacy").GetBoolean()));

        var shift = (await ShiftsAsync(host, school.Root)).Single();
        Assert.NotEqual(eveningId, shift.Id);
        Assert.Equal("الدوام المزدوج", shift.Name);
        Assert.All(await SectionsAsync(host, school.Root, school.Stage.Id), section => Assert.Equal(shift.Id, section.ShiftId));
        var plan = await ReadAsync<SessionPlanDto>(await host.Client.GetAsync("/api/v1/session-plan/"));
        Assert.Equal("twoSessions", plan.System);
        var evening = plan.Timings.Single(timing => timing.Session == "evening").Periods.Where(period => period.Kind == "lesson").ToArray();
        Assert.Equal((shift.LessonCount, "13:00", "13:40"), (evening.Length, evening[0].StartTime, evening[0].EndTime));
        Assert.Equal(10, plan.Days.Count); // 5 working days × 2 semesters

        var audit = await ReadAsync<PagedResult<JsonElement>>(await host.Client.GetAsync("/api/v1/audit/?eventType=ShiftSystemConverted"));
        Assert.Equal("dual", audit.Items.Single().GetProperty("params").GetProperty("system").GetString());
        await AssertApiErrorAsync(await host.PostAsync("/api/v1/shift-system/convert", new { targetSystem = "dual" }, school.Token), ErrorCodes.Conflict);
    }

    [Fact]
    public async Task ConvertingToEveningKeepsTheEveningShiftAndItsTimes()
    {
        await using var host = new TestHost();
        var (school, eveningId) = await LegacyAsync(host);
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync("/api/v1/shift-system/convert", new { targetSystem = "evening" }, school.Token)).StatusCode);
        var shift = (await ShiftsAsync(host, school.Root)).Single();
        Assert.Equal(("evening", "الدوام المسائي", 5), (shift.Kind, shift.Name, shift.LessonCount));
        Assert.Equal("evening", (await ReadAsync<ShiftSystemDto>(await host.Client.GetAsync("/api/v1/shift-system/"))).System);
        Assert.All(await SectionsAsync(host, school.Root, school.Stage.Id), section => Assert.Equal(shift.Id, section.ShiftId));
        Assert.Equal("oneSession", (await ReadAsync<SessionPlanDto>(await host.Client.GetAsync("/api/v1/session-plan/"))).System);
    }
}
