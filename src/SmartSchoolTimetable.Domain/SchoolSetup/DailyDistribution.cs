namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>Why no daily distribution can be suggested for a stage.</summary>
public enum DistributionProblem { None, NoCurriculum, AboveShiftCapacity, BelowWorkingDays }

public sealed record DistributionSuggestion(IReadOnlyList<DayLessons> Days, DistributionProblem Problem)
{
    public bool Possible => Problem == DistributionProblem.None;
}

/// <summary>
/// "اقتراح توزيع الحصص اليومية" (ADR 0030): spreads a stage's weekly curriculum total over the working days as evenly as
/// possible; the extra lessons go to the EARLIER days of the week, so shorter days fall at its end
/// (5 days: 28 → 6,6,6,5,5; 31 → 7,6,6,6,6). A day never exceeds what the shift teaches that day; lessons that do not fit
/// move to the earliest days with room. Every working day keeps at least one lesson.
/// </summary>
public static class DailyDistribution
{
    /// <param name="orderedDays">Working days from the week start (Sunday first for Iraqi schools).</param>
    public static DistributionSuggestion Suggest(int weeklyTotal, IReadOnlyList<int> orderedDays, Func<int, int> maxOnDay)
    {
        ArgumentNullException.ThrowIfNull(orderedDays);
        ArgumentNullException.ThrowIfNull(maxOnDay);
        if (weeklyTotal <= 0 || orderedDays.Count == 0)
            return new([], DistributionProblem.NoCurriculum);
        if (weeklyTotal > orderedDays.Sum(maxOnDay))
            return new([], DistributionProblem.AboveShiftCapacity);
        if (weeklyTotal < orderedDays.Count)
            return new([], DistributionProblem.BelowWorkingDays);

        var values = orderedDays.Select((_, index) => weeklyTotal / orderedDays.Count + (index < weeklyTotal % orderedDays.Count ? 1 : 0)).ToArray();
        var overflow = 0;
        for (var index = 0; index < values.Length; index++)
        {
            var max = maxOnDay(orderedDays[index]);
            if (values[index] <= max) continue;
            overflow += values[index] - max;
            values[index] = max;
        }
        while (overflow > 0)
        {
            for (var index = 0; index < values.Length && overflow > 0; index++)
            {
                if (values[index] >= maxOnDay(orderedDays[index])) continue;
                values[index]++;
                overflow--;
            }
        }
        return new(orderedDays.Select((day, index) => new DayLessons(day, values[index])).ToArray(), DistributionProblem.None);
    }

    /// <summary>Working days in week order, starting at <paramref name="weekStart"/> (ISO 1–7).</summary>
    public static IReadOnlyList<int> InWeekOrder(IEnumerable<int> workingDays, int weekStart) =>
        workingDays.OrderBy(day => (day - weekStart + 7) % 7).ToArray();
}
