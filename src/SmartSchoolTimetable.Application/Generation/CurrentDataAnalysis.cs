using SmartSchoolTimetable.Application.Scheduling;

namespace SmartSchoolTimetable.Application.Generation;

/// <summary>
/// One problem of a saved timetable against today's data. <paramref name="TeacherId"/> is the teacher in the saved lesson;
/// <paramref name="CurrentTeacherId"/> is who teaches the line now (only for <see cref="CurrentFindingCodes.TeacherReassigned"/>).
/// Names are looked up by the screen; the API sends codes and numbers only.
/// </summary>
public sealed record CurrentFinding(string Code, long? SectionId, long? TeacherId, long? CurrentTeacherId, long? SubjectId, long? ResourceId,
    int? Day, int? Lesson, int? Count, int? Limit);

/// <param name="Findings">Every hard problem against the current data (empty: the timetable still fits today's data).</param>
/// <param name="Candidate">The saved lessons as they would be under today's assignments (reassigned lessons carry the new teacher; removed ones are gone).</param>
/// <param name="Locked">Candidate lessons that are not part of any problem: a minimal repair keeps them exactly where they are.</param>
/// <param name="LockedDay">Smaller: the other lessons of the same section and day as a free lesson are free too (the day can close up: a section's lessons of a day must start at the first lesson with no gap).</param>
/// <param name="LockedWide">Smallest: everything of the affected sections and teachers is free; the last try when the others have no solution.</param>
public sealed record CurrentDataAnalysis(IReadOnlyList<CurrentFinding> Findings, IReadOnlyList<PlacedLesson> Candidate,
    IReadOnlyList<PlacedLesson> Locked, IReadOnlyList<PlacedLesson> LockedDay, IReadOnlyList<PlacedLesson> LockedWide)
{
    public bool HasConflicts => Findings.Count > 0;

    public int Reassigned => Findings.Count(finding => finding.Code == CurrentFindingCodes.TeacherReassigned);

    /// <summary>True when the only problem is teacher changes, and putting the new teachers into the same slots breaks nothing: «استبدال المعلم» can be done directly.</summary>
    public bool OnlyReassignments => Findings.Count > 0 && Findings.All(finding => finding.Code == CurrentFindingCodes.TeacherReassigned);
}


/// <summary>
/// MF11: checks a SAVED timetable against the CURRENT school data with the independent <see cref="TimetableVerifier"/>
/// (the saved version's own check uses the snapshot it was made from, so it cannot see later changes). Saved versions are
/// never changed by this; the result only describes them and tells a repair which lessons must move.
/// </summary>
public static class CurrentDataAnalyzer
{
    public static CurrentDataAnalysis Analyze(SchedulingInput current, IReadOnlyList<PlacedLesson> stored, bool doublePeriodsRequired)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(stored);
        var lines = current.Lines.ToDictionary(line => line.Id);
        var teacherOf = new Dictionary<(long Section, long Line), long>();
        foreach (var row in current.Assignments)
            teacherOf.TryAdd((row.SectionId, row.LineId), row.TeacherId);
        var sections = current.Sections.Select(section => section.Id).ToHashSet();

        var findings = new List<CurrentFinding>();
        var candidate = new List<PlacedLesson>();
        foreach (var lesson in stored)
        {
            var subjectId = lines.TryGetValue(lesson.LineId, out var line) ? line.SubjectId : (long?)null;
            if (!sections.Contains(lesson.SectionId) || line is null || !teacherOf.TryGetValue((lesson.SectionId, lesson.LineId), out var teacherId))
            {
                findings.Add(new CurrentFinding(CurrentFindingCodes.AssignmentRemoved, lesson.SectionId, lesson.TeacherId, null, subjectId, null, lesson.Day, lesson.Lesson, null, null));
                continue;
            }
            if (teacherId != lesson.TeacherId)
            {
                findings.Add(new CurrentFinding(CurrentFindingCodes.TeacherReassigned, lesson.SectionId, lesson.TeacherId, teacherId, subjectId, null, lesson.Day, lesson.Lesson, null, null));
                candidate.Add(lesson with { TeacherId = teacherId });
                continue;
            }
            candidate.Add(lesson);
        }

        var violations = TimetableVerifier.Verify(current, candidate, doublePeriodsRequired)
            .Where(violation => violation.Code != ViolationCodes.UnknownLesson).ToArray();
        findings.AddRange(violations.Select(violation => new CurrentFinding(violation.Code, violation.SectionId, violation.TeacherId, null, violation.SubjectId, violation.ResourceId,
            violation.Day, violation.Lesson, violation.Count, violation.Limit)));

        var free = FreeLessons(current, lines, candidate, violations);
        var locked = candidate.Where(lesson => !free.Contains(lesson)).ToArray();
        var freeDays = free.Select(lesson => (lesson.SectionId, lesson.Day)).ToHashSet();
        var lockedDay = locked.Where(lesson => !freeDays.Contains((lesson.SectionId, lesson.Day))).ToArray();
        // A reassignment that clashes with something is already free through its violation; the others keep their slot with the new teacher.
        var affectedSections = violations.Select(violation => violation.SectionId).Where(id => id is not null).Select(id => id!.Value).ToHashSet();
        var affectedTeachers = violations.Select(violation => violation.TeacherId).Where(id => id is not null).Select(id => id!.Value).ToHashSet();
        var wide = candidate.Where(lesson => !free.Contains(lesson) && !affectedSections.Contains(lesson.SectionId) && !affectedTeachers.Contains(lesson.TeacherId)).ToArray();
        return new CurrentDataAnalysis(findings, candidate, locked, lockedDay, wide);
    }

    /// <summary>The lessons a minimal repair must be free to move, per violation (the least that can resolve it).</summary>
    private static HashSet<PlacedLesson> FreeLessons(SchedulingInput input, Dictionary<long, LineInput> lines, IReadOnlyList<PlacedLesson> lessons, IReadOnlyList<Violation> violations)
    {
        var subjects = input.Subjects.ToDictionary(subject => subject.Id);
        long? SubjectOf(PlacedLesson lesson) => lines.TryGetValue(lesson.LineId, out var line) ? line.SubjectId : null;
        var free = new HashSet<PlacedLesson>();
        void Add(Func<PlacedLesson, bool> where)
        {
            foreach (var lesson in lessons.Where(where))
                free.Add(lesson);
        }

        foreach (var violation in violations)
        {
            switch (violation.Code)
            {
                case ViolationCodes.TeacherUnavailable or ViolationCodes.SubjectBlocked or ViolationCodes.OutsideSectionDay or ViolationCodes.TeacherConflict:
                    Add(lesson => lesson.SectionId == violation.SectionId && lesson.Day == violation.Day && lesson.Lesson == violation.Lesson
                        && (violation.TeacherId is null || lesson.TeacherId == violation.TeacherId));
                    break;
                case ViolationCodes.SectionConflict:
                    Add(lesson => lesson.SectionId == violation.SectionId && lesson.Day == violation.Day && lesson.Lesson == violation.Lesson);
                    break;
                case ViolationCodes.SectionGap:
                    Add(lesson => lesson.SectionId == violation.SectionId && lesson.Day == violation.Day);
                    break;
                case ViolationCodes.TeacherDayLimit:
                    Add(lesson => lesson.TeacherId == violation.TeacherId && lesson.Day == violation.Day);
                    break;
                case ViolationCodes.TeacherWeekLimit:
                    Add(lesson => lesson.TeacherId == violation.TeacherId);
                    break;
                case ViolationCodes.ResourceCapacity:
                    Add(lesson => lesson.Day == violation.Day && SubjectOf(lesson) is { } subjectId && subjects.TryGetValue(subjectId, out var subject) && subject.RequiredResourceId == violation.ResourceId);
                    break;
                case ViolationCodes.SubjectDailyCap:
                    Add(lesson => lesson.SectionId == violation.SectionId && SubjectOf(lesson) == violation.SubjectId && lesson.Day == violation.Day);
                    break;
                case ViolationCodes.DoublePeriodBroken:
                    Add(lesson => lesson.SectionId == violation.SectionId && SubjectOf(lesson) == violation.SubjectId);
                    break;
                case ViolationCodes.WrongLessonCount when violation.Count > violation.Limit:
                    // Too many lessons for a line whose hours were cut: every lesson of it is free, the solver keeps only what is needed.
                    Add(lesson => lesson.SectionId == violation.SectionId && SubjectOf(lesson) == violation.SubjectId);
                    break;
            }
        }
        return free;
    }
}
