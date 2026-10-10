using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Domain.Scheduling;

/// <summary>A lesson slot of one shift: the two shifts never share a slot.</summary>
public sealed record ShiftSlot(long ShiftId, int Day, int Lesson);

/// <param name="Slots">Slots the teacher could teach in: the union of their sections' allowed slots, without off
/// days and blocked periods.</param>
/// <param name="ByDayLimit">The slots after the daily limit (each day counts at most max per day).</param>
/// <param name="Available">What the teacher can really teach in a week: slots, bounded by days × max per day and
/// by max per week; zero while fully released.</param>
public sealed record TeacherAvailabilityResult(int Slots, int ByDayLimit, int Available);

/// <summary>
/// The weekly lessons a teacher can teach (Phase 3 §3 check 3). Pure and conservative: it only removes what can
/// never be used (off days, blocked periods, release, the limits), so an available count is never too low for a
/// school that can be timetabled (DECISIONS_PENDING #57: a blocked period applies to that lesson number in every
/// shift).
/// </summary>
public static class TeacherAvailability
{
    public static TeacherAvailabilityResult Compute(
        IEnumerable<ShiftSlot> sectionSlots,
        IReadOnlyCollection<int> offDays,
        IReadOnlyCollection<BlockedPeriod> blocked,
        int? maxPerDay,
        int? maxPerWeek,
        bool released)
    {
        ArgumentNullException.ThrowIfNull(sectionSlots);
        ArgumentNullException.ThrowIfNull(offDays);
        ArgumentNullException.ThrowIfNull(blocked);
        var usable = sectionSlots
            .Distinct()
            .Where(slot => !offDays.Contains(slot.Day) && !blocked.Contains(new BlockedPeriod(slot.Day, slot.Lesson)))
            .ToArray();
        var byDay = usable.GroupBy(slot => slot.Day).Sum(day => Math.Min(day.Count(), maxPerDay ?? int.MaxValue));
        var available = released ? 0 : Math.Min(byDay, maxPerWeek ?? int.MaxValue);
        return new TeacherAvailabilityResult(usable.Length, byDay, available);
    }

    /// <summary>The allowed slots of a section: the first N lessons of each working day, N from its stage and shift.</summary>
    public static IEnumerable<ShiftSlot> SectionSlots(long shiftId, IEnumerable<int> workingDays, Func<int, int> lessonsOn)
    {
        ArgumentNullException.ThrowIfNull(workingDays);
        ArgumentNullException.ThrowIfNull(lessonsOn);
        return workingDays.SelectMany(day => Enumerable.Range(1, Math.Max(0, lessonsOn(day))).Select(lesson => new ShiftSlot(shiftId, day, lesson)));
    }
}
