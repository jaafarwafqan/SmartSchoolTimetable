namespace SmartSchoolTimetable.Application.Scheduling;

/// <summary>
/// How the lessons of one subject in one section should spread over the week («توزيع حصص المادة على أيام الأسبوع», S1).
/// </summary>
/// <param name="Weekly">The subject's weekly lessons in the section.</param>
/// <param name="Base">Lessons a day that are never a repeat: 1, or 2 for a double subject (a double lesson is one sitting).</param>
/// <param name="Days">Working days on which the subject can be taught in the section: the section has lessons, the subject is not blocked all day, and its teacher (one of them) is there.</param>
/// <param name="SectionDays">Working days on which the section has lessons at all.</param>
/// <param name="Share">The fair share of one day, ⌈weekly ÷ days⌉ (never below <paramref name="Base"/>): a day above it is a heavy pile-up.</param>
/// <param name="TeacherIds">The teachers of the subject in the section.</param>
public sealed record SpreadShare(long SectionId, long SubjectId, int Weekly, int Base, int Days, int SectionDays, int Share, IReadOnlyList<long> TeacherIds)
{
    /// <summary>Repeats no timetable can avoid: lessons above <see cref="Base"/> a day even when spread perfectly over <see cref="Days"/>.</summary>
    public int Unavoidable => Math.Max(0, Weekly - Base * Days);

    /// <summary>
    /// The S1 penalty of one day with <paramref name="count"/> lessons of the subject: one for every lesson above <see cref="Base"/> (a same-day repeat)
    /// and one more for every lesson above <see cref="Share"/> (a pile-up beyond the fair share). The solver minimises exactly this sum.
    /// </summary>
    public int Penalty(int count) => Math.Max(0, count - Base) + Math.Max(0, count - Share);

    /// <summary>Why the unavoidable repeats exist: the teacher is missing on some of the section's days, or the subject has more lessons than days.</summary>
    public string Reason => Days < SectionDays ? SpreadReasons.TeacherAbsent : SpreadReasons.MoreLessonsThanDays;
}

/// <summary>Stable reason codes of an unavoidable repeat (the frontend words them).</summary>
public static class SpreadReasons
{
    public const string TeacherAbsent = "teacherAbsent";
    public const string MoreLessonsThanDays = "moreLessonsThanDays";
}

/// <summary>The spread share of every (section, subject) whose subject spreads across days (the default; the subject's switch turns it off).</summary>
public static class SubjectSpread
{
    public static IReadOnlyDictionary<(long SectionId, long SubjectId), SpreadShare> Of(SchedulingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var subjects = input.Subjects.ToDictionary(subject => subject.Id);
        var lines = input.Lines.ToDictionary(line => line.Id);
        var sections = input.Sections.ToDictionary(section => section.Id);
        var teachers = input.Teachers.ToDictionary(teacher => teacher.Id);
        bool LineDouble(LineInput line) => line.NeedsDoublePeriod || subjects.TryGetValue(line.SubjectId, out var subject) && subject.RequiresDoublePeriod;
        var result = new Dictionary<(long, long), SpreadShare>();
        var rows = input.Assignments.Where(row => lines.ContainsKey(row.LineId) && sections.ContainsKey(row.SectionId)
            && subjects.TryGetValue(lines[row.LineId].SubjectId, out var subject) && subject.SpreadAcrossDays);
        foreach (var group in rows.GroupBy(row => (row.SectionId, lines[row.LineId].SubjectId)))
        {
            var section = sections[group.Key.SectionId];
            var subject = subjects[group.Key.SubjectId];
            var weekly = group.Sum(row => lines[row.LineId].WeeklyLessons);
            if (weekly == 0)
                continue;
            var doubled = input.Lines.Any(line => line.StageId == section.StageId && line.SubjectId == subject.Id && LineDouble(line));
            var baseCount = doubled ? 2 : 1;
            var teacherIds = group.Select(row => row.TeacherId).Distinct().Order().ToArray();
            var sectionDays = 0;
            var days = 0;
            foreach (var day in input.WorkingDays)
            {
                var allowed = section.AllowedByDay.FirstOrDefault(item => item.Day == day)?.Lessons ?? 0;
                if (allowed == 0)
                    continue;
                sectionDays++;
                var open = Enumerable.Range(1, allowed).Where(lesson => !subject.Blocked.Contains(new SlotRef(day, lesson))).ToArray();
                if (teacherIds.Any(id => teachers.TryGetValue(id, out var teacher) && !teacher.Released && !teacher.OffDays.Contains(day)
                        && open.Any(lesson => !teacher.Blocked.Contains(new SlotRef(day, lesson)))))
                    days++;
            }
            if (days == 0)
                continue;
            var share = Math.Max(baseCount, (weekly + days - 1) / days);
            result[group.Key] = new SpreadShare(section.Id, subject.Id, weekly, baseCount, days, sectionDays, share, teacherIds);
        }
        return result;
    }
}
