using System.Globalization;
using SmartSchoolTimetable.Domain;

namespace SmartSchoolTimetable.Application;

/// <summary>Parses the API representation of the inactivity auto-lock choice: minutes as text, or "never".</summary>
public static class InactivityTimeoutChoices
{
    public const string Never = "never";

    public static IReadOnlyList<int> Minutes => OwnerAccount.InactivityTimeoutChoicesMinutes;

    public static bool TryParse(string? value, out int? minutes)
    {
        minutes = null;
        if (string.Equals(value, Never, StringComparison.Ordinal))
            return true;
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
            Minutes.Contains(parsed))
        {
            minutes = parsed;
            return true;
        }
        return false;
    }
}
