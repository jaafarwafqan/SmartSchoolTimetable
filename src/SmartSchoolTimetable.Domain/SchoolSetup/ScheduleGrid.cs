using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>A blocked slot: an ISO weekday and a lesson number (breaks excluded, DECISIONS_PENDING #4).</summary>
public sealed record BlockedPeriod(int Day, int LessonNumber);

/// <summary>
/// The day × lesson grid that constraints (blocked periods, teacher limits) are checked against: the school's
/// working days, the lessons that exist on each day (the most any shift of the current year teaches that day),
/// and the weekly total of the largest shift (DECISIONS_PENDING #5, #12; ADR 0020).
/// </summary>
public sealed record ScheduleGrid(IReadOnlyList<int> WorkingDays, IReadOnlyDictionary<int, int> LessonsByDay, int MaxWeeklyLessons)
{
    /// <summary>The most lessons of any day (0 until periods exist).</summary>
    public int LessonsPerDay => LessonsByDay.Values.DefaultIfEmpty(0).Max();

    /// <summary>The same number of lessons on every working day.</summary>
    public static ScheduleGrid Uniform(IReadOnlyList<int> workingDays, int lessons)
    {
        ArgumentNullException.ThrowIfNull(workingDays);
        return new ScheduleGrid(workingDays, workingDays.ToDictionary(day => day, _ => lessons), workingDays.Count * lessons);
    }

    /// <summary>The grid of a working week and the shifts of one academic year.</summary>
    public static ScheduleGrid From(IReadOnlyList<int> workingDays, IReadOnlyCollection<Shift> shifts)
    {
        ArgumentNullException.ThrowIfNull(workingDays);
        ArgumentNullException.ThrowIfNull(shifts);
        var byDay = workingDays.ToDictionary(day => day, day => shifts.Select(shift => shift.LessonsOn(day)).DefaultIfEmpty(0).Max());
        var weekly = shifts.Select(shift => shift.WeeklyLessons(workingDays)).DefaultIfEmpty(0).Max();
        return new ScheduleGrid(workingDays, byDay, weekly);
    }

    public int LessonsOn(int day) => LessonsByDay.TryGetValue(day, out var lessons) ? lessons : 0;

    public bool Contains(BlockedPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        return WorkingDays.Contains(period.Day) && period.LessonNumber >= 1 && period.LessonNumber <= LessonsOn(period.Day);
    }

    /// <summary>Adds <see cref="DomainErrorCode.BlockedPeriodInvalid"/> when any slot is outside the grid.</summary>
    public DomainErrors ValidateBlocked(IEnumerable<BlockedPeriod>? periods, string field, DomainErrors errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors.When(periods?.Any(period => period is null || !Contains(period)) == true, field, DomainErrorCode.BlockedPeriodInvalid);
    }
}
