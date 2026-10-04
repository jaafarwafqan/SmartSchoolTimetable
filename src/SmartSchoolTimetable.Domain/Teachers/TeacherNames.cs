using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.Teachers;

/// <summary>Short-name proposals for bulk add (DECISIONS_PENDING #13).</summary>
public static class TeacherNames
{
    /// <summary>
    /// The first two words of the full name; if that is taken, the first three; then the full name. Returns null
    /// when every candidate is taken or too long (the owner then adds that teacher individually).
    /// </summary>
    public static string? ProposeShortName(string fullName, Func<string, bool> isTaken)
    {
        ArgumentNullException.ThrowIfNull(isTaken);
        var words = ArabicText.Clean(fullName).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return null;
        var candidates = new[] { 2, 3, words.Length }
            .Select(count => string.Join(' ', words.Take(Math.Min(count, words.Length))))
            .Distinct(StringComparer.Ordinal);
        return candidates.FirstOrDefault(candidate =>
            candidate.Length <= Teacher.ShortNameMaxLength && !isTaken(ArabicText.Normalize(candidate)));
    }
}
