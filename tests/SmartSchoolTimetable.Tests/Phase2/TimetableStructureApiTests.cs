using System.Net;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase2;

public sealed class TimetableStructureApiTests
{
    private const string WeekPath = "/api/v1/working-days/";
    private static readonly int[] SavedDays = [7, 1, 2, 3, 4, 5];
    private static readonly int[] StaleDays = [7, 1, 2, 3, 4];
    private static readonly int[] DefaultDays = [7, 1, 2, 3, 4];

    [Fact]
    public async Task WorkingDaysAndBellSettingsRequireSessionAndPersistVersionedChanges()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(WeekPath)).StatusCode);
        var (token, _) = await SetupOwnerAsync(host);
        var week = await ReadAsync<WorkingWeekDto>(await host.Client.GetAsync(WeekPath));
        Assert.Equal(DefaultDays, week.Days);
        var invalid = await host.PutAsync(WeekPath, new { days = Array.Empty<int>(), weekStartDay = 7, version = week.Version }, token);
        Assert.Contains((await AssertApiErrorAsync(invalid, "VALIDATION_FAILED")).Errors, issue => issue.Code == "NO_WORKING_DAYS");
        var saved = await ReadAsync<WorkingWeekDto>(await host.PutAsync(WeekPath, new { days = SavedDays, weekStartDay = 7, version = week.Version }, token));
        Assert.Equal(6, saved.Days.Count);
        Assert.Equal(HttpStatusCode.Conflict, (await host.PutAsync(WeekPath, new { days = StaleDays, weekStartDay = 7, version = week.Version }, token)).StatusCode);

        var bell = await ReadAsync<BellSettingsDto>(await host.Client.GetAsync("/api/v1/bell-settings/"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClientWithoutCookies().GetAsync("/api/v1/bell-settings/")).StatusCode);
        var invalidTone = await host.PutAsync("/api/v1/bell-settings/", new { tone = "Unknown", breakBell = true, version = bell.Version }, token);
        Assert.Contains((await AssertApiErrorAsync(invalidTone, "VALIDATION_FAILED")).Errors, issue => issue.Code == "INVALID_OPTION");
        var savedBellResponse = await host.PutAsync("/api/v1/bell-settings/", new { tone = "chime", breakBell = false, version = bell.Version }, token);
        Assert.Equal(HttpStatusCode.OK, savedBellResponse.StatusCode);
        var savedBell = await ReadAsync<BellSettingsDto>(savedBellResponse);
        Assert.Equal("chime", savedBell.Tone);
        Assert.False(savedBell.BreakBell);
    }

    [Fact]
    public async Task ShiftsValidatePeriodsSearchAndCopyAcrossYears()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var year = await AcademicYearApiTests.CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");
        var create = await host.PostAsync($"/api/v1/academic-years/{year.Id}/shifts/", new { name = "ØµØ¨Ø§Ø­ÙŠ", displayOrder = 1, version = 0 }, token);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var shift = await ReadAsync<ShiftDto>(create);
        var bad = await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{shift.Id}/periods", new { periods = new[] { new { kind = "lesson", startTime = "09:00", endTime = "10:00", startBell = true, endBell = true }, new { kind = "lesson", startTime = "08:00", endTime = "09:00", startBell = true, endBell = true } }, version = shift.Version }, token);
        Assert.Contains((await AssertApiErrorAsync(bad, "VALIDATION_FAILED")).Errors, issue => issue.Code == "PERIODS_NOT_ASCENDING");
        var periods = new[] {
            new { kind = "lesson", startTime = "08:00", endTime = "08:45", startBell = true, endBell = true },
            new { kind = "break", startTime = "08:45", endTime = "09:00", startBell = false, endBell = false },
            new { kind = "lesson", startTime = "09:00", endTime = "09:45", startBell = true, endBell = true }
        };
        shift = await ReadAsync<ShiftDto>(await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{shift.Id}/periods", new { periods, version = shift.Version }, token));
        Assert.Equal(2, shift.LessonCount);
        Assert.Equal(HttpStatusCode.Conflict, (await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{shift.Id}/periods", new { periods, version = 0 }, token)).StatusCode);
        var generated = await ReadAsync<GeneratedPeriodsDto>(await host.PostAsync($"/api/v1/academic-years/{year.Id}/shifts/generate-periods", new { firstStartTime = "08:00", lessonMinutes = 45, lessonCount = 7, breakMinutes = 15, breakAfterLesson = 4 }, token));
        Assert.Equal(8, generated.Periods.Count);
        Assert.Equal("break", generated.Periods[4].Kind);

        var copiedYear = await ReadAsync<AcademicYearDto>(await host.PostAsync("/api/v1/academic-years/", new { label = "2027-2028", startDate = "2027-09-01", endDate = "2028-06-30", version = 0, copyStructureFromYearId = year.Id }, token));
        var copied = await ReadAsync<PagedResult<ShiftDto>>(await host.Client.GetAsync($"/api/v1/academic-years/{copiedYear.Id}/shifts/"));
        Assert.Single(copied.Items);
        Assert.Equal(2, copied.Items[0].LessonCount);
    }
}
