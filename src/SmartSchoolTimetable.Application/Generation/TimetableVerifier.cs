using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Generation;

/// <param name="Rule">The hard constraint id (H1–H10).</param>
public sealed record Violation(string Code, string Rule, long? SectionId = null, long? TeacherId = null, long? SubjectId = null,
    long? ResourceId = null, int? Day = null, int? Lesson = null, int? Count = null, int? Limit = null);

/// <summary>
/// The INDEPENDENT checker of every hard constraint H1–H10 (Phase 4 §1, ADR 0039). It reads only the raw
/// <see cref="SchedulingInput"/> and a list of lessons and shares no code with the solver model, so a modelling
/// mistake in the solver cannot hide here. Every generated or edited timetable passes it before it is saved.
/// </summary>
public static class TimetableVerifier
{
    /// <summary>Lesson length used when a shift has no period details (hand-built inputs; DECISIONS_PENDING #69).</summary>
    public const int EstimatedLessonMinutes = 45;

    private const int DefaultStartMinute = 480;

    public static IReadOnlyList<Violation> Verify(SchedulingInput input, IReadOnlyList<PlacedLesson> lessons, bool doublePeriodsRequired)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(lessons);
        var data = new Data(input);
        var violations = new List<Violation>();
        var known = new List<(PlacedLesson Lesson, AssignmentInput Row, LineInput Line, SectionInput Section, SubjectInput Subject, TeacherInput Teacher)>();
        foreach (var lesson in lessons)
        {
            if (data.Assignment(lesson) is not { } row || !data.Lines.TryGetValue(row.LineId, out var line) || !data.Sections.TryGetValue(row.SectionId, out var section)
                || !data.Subjects.TryGetValue(line.SubjectId, out var subject) || !data.Teachers.TryGetValue(row.TeacherId, out var teacher))
            {
                violations.Add(new Violation(ViolationCodes.UnknownLesson, "H1", lesson.SectionId, lesson.TeacherId, Day: lesson.Day, Lesson: lesson.Lesson));
                continue;
            }
            known.Add((lesson, row, line, section, subject, teacher));
        }

        // H1: every assignment line gets exactly its weekly lessons.
        var placed = known.GroupBy(item => (item.Row.SectionId, item.Row.LineId)).ToDictionary(group => group.Key, group => group.Count());
        foreach (var row in input.Assignments)
        {
            if (!data.Lines.TryGetValue(row.LineId, out var line) || !data.Sections.ContainsKey(row.SectionId))
                continue;
            var count = placed.GetValueOrDefault((row.SectionId, row.LineId));
            if (count != line.WeeklyLessons)
                violations.Add(new Violation(ViolationCodes.WrongLessonCount, "H1", row.SectionId, row.TeacherId, line.SubjectId, Count: count, Limit: line.WeeklyLessons));
        }

        foreach (var item in known)
        {
            var (lesson, _, _, section, subject, teacher) = item;
            // H3: inside the section's day (its stage's lesson count on that working day).
            var allowed = input.WorkingDays.Contains(lesson.Day) ? section.AllowedByDay.FirstOrDefault(day => day.Day == lesson.Day)?.Lessons ?? 0 : 0;
            if (lesson.Lesson < 1 || lesson.Lesson > allowed)
                violations.Add(new Violation(ViolationCodes.OutsideSectionDay, "H3", section.Id, teacher.Id, subject.Id, Day: lesson.Day, Lesson: lesson.Lesson, Limit: allowed));
            // H5: off days, blocked periods, full release.
            if (teacher.Released || teacher.OffDays.Contains(lesson.Day) || teacher.Blocked.Contains(new SlotRef(lesson.Day, lesson.Lesson)))
                violations.Add(new Violation(ViolationCodes.TeacherUnavailable, "H5", section.Id, teacher.Id, subject.Id, Day: lesson.Day, Lesson: lesson.Lesson));
            // H6: the subject's blocked periods.
            if (subject.Blocked.Contains(new SlotRef(lesson.Day, lesson.Lesson)))
                violations.Add(new Violation(ViolationCodes.SubjectBlocked, "H6", section.Id, teacher.Id, subject.Id, Day: lesson.Day, Lesson: lesson.Lesson));
        }

        foreach (var group in known.GroupBy(item => (item.Section.Id, item.Lesson.Day)))
        {
            // H2: one lesson per section slot.
            foreach (var clash in group.GroupBy(item => item.Lesson.Lesson).Where(slot => slot.Count() > 1))
                violations.Add(new Violation(ViolationCodes.SectionConflict, "H2", group.Key.Item1, Day: group.Key.Day, Lesson: clash.Key, Count: clash.Count()));
            // H3: no free period between lessons: the used lesson numbers are exactly 1..k.
            var used = group.Select(item => item.Lesson.Lesson).Distinct().Order().ToArray();
            for (var index = 0; index < used.Length; index++)
            {
                if (used[index] == index + 1)
                    continue;
                violations.Add(new Violation(ViolationCodes.SectionGap, "H3", group.Key.Item1, Day: group.Key.Day, Lesson: index + 1));
                break;
            }
        }

        foreach (var group in known.GroupBy(item => item.Teacher.Id))
        {
            var teacher = group.First().Teacher;
            // H4: never two lessons at overlapping real times (across shifts too).
            foreach (var day in group.GroupBy(item => item.Lesson.Day))
            {
                var items = day.ToArray();
                for (var first = 0; first < items.Length; first++)
                {
                    for (var second = first + 1; second < items.Length; second++)
                    {
                        if (data.Overlap(items[first].Section.ShiftId, items[first].Lesson.Lesson, items[second].Section.ShiftId, items[second].Lesson.Lesson))
                            violations.Add(new Violation(ViolationCodes.TeacherConflict, "H4", items[second].Section.Id, teacher.Id, Day: day.Key, Lesson: items[second].Lesson.Lesson));
                    }
                }
                // H7: maximum per day.
                if (teacher.MaxPerDay is { } maxDay && items.Length > maxDay)
                    violations.Add(new Violation(ViolationCodes.TeacherDayLimit, "H7", TeacherId: teacher.Id, Day: day.Key, Count: items.Length, Limit: maxDay));
            }
            // H7: maximum per week.
            if (teacher.MaxPerWeek is { } maxWeek && group.Count() > maxWeek)
                violations.Add(new Violation(ViolationCodes.TeacherWeekLimit, "H7", TeacherId: teacher.Id, Count: group.Count(), Limit: maxWeek));
        }

        // H8: at the start of every lesson, the lessons that need a resource and are running then stay within its capacity.
        foreach (var resource in input.Resources)
        {
            var users = known.Where(item => item.Subject.RequiredResourceId == resource.Id).ToArray();
            foreach (var day in users.GroupBy(item => item.Lesson.Day))
            {
                var items = day.ToArray();
                foreach (var probe in items.DistinctBy(item => (item.Section.ShiftId, item.Lesson.Lesson)))
                {
                    var start = data.Times(probe.Section.ShiftId, probe.Lesson.Lesson).Start;
                    var running = items.Count(item => data.Times(item.Section.ShiftId, item.Lesson.Lesson) is var (s, e) && s <= start && start < e);
                    if (running > resource.Capacity)
                        violations.Add(new Violation(ViolationCodes.ResourceCapacity, "H8", probe.Section.Id, ResourceId: resource.Id, Day: day.Key, Lesson: probe.Lesson.Lesson,
                            Count: running, Limit: resource.Capacity));
                }
            }
        }

        // H9: per (section, subject) and day at most max(2, ceil(weekly lessons ÷ the section's working days)).
        foreach (var group in known.GroupBy(item => (item.Section.Id, item.Subject.Id)))
        {
            var section = group.First().Section;
            var weekly = input.Lines.Where(line => line.StageId == section.StageId && line.SubjectId == group.Key.Item2).Sum(line => line.WeeklyLessons);
            var days = section.AllowedByDay.Count(day => day.Lessons > 0 && input.WorkingDays.Contains(day.Day));
            var cap = Math.Max(2, days == 0 ? weekly : (weekly + days - 1) / days);
            foreach (var day in group.GroupBy(item => item.Lesson.Day).Where(day => day.Count() > cap))
                violations.Add(new Violation(ViolationCodes.SubjectDailyCap, "H9", section.Id, SubjectId: group.Key.Item2, Day: day.Key, Count: day.Count(), Limit: cap));
        }

        // H10 («دروس مزدوجة»): each double line holds ⌊lessons ÷ 2⌋ disjoint pairs of adjacent lessons on one day.
        if (doublePeriodsRequired)
        {
            foreach (var group in known.Where(item => item.Line.NeedsDoublePeriod || item.Subject.RequiresDoublePeriod).GroupBy(item => (item.Section.Id, item.Line.Id)))
            {
                var line = group.First().Line;
                var pairs = group.GroupBy(item => item.Lesson.Day).Sum(day => data.Pairs(group.First().Section.ShiftId, day.Select(item => item.Lesson.Lesson)));
                if (pairs < line.WeeklyLessons / 2)
                    violations.Add(new Violation(ViolationCodes.DoublePeriodBroken, "H10", group.Key.Item1, SubjectId: line.SubjectId, Count: pairs, Limit: line.WeeklyLessons / 2));
            }
        }
        return violations;
    }

    private sealed class Data(SchedulingInput input)
    {
        private readonly Dictionary<(long Section, long Line), AssignmentInput> _assignments =
            input.Assignments.GroupBy(row => (row.SectionId, row.LineId)).ToDictionary(group => group.Key, group => group.First());

        public Dictionary<long, LineInput> Lines { get; } = input.Lines.ToDictionary(line => line.Id);
        public Dictionary<long, SectionInput> Sections { get; } = input.Sections.ToDictionary(section => section.Id);
        public Dictionary<long, SubjectInput> Subjects { get; } = input.Subjects.ToDictionary(subject => subject.Id);
        public Dictionary<long, TeacherInput> Teachers { get; } = input.Teachers.ToDictionary(teacher => teacher.Id);
        private Dictionary<long, ShiftInput> Shifts { get; } = input.Shifts.ToDictionary(shift => shift.Id);

        public AssignmentInput? Assignment(PlacedLesson lesson) =>
            _assignments.TryGetValue((lesson.SectionId, lesson.LineId), out var row) && row.TeacherId == lesson.TeacherId ? row : null;

        private PeriodInput[] LessonRows(long shiftId) =>
            Shifts.TryGetValue(shiftId, out var shift) && shift.Periods is { Count: > 0 } periods
                ? periods.Where(period => period.Kind == nameof(PeriodKind.Lesson)).OrderBy(period => period.Position).ToArray()
                : [];

        /// <summary>Real clock time of lesson n: its period row, or an estimate from the shift start when there are no rows.</summary>
        public (int Start, int End) Times(long shiftId, int lesson)
        {
            var rows = LessonRows(shiftId);
            if (lesson >= 1 && lesson <= rows.Length)
                return (rows[lesson - 1].StartMinute, rows[lesson - 1].EndMinute);
            var start = (Shifts.TryGetValue(shiftId, out var shift) ? shift.StartMinute : null) ?? DefaultStartMinute;
            return (start + (lesson - 1) * EstimatedLessonMinutes, start + lesson * EstimatedLessonMinutes);
        }

        public bool Overlap(long shiftA, int lessonA, long shiftB, int lessonB)
        {
            if (shiftA == shiftB)
                return lessonA == lessonB;
            var (startA, endA) = Times(shiftA, lessonA);
            var (startB, endB) = Times(shiftB, lessonB);
            return startA < endB && startB < endA;
        }

        /// <summary>Lessons n and n+1 form a double only when no break row lies between them.</summary>
        private bool Adjacent(long shiftId, int lesson)
        {
            var rows = LessonRows(shiftId);
            return lesson + 1 > rows.Length || rows[lesson].Position == rows[lesson - 1].Position + 1;
        }

        public int Pairs(long shiftId, IEnumerable<int> lessons)
        {
            var sorted = lessons.Distinct().Order().ToArray();
            var pairs = 0;
            for (var index = 0; index + 1 < sorted.Length; index++)
            {
                if (sorted[index + 1] != sorted[index] + 1 || !Adjacent(shiftId, sorted[index]))
                    continue;
                pairs++;
                index++;
            }
            return pairs;
        }
    }
}
