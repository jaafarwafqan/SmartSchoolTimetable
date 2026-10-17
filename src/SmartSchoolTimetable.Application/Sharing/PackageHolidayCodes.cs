using SmartSchoolTimetable.Application.Calendar;

namespace SmartSchoolTimetable.Application.Sharing;

/// <summary>
/// Short codes for the holidays of the Iraqi template (docs/SST_PACKAGE_FORMAT.md §4.1, DECISIONS_PENDING #115). A calendar entry that
/// came from the template with its title unchanged travels as its code instead of its Arabic title, which keeps a full-time teacher's
/// package inside one QR code; the phone has the same table and shows the same title. Codes are fixed per template key, never
/// renumbered; a new template holiday gets the next free code. A renamed or hand-made entry keeps its own title.
/// </summary>
public static class PackageHolidayCodes
{
    private static readonly Dictionary<string, int> CodeByKey = new(StringComparer.Ordinal)
    {
        ["new-year"] = 1,
        ["army-day"] = 2,
        ["nowruz"] = 3,
        ["labour-day"] = 4,
        ["christmas"] = 5,
        ["revolution-14-july"] = 6,
        ["national-day"] = 7,
        ["victory-day"] = 8,
        ["hijri-new-year"] = 9,
        ["ashura"] = 10,
        ["mawlid"] = 11,
        ["eid-al-fitr"] = 12,
        ["eid-al-adha"] = 13,
        ["ghadir"] = 14,
    };

    /// <summary>Code → (template key, Arabic title), the table the phone app bundles.</summary>
    public static IReadOnlyDictionary<int, (string Key, string Title)> Table { get; } =
        IraqHolidayTemplate.Holidays.Where(holiday => CodeByKey.ContainsKey(holiday.Key))
            .ToDictionary(holiday => CodeByKey[holiday.Key], holiday => (holiday.Key, holiday.Title));

    /// <summary>The code of a template entry whose title is still the template's; 0 when it has none (it then keeps its own title).</summary>
    public static int CodeOf(string? templateKey, string title) =>
        templateKey is not null && CodeByKey.TryGetValue(templateKey, out var code) && Table[code].Title == title ? code : 0;

    /// <summary>The title of a code, or null for a code this build does not know (a newer desktop): the phone then shows «عطلة».</summary>
    public static string? TitleOf(int code) => Table.TryGetValue(code, out var entry) ? entry.Title : null;
}
