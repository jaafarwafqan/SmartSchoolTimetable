using System.Net;
using SmartSchoolTimetable.Application.Calendar;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
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
    private static readonly string[] ByDecisionKeys = ["revolution-14-july", "national-day", "victory-day"];
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
        // #87 (owner-confirmed): three fixed one-day holidays that may be announced yearly by a decision; nothing else is "by decision".
        var byDecision = IraqHolidayTemplate.Holidays.Where(item => item.ByDecision).ToArray();
        Assert.Equal(ByDecisionKeys.Order(StringComparer.Ordinal), byDecision.Select(item => item.Key).Order(StringComparer.Ordinal));
        Assert.All(byDecision, item => Assert.Equal((false, 1), (item.Hijri, item.Days)));
        Assert.Equal((7, 14), (byDecision.Single(item => item.Key == "revolution-14-july").Month, byDecision.Single(item => item.Key == "revolution-14-july").Day));
        Assert.Equal((10, 3), (byDecision.Single(item => item.Key == "national-day").Month, byDecision.Single(item => item.Key == "national-day").Day));
        Assert.Equal((12, 10), (byDecision.Single(item => item.Key == "victory-day").Month, byDecision.Single(item => item.Key == "victory-day").Day));
        // Not in the template (they stay manual through «إضافة يوم»): 6 and 16 March, 9 April.
        Assert.DoesNotContain(IraqHolidayTemplate.Holidays, item => !item.Hijri && item.Month == 3 && item.Day is 6 or 16);
        Assert.DoesNotContain(IraqHolidayTemplate.Holidays, item => !item.Hijri && item.Month == 4 && item.Day == 9);
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
    public async Task ANewOrCopiedYearGetsTheHolidaysAtOnceAndASuggestionNeverDuplicatesThem()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var expected = IraqHolidayTemplate.PlaceIn(YearStart, YearEnd).Count;

        // #87: creating the year adds the template itself (no waiting for the owner) and says how many.
        var year = await AcademicYearApiTests.CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");
        Assert.Equal(expected, year.HolidaysAdded);
        var all = await ReadAsync<PagedResult<CalendarDayDto>>(await host.Client.GetAsync("/api/v1/calendar-days/?pageSize=100"));
        Assert.Equal(expected, all.Total);
        Assert.All(all.Items, day => Assert.Equal(("officialHoliday", "iraqTemplate", true, true), (day.Kind, day.Source, day.IsEnabled, day.AffectsSchedule)));
        Assert.Contains(all.Items, day => day.IsApproximate);
        Assert.All(all.Items.Where(day => day.Title is "عيد نوروز" or "عيد العمال"), day => Assert.False(day.IsApproximate || day.ByDecision));
        // The owner-confirmed holidays carry the «by decision» note: 3 October and 10 December fall in this year, 14 July does not.
        Assert.All(all.Items.Where(day => day.Title is "اليوم الوطني العراقي" or "عيد النصر"), day => Assert.True(day.ByDecision));
        Assert.Contains(all.Items, day => day.Title == "اليوم الوطني العراقي" && day.StartDate == "2026-10-03");
        Assert.Contains(all.Items, day => day.Title == "عيد النصر" && day.StartDate == "2026-12-10");
        Assert.DoesNotContain(all.Items, day => day.Title == "ثورة ١٤ تموز");

        // The manual suggestion finds nothing new, so nothing is duplicated.
        var preview = await ReadAsync<IraqHolidayPreviewResponse>(await host.Client.GetAsync($"/api/v1/calendar-days/iraq-holidays?yearId={year.Id}"));
        Assert.Equal(expected, preview.Holidays.Count);
        Assert.All(preview.Holidays, holiday => Assert.True(holiday.AlreadyAdded));
        var again = await ReadAsync<ImportIraqHolidaysResult>(await host.PostAsync("/api/v1/calendar-days/iraq-holidays", new { yearId = year.Id }, token));
        Assert.Equal((0, expected), (again.Added, again.Skipped));

        // Edit one (no longer approximate), disable another, delete a third: the suggestion brings back only the deleted one.
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
        Assert.Equal((1, expected - 1), (second.Added, second.Skipped)); // the moved and the disabled ones count as present
        var after = await ReadAsync<PagedResult<CalendarDayDto>>(await host.Client.GetAsync("/api/v1/calendar-days/?pageSize=100"));
        Assert.Equal(expected, after.Total);
        Assert.False(after.Items.Single(day => day.Id == nowruz.Id).IsEnabled);

        // A COPIED year gets its own holidays too (this one starts in July, so 14 July falls in it; a September–June year never contains it), without touching the first year's entries.
        var copied = await host.PostAsync("/api/v1/academic-years/", new { label = "2027-2028", startDate = "2027-07-01", endDate = "2028-06-30", version = 0, copyStructureFromYearId = year.Id }, token);
        Assert.Equal(HttpStatusCode.Created, copied.StatusCode);
        var copiedYear = await ReadAsync<AcademicYearDto>(copied);
        var secondYearHolidays = IraqHolidayTemplate.PlaceIn(new DateOnly(2027, 7, 1), new DateOnly(2028, 6, 30)).Count;
        Assert.Equal(secondYearHolidays, copiedYear.HolidaysAdded);
        var both = await ReadAsync<PagedResult<CalendarDayDto>>(await host.Client.GetAsync("/api/v1/calendar-days/?pageSize=200"));
        Assert.Equal(expected + secondYearHolidays, both.Total);
        Assert.Contains(both.Items, day => day.Title == "ثورة ١٤ تموز" && day.StartDate == "2027-07-14" && day.ByDecision);
    }

    [Fact]
    public async Task EnablingRejectsStaleVersionsAndUnknownIds()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var year = await AcademicYearApiTests.CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");
        var day = (await ReadAsync<PagedResult<CalendarDayDto>>(await host.Client.GetAsync("/api/v1/calendar-days/?pageSize=100"))).Items[0];
        var disabled = await ReadAsync<CalendarDayDto>(await host.PutAsync($"/api/v1/calendar-days/{day.Id}/enabled", new { enabled = false, version = day.Version }, token));
        var enabled = await ReadAsync<CalendarDayDto>(await host.PutAsync($"/api/v1/calendar-days/{day.Id}/enabled", new { enabled = true, version = disabled.Version }, token));
        Assert.True(enabled.IsEnabled);
        Assert.Equal(HttpStatusCode.Conflict, (await host.PutAsync($"/api/v1/calendar-days/{day.Id}/enabled", new { enabled = false, version = day.Version }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PutAsync("/api/v1/calendar-days/9999/enabled", new { enabled = false, version = 1 }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PostAsync("/api/v1/calendar-days/iraq-holidays", new { yearId = 9999 }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/v1/calendar-days/iraq-holidays?yearId=9999")).StatusCode);
        Assert.True(year.HolidaysAdded > 0);
    }
}
