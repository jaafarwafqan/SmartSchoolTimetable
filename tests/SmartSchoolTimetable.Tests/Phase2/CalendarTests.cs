using System.Net;
using SmartSchoolTimetable.Application.Calendar;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Calendar;
using SmartSchoolTimetable.Domain.Common;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase2;

/// <summary>The academic calendar: domain validation and the calendar routes.</summary>
public sealed class CalendarTests
{
    [Fact]
    public void CalendarDayValidatesRangeTitleAndKind()
    {
        var errors = Assert.Throws<DomainValidationException>(() =>
            CalendarDay.Create(" ", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 1), (CalendarDayKind)9, true)).Errors;
        Assert.Equal(["Title", "EndDate", "Kind"], errors.Select(error => error.Field));
        Assert.Contains(Assert.Throws<DomainValidationException>(() =>
            CalendarDay.Create("x", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 5), CalendarDayKind.Exam, true)).Errors, error => error.Code == DomainErrorCode.OutOfRange);

        var day = CalendarDay.Create("عطلة", new DateOnly(2026, 10, 3), null, CalendarDayKind.OfficialHoliday, true);
        Assert.Equal(day.StartDate, day.EndDate);
        Assert.False(day.IsOutside(new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));
        day.Update("امتحانات", new DateOnly(2027, 6, 25), new DateOnly(2027, 7, 5), CalendarDayKind.Exam, false);
        Assert.True(day.IsOutside(new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));
        Assert.Equal(2, day.Version);
    }

    [Fact]
    public async Task CalendarRoutesWarnOutsideTheYearAndFilterByMonth()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/calendar-days/")).StatusCode);
        var (token, _) = await SetupOwnerAsync(host);
        var noYear = await ReadAsync<CalendarDayDto>(await host.PostAsync("/api/v1/calendar-days/", new { title = "يوم", startDate = "2020-01-01", kind = "specialDay", version = 0 }, token));
        Assert.False(noYear.OutsideCurrentYear); // nothing to compare with yet
        await AcademicYearApiTests.CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");

        var invalid = await host.PostAsync("/api/v1/calendar-days/", new { title = "", startDate = "2026-13-01", kind = "party", version = 0 }, token);
        var fields = (await AssertApiErrorAsync(invalid, "VALIDATION_FAILED")).Errors.Select(issue => (issue.Field, issue.Code)).ToArray();
        Assert.Contains(("StartDate", "INVALID_DATE"), fields);
        Assert.Contains(("Kind", "INVALID_OPTION"), fields);

        var holiday = await ReadAsync<CalendarDayDto>(await host.PostAsync("/api/v1/calendar-days/", new { title = "عطلة نصف السنة", startDate = "2027-01-21", endDate = "2027-02-04", kind = "schoolHoliday", affectsSchedule = true, version = 0 }, token));
        Assert.False(holiday.OutsideCurrentYear);
        var outside = await ReadAsync<CalendarDayDto>(await host.PostAsync("/api/v1/calendar-days/", new { title = "صيف", startDate = "2027-07-01", kind = "schoolHoliday", version = 0 }, token));
        Assert.True(outside.OutsideCurrentYear); // warned, not blocked

        var february = await ReadAsync<PagedResult<CalendarDayDto>>(await host.Client.GetAsync("/api/v1/calendar-days/?from=2027-02-01&to=2027-02-28"));
        Assert.Equal("عطلة نصف السنة", Assert.Single(february.Items).Title);
        await AssertApiErrorAsync(await host.Client.GetAsync("/api/v1/calendar-days/?from=feb"), "VALIDATION_FAILED");
        Assert.Equal(3, (await ReadAsync<PagedResult<CalendarDayDto>>(await host.Client.GetAsync("/api/v1/calendar-days/?sort=-startDate"))).Total);

        var updated = await ReadAsync<CalendarDayDto>(await host.PutAsync($"/api/v1/calendar-days/{outside.Id}", new { title = "صيف", startDate = "2027-06-30", kind = "officialHoliday", version = outside.Version }, token));
        Assert.Equal(("officialHoliday", false), (updated.Kind, updated.OutsideCurrentYear));
        Assert.Equal(HttpStatusCode.Conflict, (await host.PutAsync($"/api/v1/calendar-days/{outside.Id}", new { title = "x", startDate = "2027-06-30", kind = "exam", version = outside.Version }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PutAsync("/api/v1/calendar-days/999", new { title = "x", startDate = "2027-06-30", kind = "exam", version = 1 }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.SendJsonAsync(HttpMethod.Delete, $"/api/v1/calendar-days/{outside.Id}?version={outside.Version}", new { }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendJsonAsync(HttpMethod.Delete, $"/api/v1/calendar-days/{outside.Id}?version={updated.Version}", new { }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.SendJsonAsync(HttpMethod.Delete, $"/api/v1/calendar-days/{outside.Id}?version={updated.Version}", new { }, token)).StatusCode);
    }
}
