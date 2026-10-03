using System.Globalization;
using System.Text.Json;

namespace SmartSchoolTimetable.Application.Common;

/// <summary>API text formats: dates "yyyy-MM-dd", times "HH:mm", enum values in camelCase.</summary>
public static class InputParsing
{
    public const string DateFormat = "yyyy-MM-dd";
    public const string TimeFormat = "HH:mm";

    public static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value?.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value?.Trim(), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string? Format(DateOnly? date) => date is { } value ? Format(value) : null;

    public static string Format(TimeOnly time) => time.ToString(TimeFormat, CultureInfo.InvariantCulture);
}

/// <summary>Converts Domain enums to and from their API representation (camelCase names), so DTOs never
/// expose Domain types and the Api assembly stays independent of Domain.</summary>
public static class ApiText
{
    public static string ToValue<TEnum>(TEnum value) where TEnum : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    public static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(ToValue(candidate), value.Trim(), StringComparison.Ordinal))
            {
                result = candidate;
                return true;
            }
        }
        return false;
    }

    public static IReadOnlyList<string> Values<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(ToValue).ToArray();
}
