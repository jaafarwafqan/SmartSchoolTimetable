using System.Text;

namespace SmartSchoolTimetable.Domain.Text;

/// <summary>
/// Normalizes Arabic text for comparison (uniqueness checks and search). Stored values keep the
/// user's original spelling; only the normalized companion column uses this form.
/// </summary>
public static class ArabicText
{
    private const char Tatweel = 'ـ';

    /// <summary>Trim, collapse spaces, drop tatweel and diacritics, unify alef and ya forms, fold digits and case.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var raw in value.Trim())
        {
            if (char.IsWhiteSpace(raw))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }
            if (raw == Tatweel || IsDiacritic(raw))
                continue;

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(Fold(raw));
        }
        return builder.ToString();
    }

    /// <summary>Removes surrounding spaces and collapses inner runs of whitespace; keeps the spelling.</summary>
    public static string Clean(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static bool IsDiacritic(char character) =>
        character is >= 'ً' and <= 'ٟ' or 'ٰ' or >= 'ۖ' and <= 'ۭ';

    private static char Fold(char character) => character switch
    {
        'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', // أ إ آ ٱ -> ا
        'ى' => 'ي', // ى -> ي
        >= '٠' and <= '٩' => (char)('0' + (character - '٠')), // Arabic-Indic digits
        >= '۰' and <= '۹' => (char)('0' + (character - '۰')), // Extended Arabic-Indic digits
        _ => char.ToLowerInvariant(character),
    };
}
