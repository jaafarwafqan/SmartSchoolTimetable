using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Teachers;

/// <summary>
/// Bulk add from pasted names (spec 2.10): a preview classifies every non-blank line, and creation re-runs the same
/// classification so only "ready" lines are saved, in one transaction.
/// </summary>
public static class TeacherBulkAdd
{
    public const int MaxLines = 200;
    public const string Ready = "ready";

    public static async Task<IReadOnlyList<BulkPreviewLineDto>> ClassifyAsync(IDataStore store, IReadOnlyList<string> names, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(names);
        var existing = await store.ListAsync(store.Query<Teacher>().Select(teacher => new { teacher.NormalizedFullName, teacher.NormalizedShortName }), token);
        var fullNames = existing.Select(teacher => teacher.NormalizedFullName).ToHashSet(StringComparer.Ordinal);
        var shortNames = existing.Select(teacher => teacher.NormalizedShortName).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = new List<BulkPreviewLineDto>();
        for (var index = 0; index < names.Count; index++)
        {
            var fullName = ArabicText.Clean(names[index]);
            if (fullName.Length == 0)
                continue;
            var normalized = ArabicText.Normalize(fullName);
            string? shortName = null;
            var status = fullName.Length > Teacher.FullNameMaxLength ? "tooLong"
                : !seen.Add(normalized) ? "duplicateInList"
                : fullNames.Contains(normalized) ? "exists"
                : (shortName = TeacherNames.ProposeShortName(fullName, shortNames.Contains)) is null ? "noShortName"
                : Ready;
            if (status == Ready)
                shortNames.Add(ArabicText.Normalize(shortName));
            lines.Add(new BulkPreviewLineDto(index + 1, fullName, status == Ready ? shortName : null, status));
        }
        return lines;
    }
}
