using SmartSchoolTimetable.Application.Scheduling;

namespace SmartSchoolTimetable.Application.Generation;

/// <summary>
/// One difference between the school data a version was made from and today's. Only the fields that apply are set:
/// <paramref name="TeacherId"/> / <paramref name="ToTeacherId"/> (the teacher before and after a change of assignment),
/// <paramref name="From"/> / <paramref name="To"/> (numbers, or 0/1 for a flag), <paramref name="Days"/> (the current off days).
/// </summary>
public sealed record InputChange(string Code, long? TeacherId = null, long? ToTeacherId = null, long? SectionId = null, long? SubjectId = null,
    long? StageId = null, long? ShiftId = null, long? ResourceId = null, int? From = null, int? To = null, IReadOnlyList<int>? Days = null);

/// <summary>MF11: the difference between two scheduling inputs, in the owner's terms (assignments, availability, loads, curriculum hours, timing).</summary>
public static class InputDiff
{
    public static IReadOnlyList<InputChange> Compare(SchedulingInput before, SchedulingInput now)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(now);
        var changes = new List<InputChange>();

        // Assignments by (section, line).
        var beforeLines = before.Lines.ToDictionary(line => line.Id);
        var nowLines = now.Lines.ToDictionary(line => line.Id);
        long SubjectOf(long lineId) => nowLines.TryGetValue(lineId, out var line) ? line.SubjectId : beforeLines.TryGetValue(lineId, out var old) ? old.SubjectId : 0;
        var oldAssignments = before.Assignments.GroupBy(row => (row.SectionId, row.LineId)).ToDictionary(group => group.Key, group => group.First());
        var newAssignments = now.Assignments.GroupBy(row => (row.SectionId, row.LineId)).ToDictionary(group => group.Key, group => group.First());
        foreach (var (key, row) in newAssignments.OrderBy(item => item.Key))
        {
            if (!oldAssignments.TryGetValue(key, out var old))
                changes.Add(new InputChange(InputChangeCodes.AssignmentAdded, TeacherId: row.TeacherId, SectionId: key.SectionId, SubjectId: SubjectOf(key.LineId)));
            else if (old.TeacherId != row.TeacherId)
                changes.Add(new InputChange(InputChangeCodes.AssignmentTeacherChanged, TeacherId: old.TeacherId, ToTeacherId: row.TeacherId, SectionId: key.SectionId, SubjectId: SubjectOf(key.LineId)));
        }
        foreach (var (key, row) in oldAssignments.OrderBy(item => item.Key).Where(item => !newAssignments.ContainsKey(item.Key)))
            changes.Add(new InputChange(InputChangeCodes.AssignmentRemoved, TeacherId: row.TeacherId, SectionId: key.SectionId, SubjectId: SubjectOf(key.LineId)));

        // Teachers.
        var oldTeachers = before.Teachers.ToDictionary(teacher => teacher.Id);
        foreach (var teacher in now.Teachers.OrderBy(item => item.Id))
        {
            if (!oldTeachers.TryGetValue(teacher.Id, out var old))
                continue; // a new teacher changes nothing in a saved timetable until assigned (that shows as an assignment change)
            if (!old.OffDays.Order().SequenceEqual(teacher.OffDays.Order()))
                changes.Add(new InputChange(InputChangeCodes.TeacherOffDaysChanged, TeacherId: teacher.Id, Days: teacher.OffDays.Order().ToArray()));
            if (!old.Blocked.OrderBy(slot => slot.Day).ThenBy(slot => slot.Lesson).SequenceEqual(teacher.Blocked.OrderBy(slot => slot.Day).ThenBy(slot => slot.Lesson)))
                changes.Add(new InputChange(InputChangeCodes.TeacherBlockedChanged, TeacherId: teacher.Id, From: old.Blocked.Count, To: teacher.Blocked.Count));
            if (old.Released != teacher.Released || old.PartiallyReleased != teacher.PartiallyReleased)
                changes.Add(new InputChange(InputChangeCodes.TeacherReleasedChanged, TeacherId: teacher.Id, To: teacher.Released ? 1 : 0));
            if (old.IsArchived != teacher.IsArchived)
                changes.Add(new InputChange(InputChangeCodes.TeacherArchivedChanged, TeacherId: teacher.Id, To: teacher.IsArchived ? 1 : 0));
            if (!old.SpecializationIds.Order().SequenceEqual(teacher.SpecializationIds.Order()))
                changes.Add(new InputChange(InputChangeCodes.TeacherSpecializationsChanged, TeacherId: teacher.Id));
            if (old.MaxPerDay != teacher.MaxPerDay)
                changes.Add(new InputChange(InputChangeCodes.TeacherDayLimitChanged, TeacherId: teacher.Id, From: old.MaxPerDay, To: teacher.MaxPerDay));
            if (old.MaxPerWeek != teacher.MaxPerWeek)
                changes.Add(new InputChange(InputChangeCodes.TeacherWeekLimitChanged, TeacherId: teacher.Id, From: old.MaxPerWeek, To: teacher.MaxPerWeek));
        }

        // Curriculum lines (hours per week).
        foreach (var line in now.Lines.OrderBy(item => item.Id))
        {
            if (!beforeLines.TryGetValue(line.Id, out var old))
                changes.Add(new InputChange(InputChangeCodes.LineAdded, SubjectId: line.SubjectId, StageId: line.StageId, To: line.WeeklyLessons));
            else
            {
                if (old.WeeklyLessons != line.WeeklyLessons)
                    changes.Add(new InputChange(InputChangeCodes.LessonsPerWeekChanged, SubjectId: line.SubjectId, StageId: line.StageId, From: old.WeeklyLessons, To: line.WeeklyLessons));
                if (old.NeedsDoublePeriod != line.NeedsDoublePeriod)
                    changes.Add(new InputChange(InputChangeCodes.LineDoubleChanged, SubjectId: line.SubjectId, StageId: line.StageId, To: line.NeedsDoublePeriod ? 1 : 0));
            }
        }
        foreach (var line in before.Lines.Where(item => !nowLines.ContainsKey(item.Id)).OrderBy(item => item.Id))
            changes.Add(new InputChange(InputChangeCodes.LineRemoved, SubjectId: line.SubjectId, StageId: line.StageId, From: line.WeeklyLessons));

        // Timing, sections, days.
        if (!before.WorkingDays.Order().SequenceEqual(now.WorkingDays.Order()))
            changes.Add(new InputChange(InputChangeCodes.WorkingDaysChanged, Days: now.WorkingDays.Order().ToArray()));
        var oldShifts = before.Shifts.ToDictionary(shift => shift.Id);
        foreach (var shift in now.Shifts.OrderBy(item => item.Id))
        {
            if (oldShifts.TryGetValue(shift.Id, out var old) && !SameShiftTiming(old, shift))
                changes.Add(new InputChange(InputChangeCodes.ShiftTimingChanged, ShiftId: shift.Id));
        }
        var oldSections = before.Sections.ToDictionary(section => section.Id);
        foreach (var section in now.Sections.OrderBy(item => item.Id))
        {
            if (!oldSections.TryGetValue(section.Id, out var old))
                changes.Add(new InputChange(InputChangeCodes.SectionAdded, SectionId: section.Id));
            else if (old.StageId != section.StageId || old.ShiftId != section.ShiftId || !SameDays(old.AllowedByDay, section.AllowedByDay))
                changes.Add(new InputChange(InputChangeCodes.SectionDaysChanged, SectionId: section.Id));
        }
        foreach (var section in before.Sections.Where(item => now.Sections.All(other => other.Id != item.Id)).OrderBy(item => item.Id))
            changes.Add(new InputChange(InputChangeCodes.SectionRemoved, SectionId: section.Id));

        // Subjects and resources.
        var oldSubjects = before.Subjects.ToDictionary(subject => subject.Id);
        foreach (var subject in now.Subjects.OrderBy(item => item.Id))
        {
            if (oldSubjects.TryGetValue(subject.Id, out var old) && !SameSubject(old, subject))
                changes.Add(new InputChange(InputChangeCodes.SubjectRulesChanged, SubjectId: subject.Id));
        }
        var oldResources = before.Resources.ToDictionary(resource => resource.Id);
        foreach (var resource in now.Resources.OrderBy(item => item.Id))
        {
            if (oldResources.TryGetValue(resource.Id, out var old) && (old.Capacity != resource.Capacity || old.IsArchived != resource.IsArchived || old.Kind != resource.Kind))
                changes.Add(new InputChange(InputChangeCodes.ResourceChanged, ResourceId: resource.Id, From: old.Capacity, To: resource.Capacity));
        }

        if (before.Profile.ProfileVersion != now.Profile.ProfileVersion || !before.Profile.Rules.OrderBy(rule => rule.Key, StringComparer.Ordinal).SequenceEqual(now.Profile.Rules.OrderBy(rule => rule.Key, StringComparer.Ordinal)))
            changes.Add(new InputChange(InputChangeCodes.PrioritiesChanged));
        return changes;
    }

    private static bool SameDays(IReadOnlyList<DayLessons> first, IReadOnlyList<DayLessons> second) =>
        first.OrderBy(day => day.Day).SequenceEqual(second.OrderBy(day => day.Day));

    private static bool SameShiftTiming(ShiftInput first, ShiftInput second) =>
        SameDays(first.LessonsByDay, second.LessonsByDay) && first.StartMinute == second.StartMinute && first.EndMinute == second.EndMinute
        && (first.Periods ?? []).SequenceEqual(second.Periods ?? []) && (first.SessionBreaksAfter ?? []).Order().SequenceEqual((second.SessionBreaksAfter ?? []).Order());

    private static bool SameSubject(SubjectInput first, SubjectInput second) =>
        first.Priority == second.Priority && first.DistributionEnabled == second.DistributionEnabled && first.SpreadAcrossDays == second.SpreadAcrossDays
        && first.Heavy == second.Heavy && first.RequiresDoublePeriod == second.RequiresDoublePeriod && first.RequiredResourceId == second.RequiredResourceId
        && first.Blocked.OrderBy(slot => slot.Day).ThenBy(slot => slot.Lesson).SequenceEqual(second.Blocked.OrderBy(slot => slot.Day).ThenBy(slot => slot.Lesson));
}
