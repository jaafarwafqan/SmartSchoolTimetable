using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>A blocked slot: an ISO weekday and a lesson number (breaks excluded, DECISIONS_PENDING #4).</summary>
public sealed record BlockedPeriod(int Day, int LessonNumber);

/// <summary>
/// The day × lesson grid that constraints (subject and teacher blocked periods, limits) are checked against:
/// the school's working days and the most lessons per day of any shift in the current year
/// (DECISIONS_PENDING #5 and #12). With no lessons defined yet, no slot exists.
/// </summary>
public sealed record ScheduleGrid(IReadOnlyList<int> WorkingDays, int LessonsPerDay)
{
    public int WeeklyCapacity => WorkingDays.Count * LessonsPerDay;

    public bool Contains(BlockedPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        return WorkingDays.Contains(period.Day) && period.LessonNumber >= 1 && period.LessonNumber <= LessonsPerDay;
    }

    /// <summary>Adds <see cref="DomainErrorCode.BlockedPeriodInvalid"/> when any slot is outside the grid.</summary>
    public DomainErrors ValidateBlocked(IEnumerable<BlockedPeriod>? periods, string field, DomainErrors errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors.When(periods?.Any(period => period is null || !Contains(period)) == true, field, DomainErrorCode.BlockedPeriodInvalid);
    }
}
