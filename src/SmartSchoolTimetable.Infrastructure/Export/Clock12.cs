using System.Globalization;

namespace SmartSchoolTimetable.Infrastructure.Export;

/// <summary>
/// Clock text for generated documents in the Iraqi 12-hour form (R1), the same rule as the frontend's lib/time.ts:
/// 00:00 → «١٢:٠٠ ص», 12:00 → «١٢:٠٠ م», 13:05 → «١:٠٥ م»; hours without a leading zero, minutes two digits.
/// </summary>
public static class Clock12
{
    public const string Am = "ص";
    public const string Pm = "م";

    public static string Format(int minutesAfterMidnight, bool arabicIndic)
    {
        var total = ((minutesAfterMidnight % 1440) + 1440) % 1440;
        var hours = total / 60;
        var hour = hours % 12 == 0 ? 12 : hours % 12;
        var text = string.Create(CultureInfo.InvariantCulture, $"{hour}:{total % 60:00}");
        if (arabicIndic)
            text = string.Concat(text.Select(ch => ch is >= '0' and <= '9' ? (char)('٠' + (ch - '0')) : ch));
        return $"{text} {(hours < 12 ? Am : Pm)}";
    }
}
