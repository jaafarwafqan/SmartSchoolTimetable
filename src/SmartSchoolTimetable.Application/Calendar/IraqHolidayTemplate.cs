using System.Globalization;

namespace SmartSchoolTimetable.Application.Calendar;

/// <summary>One holiday of the template: a fixed Gregorian date, or a Hijri date converted with the Umm al-Qura calendar.</summary>
/// <param name="Key">Stable key stored on the calendar entry (never shown).</param>
/// <param name="Title">The Arabic title shown in the calendar.</param>
/// <param name="Hijri">True when the date follows the Hijri calendar; the Gregorian date is then a calculation.</param>
/// <param name="Month">Month of the year in the entry's calendar.</param>
/// <param name="Day">First day of the holiday.</param>
/// <param name="Days">Number of days from <paramref name="Day"/> (1 for a single day).</param>
/// <param name="ByDecision">The date is fixed but whether the day is a holiday is announced yearly by a decision (owner-confirmed, #87).</param>
public sealed record IraqHoliday(string Key, string Title, bool Hijri, int Month, int Day, int Days = 1, bool ByDecision = false);

/// <summary>A holiday placed on a date range of one academic year.</summary>
public sealed record PlacedHoliday(IraqHoliday Holiday, DateOnly Start, DateOnly End)
{
    public bool Approximate => Holiday.Hijri;
    public bool ByDecision => Holiday.ByDecision;
}

/// <summary>
/// MF8: the Iraqi official-holidays template: the holidays that the Official Holidays Law No. 12 of 2024 lists (as summarised
/// by public sources), Christmas (cabinet decision, 2018), and three the owner confirmed (14 July, 3 October, 10 December;
/// DECISIONS #87), which are marked «by decision» because they may be announced yearly. The other disputed days (6 and 16
/// March, 9 April, regional or sect-specific days) are added by hand. Hijri dates are calculated with the Umm al-Qura
/// calendar, whereas Iraq announces them by moon sighting, so they are approximate. Documented in docs/IRAQ_HOLIDAYS.md.
/// </summary>
public static class IraqHolidayTemplate
{
    public static readonly IReadOnlyList<IraqHoliday> Holidays =
    [
        new("new-year", "رأس السنة الميلادية", false, 1, 1),
        new("army-day", "عيد الجيش العراقي", false, 1, 6),
        new("nowruz", "عيد نوروز", false, 3, 21),
        new("labour-day", "عيد العمال", false, 5, 1),
        new("christmas", "عيد الميلاد المجيد", false, 12, 25),
        new("revolution-14-july", "ثورة ١٤ تموز", false, 7, 14, ByDecision: true),
        new("national-day", "اليوم الوطني العراقي", false, 10, 3, ByDecision: true),
        new("victory-day", "عيد النصر", false, 12, 10, ByDecision: true),
        new("hijri-new-year", "رأس السنة الهجرية", true, 1, 1),
        new("ashura", "يوم عاشوراء", true, 1, 10),
        new("mawlid", "المولد النبوي الشريف", true, 3, 12),
        new("eid-al-fitr", "عيد الفطر المبارك", true, 10, 1, 3),
        new("eid-al-adha", "عيد الأضحى المبارك", true, 12, 10, 4),
        new("ghadir", "عيد الغدير", true, 12, 18),
    ];

    private static readonly UmAlQuraCalendar UmAlQura = new();

    /// <summary>The template's holidays that fall (at least partly) inside [start, end], in date order.</summary>
    public static IReadOnlyList<PlacedHoliday> PlaceIn(DateOnly start, DateOnly end)
    {
        var placed = new List<PlacedHoliday>();
        foreach (var holiday in Holidays)
        {
            foreach (var first in StartsOf(holiday, start, end))
            {
                var last = first.AddDays(holiday.Days - 1);
                if (last >= start && first <= end)
                    placed.Add(new PlacedHoliday(holiday, first, last));
            }
        }
        return placed.OrderBy(item => item.Start).ThenBy(item => item.Holiday.Key, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<DateOnly> StartsOf(IraqHoliday holiday, DateOnly start, DateOnly end)
    {
        if (!holiday.Hijri)
        {
            for (var year = start.Year; year <= end.Year; year++)
                yield return new DateOnly(year, holiday.Month, holiday.Day);
            yield break;
        }
        var min = DateOnly.FromDateTime(UmAlQura.MinSupportedDateTime);
        var max = DateOnly.FromDateTime(UmAlQura.MaxSupportedDateTime);
        if (start < min || end > max)
            yield break; // outside the calendar's table: no calculated date rather than a wrong one
        var from = UmAlQura.GetYear(start.ToDateTime(TimeOnly.MinValue));
        var to = UmAlQura.GetYear(end.ToDateTime(TimeOnly.MinValue));
        for (var year = from; year <= to; year++)
            yield return DateOnly.FromDateTime(UmAlQura.ToDateTime(year, holiday.Month, holiday.Day, 0, 0, 0, 0));
    }
}
