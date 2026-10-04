namespace SmartSchoolTimetable.Domain.Curriculum;

public enum CapacityStatus { Under, Equal, Over }

/// <summary>Capacity of one shift group of a stage: its sections on that shift share the same weekly capacity.</summary>
public sealed record ShiftCapacity(long ShiftId, string ShiftName, int Sections, int WeeklyCapacity);

/// <param name="Difference">Capacity minus planned lessons: positive = lessons missing, negative = too many.</param>
public sealed record ShiftTotal(long ShiftId, string ShiftName, int Sections, int WeeklyCapacity, CapacityStatus Status, int Difference);

/// <summary>
/// Live totals of a stage's curriculum against the weekly capacity of its sections (spec 2.5 §3.3): one result
/// per shift the stage's active sections use (dual-shift stages get two). Equal is the target; under means
/// lessons are missing, over means the plan does not fit.
/// </summary>
public static class CurriculumTotals
{
    public static IReadOnlyList<ShiftTotal> For(int plannedLessons, IEnumerable<ShiftCapacity> capacities)
    {
        ArgumentNullException.ThrowIfNull(capacities);
        return capacities
            .Select(capacity => new ShiftTotal(
                capacity.ShiftId,
                capacity.ShiftName,
                capacity.Sections,
                capacity.WeeklyCapacity,
                plannedLessons == capacity.WeeklyCapacity ? CapacityStatus.Equal
                    : plannedLessons < capacity.WeeklyCapacity ? CapacityStatus.Under : CapacityStatus.Over,
                capacity.WeeklyCapacity - plannedLessons))
            .ToArray();
    }
}
