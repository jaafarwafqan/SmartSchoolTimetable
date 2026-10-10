using System.Net;
using SmartSchoolTimetable.Application.Calendar;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Calendar;
using SmartSchoolTimetable.Tests.Phase2;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>MF8: the Iraqi official-holidays template (Gregorian and Umm al-Qura dates), its import per year, and enabling/disabling entries.</summary>
public sealed class CalendarHolidaysTests
{
    private static readonly DateOnly YearStart = new(2026, 9, 1);
    private static readonly DateOnly YearEnd = new(2027, 6, 30);
    private static readonly string[] FixedKeys = ["christmas", "new-year", "army-day", "nowruz", "labour-day"];
    private static readonly string[] CalculatedKeys = ["eid-al-fitr", "eid-al-adha", "ghadir"];

    [Fact]
    public void TemplateKeysAreUniqueAndEveryEntryIsValid()
    {
        Assert.Equal(IraqHolidayTemplate.Holidays.Count, IraqHolidayTemplate.Holidays.Select(item => item.Key).Distinct().Count());
        foreach (var holiday in IraqHolidayTemplate.Holidays)
        {
            Assert.InRange(holiday.Title.Length, 1, CalendarDay.TitleMaxLength);
            Assert.InRange(holiday.Month, 1, 12);
            Assert.InRange(holiday.Day, 1, 30);
            Assert.InRange(holiday.Days, 1, 4);
        }
        // Disputed holidays are not part of the template (they are documented in docs/IRAQ_HOLIDAYS.md and added by hand).
        Assert.DoesNotContain(IraqHolidayTemplate.Holidays, item => item.Key is "republic-day" or "national-day" or "victory-day");
    }

    [Fact]
    public void GregorianHolidaysAreFixedAndHijriHolidaysAreCalculatedAndApproximate()
    {
        var placed = IraqHolidayTemplate.PlaceIn(YearStart, YearEnd).ToDictionary(item => item.Holiday.Key);

        Assert.Equal(new DateOnly(2026, 12, 25), placed["christmas"].Start);
        Assert.Equal(new DateOnly(2027, 1, 1), placed["new-year"].Start);
        Assert.Equal(new DateOnly(2027, 1, 6), placed["army-day"].Start);
        Assert.Equal(new DateOnly(2027, 3, 21), placed["nowruz"].Start);
        Assert.Equal(new DateOnly(2027, 5, 1), placed["labour-day"].Start);
        Assert.All(FixedKeys, key => Assert.False(placed[key].Approximate));

        // Umm al-Qura: 1 Shawwal 1448 is in March 2027 (three days), 10 Dhu al-Hijjah 1448 in May 2027 (four days).
        var fitr = placed["eid-al-fitr"];
        Assert.Equal((2027, 3), (fitr.Start.Year, fitr.Start.Month));
        Assert.Equal(2, fitr.End.DayNumber - fitr.Start.DayNumber);
        var adha = placed["eid-al-adha"];
        Assert.Equal((2027, 5), (adha.Start.Year, adha.Start.Month));
        Assert.Equal(3, adha.End.DayNumber - adha.Start.DayNumber);
        Assert.True(placed["ghadir"].Start > adha.End);
        Assert.All(CalculatedKeys, key => Assert.True(placed[key].Approximate));
        // 1 Muharram 1449 (early June 2027) and 10 Muharram fall at the end of the year; 1 Muharram 1448 (June 2026) is before it.
        Assert.Equal((2027, 6), (placed["hijri-new-year"].Start.Year, placed["hijri-new-year"].Start.Month));
        Assert.Equal(9, placed["ashura"].Start.DayNumber - placed["hijri-new-year"].Start.DayNumber);
        Assert.DoesNotContain("mawlid", placed.Keys);        // 12 Rabi' al-awwal 1448 is in August 2026, before the year
    }

    [Fact]
    public void ARangeOutsideTheCalendarTableGivesNoHijriDates()
    {
        var placed = IraqHolidayTemplate.PlaceIn(new DateOnly(2090, 9, 1), new DateOnly(2091, 6, 30));
        Assert.DoesNotContain(placed, item => item.Holiday.Hijri);
        Assert.Contains(placed, item => item.Holiday.Key == "new-year");
    }

    [Fact]
    public async Task ImportAddsMissingHolidaysOnceAndKeepsEditedAndDisabledEntries()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var year = await AcademicYearApiTests.CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");
        var expected = IraqHolidayTemplate.PlaceIn(YearStart, YearEnd).Count;

        var preview = await ReadAsync<IraqHolidayPreviewResponse>(await host.Client.GetAsync($"/api/v1/calendar-days/iraq-holidays?yearId={year.Id}"));
        Assert.Equal(expected, preview.Holidays.Count);
        Assert.All(preview.Holidays, holiday => Assert.False(holiday.AlreadyAdded));

        var first = await ReadAsync<ImportIraqHolidaysResult>(await host.PostAsync("/api/v1/calendar-days/iraq-holidays", new { yearId = year.Id }, token));
        Assert.Equal((expected, 0), (first.Added, first.Skipped));
        var all = await ReadAsync<PagedResult<CalendarDayDto>>(await host.Client.GetAsync("/api/v1/calendar-days/"));
        Assert.Equal(expected, all.Total);
        Assert.All(all.Items, day => Assert.Equal(("officialHoliday", "iraqTemplate", true, true), (day.Kind, day.Source, day.IsEnabled, day.AffectsSchedule)));
        Assert.Contains(all.Items, day => day.IsApproximate);
        Assert.Contains(all.Items, day => !day.IsApproximate);

        // Edit one (the date becomes the owner's: no longer approximate), disable another, delete a third; import again.
        var fitr = all.Items.Single(day => day.Title.StartsWith("عيد الفطر", StringComparison.Ordinal));
        var edited = await ReadAsync<CalendarDayDto>(await host.PutAsync($"/api/v1/calendar-days/{fitr.Id}",
            new { title = fitr.Title, startDate = "2027-03-11", endDate = "2027-03-14", kind = "officialHoliday", affectsSchedule = true, version = fitr.Version }, token));
        Assert.False(edited.IsApproximate);
        var nowruz = all.Items.Single(day => day.Title == "عيد نوروز");
        var disabled = await ReadAsync<CalendarDayDto>(await host.PutAsync($"/api/v1/calendar-days/{nowruz.Id}/enabled", new { enabled = false, version = nowruz.Version }, token));
        Assert.Equal((false, false), (disabled.IsEnabled, disabled.AffectsSchedule));
        var labour = all.Items.Single(day => day.Title == "عيد العمال");
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendJsonAsync(HttpMethod.Delete, $"/api/v1/calendar-days/{labour.Id}?version={labour.Version}", new { }, token)).StatusCode);

        var second = await ReadAsync<ImportIraqHolidaysResult>(await host.PostAsync("/api/v1/calendar-days/iraq-holidays", new { yearId = year.Id }, token));
        Assert.Equal((1, expected - 1), (second.Added, second.Skipped)); // only the deleted one comes back; the moved and the disabled ones count as present
        var after = await ReadAsync<PagedResult<CalendarDayDto>>(await host.Client.GetAsync("/api/v1/calendar-days/"));
        Assert.Equal(expected, after.Total);
        Assert.False(after.Items.Single(day => day.Id == nowruz.Id).IsEnabled);

        // Enabling again; stale versions conflict; unknown ids and years are not found.
        var enabled = await ReadAsync<CalendarDayDto>(await host.PutAsync($"/api/v1/calendar-days/{nowruz.Id}/enabled", new { enabled = true, version = disabled.Version }, token));
        Assert.True(enabled.IsEnabled);
        Assert.Equal(HttpStatusCode.Conflict, (await host.PutAsync($"/api/v1/calendar-days/{nowruz.Id}/enabled", new { enabled = false, version = nowruz.Version }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PutAsync("/api/v1/calendar-days/9999/enabled", new { enabled = false, version = 1 }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PostAsync("/api/v1/calendar-days/iraq-holidays", new { yearId = 9999 }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/v1/calendar-days/iraq-holidays?yearId=9999")).StatusCode);
    }
}
