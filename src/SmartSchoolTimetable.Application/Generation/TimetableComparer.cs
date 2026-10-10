using SmartSchoolTimetable.Domain.Generation;

namespace SmartSchoolTimetable.Application.Generation;

/// <summary>What happened to one lesson between two versions.</summary>
public static class ChangeKinds
{
    /// <summary>The lesson exists only in the newer version.</summary>
    public const string Added = "added";

    /// <summary>The lesson exists only in the older version.</summary>
    public const string Removed = "removed";

    /// <summary>The same (section, curriculum line) lesson sits in a different day or lesson number.</summary>
    public const string Moved = "moved";

    /// <summary>The same slot, a different teacher.</summary>
    public const string Reassigned = "reassigned";

    public static readonly IReadOnlyList<string> All = [Added, Removed, Moved, Reassigned];
}

/// <summary>One difference. From* describes the older version, To* the newer; the side that does not exist is null.</summary>
public sealed record LessonChange(
    string Kind,
    long SectionId,
    long LineId,
    long SubjectId,
    long? FromTeacherId,
    long? ToTeacherId,
    int? FromDay,
    int? FromLesson,
    int? ToDay,
    int? ToLesson);

public sealed record CompareTotals(int Added, int Removed, int Moved, int Reassigned, int Unchanged)
{
    public int Changed => Added + Removed + Moved + Reassigned;
}

public sealed record SectionChangeSummary(long SectionId, int Added, int Removed, int Moved, int Reassigned);

/// <summary>Per teacher: lessons gained, lost and moved inside the teacher's own week.</summary>
public sealed record TeacherChangeSummary(long TeacherId, int Gained, int Lost, int Moved);

/// <param name="FromVersionId">The older (base) version.</param>
/// <param name="ToVersionId">The newer (compared) version.</param>
public sealed record TimetableComparisonDto(
    long FromVersionId,
    long ToVersionId,
    int FromNumber,
    int ToNumber,
    CompareTotals Totals,
    IReadOnlyList<SectionChangeSummary> Sections,
    IReadOnlyList<TeacherChangeSummary> Teachers,
    IReadOnlyList<LessonChange> Changes);

/// <summary>
/// Compares two saved timetables (M1). Lessons are matched per (section, curriculum line), because a line may be placed
/// several times a week: a slot used by both versions is unchanged (or a teacher change); the remaining slots of each
/// side are paired in day and lesson order as moves; whatever is left over is removed or added. The result is
/// deterministic and the counts add up: unchanged + moved + reassigned + removed equals the older version's lessons.
/// </summary>
public static class TimetableComparer
{
    public static (CompareTotals Totals, IReadOnlyList<LessonChange> Changes) Compare(
        IReadOnlyCollection<TimetableLesson> older,
        IReadOnlyCollection<TimetableLesson> newer,
        IReadOnlyDictionary<long, long> subjectOfLine)
    {
        ArgumentNullException.ThrowIfNull(older);
        ArgumentNullException.ThrowIfNull(newer);
        ArgumentNullException.ThrowIfNull(subjectOfLine);
        var changes = new List<LessonChange>();
        var unchanged = 0;
        var olderGroups = older.GroupBy(lesson => (lesson.SectionId, lesson.CurriculumEntryId)).ToDictionary(group => group.Key, group => group.ToList());
        var newerGroups = newer.GroupBy(lesson => (lesson.SectionId, lesson.CurriculumEntryId)).ToDictionary(group => group.Key, group => group.ToList());
        var keys = olderGroups.Keys.Union(newerGroups.Keys).OrderBy(key => key.SectionId).ThenBy(key => key.CurriculumEntryId);
        foreach (var key in keys)
        {
            var before = olderGroups.GetValueOrDefault(key) ?? [];
            var after = newerGroups.GetValueOrDefault(key) ?? [];
            var subject = subjectOfLine.GetValueOrDefault(key.CurriculumEntryId);

            // 1. The same slot on both sides: unchanged, or the same lesson given to another teacher.
            var leftBefore = new List<TimetableLesson>();
            var leftAfter = after.ToList();
            foreach (var lesson in before.OrderBy(item => item.Day).ThenBy(item => item.LessonNumber))
            {
                var index = leftAfter.FindIndex(item => item.Day == lesson.Day && item.LessonNumber == lesson.LessonNumber);
                if (index < 0)
                {
                    leftBefore.Add(lesson);
                    continue;
                }
                var match = leftAfter[index];
                leftAfter.RemoveAt(index);
                if (match.TeacherId == lesson.TeacherId)
                    unchanged++;
                else
                    changes.Add(new LessonChange(ChangeKinds.Reassigned, key.SectionId, key.CurriculumEntryId, subject, lesson.TeacherId, match.TeacherId,
                        lesson.Day, lesson.LessonNumber, match.Day, match.LessonNumber));
            }

            // 2. What is left on each side is paired in order: a move. The rest is removed or added.
            leftAfter.Sort((x, y) => x.Day != y.Day ? x.Day.CompareTo(y.Day) : x.LessonNumber.CompareTo(y.LessonNumber));
            var paired = Math.Min(leftBefore.Count, leftAfter.Count);
            for (var index = 0; index < paired; index++)
            {
                var from = leftBefore[index];
                var to = leftAfter[index];
                changes.Add(new LessonChange(ChangeKinds.Moved, key.SectionId, key.CurriculumEntryId, subject, from.TeacherId, to.TeacherId,
                    from.Day, from.LessonNumber, to.Day, to.LessonNumber));
            }
            foreach (var from in leftBefore.Skip(paired))
                changes.Add(new LessonChange(ChangeKinds.Removed, key.SectionId, key.CurriculumEntryId, subject, from.TeacherId, null, from.Day, from.LessonNumber, null, null));
            foreach (var to in leftAfter.Skip(paired))
                changes.Add(new LessonChange(ChangeKinds.Added, key.SectionId, key.CurriculumEntryId, subject, null, to.TeacherId, null, null, to.Day, to.LessonNumber));
        }
        var totals = new CompareTotals(
            changes.Count(change => change.Kind == ChangeKinds.Added),
            changes.Count(change => change.Kind == ChangeKinds.Removed),
            changes.Count(change => change.Kind == ChangeKinds.Moved),
            changes.Count(change => change.Kind == ChangeKinds.Reassigned),
            unchanged);
        return (totals, changes);
    }

    public static IReadOnlyList<SectionChangeSummary> BySection(IEnumerable<LessonChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return changes.GroupBy(change => change.SectionId).OrderBy(group => group.Key)
            .Select(group => new SectionChangeSummary(group.Key,
                group.Count(change => change.Kind == ChangeKinds.Added), group.Count(change => change.Kind == ChangeKinds.Removed),
                group.Count(change => change.Kind == ChangeKinds.Moved), group.Count(change => change.Kind == ChangeKinds.Reassigned)))
            .ToArray();
    }

    public static IReadOnlyList<TeacherChangeSummary> ByTeacher(IEnumerable<LessonChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var gained = new Dictionary<long, int>();
        var lost = new Dictionary<long, int>();
        var moved = new Dictionary<long, int>();
        static void Bump(Dictionary<long, int> map, long? teacher)
        {
            if (teacher is { } id)
                map[id] = map.GetValueOrDefault(id) + 1;
        }
        foreach (var change in changes)
        {
            switch (change.Kind)
            {
                case ChangeKinds.Added:
                    Bump(gained, change.ToTeacherId);
                    break;
                case ChangeKinds.Removed:
                    Bump(lost, change.FromTeacherId);
                    break;
                case ChangeKinds.Moved when change.FromTeacherId == change.ToTeacherId:
                    Bump(moved, change.FromTeacherId);
                    break;
                default:
                    Bump(lost, change.FromTeacherId);
                    Bump(gained, change.ToTeacherId);
                    break;
            }
        }
        return gained.Keys.Union(lost.Keys).Union(moved.Keys).Order()
            .Select(id => new TeacherChangeSummary(id, gained.GetValueOrDefault(id), lost.GetValueOrDefault(id), moved.GetValueOrDefault(id))).ToArray();
    }
}
