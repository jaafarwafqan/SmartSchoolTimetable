namespace SmartSchoolTimetable.Application.Scheduling;

/// <summary>
/// «عدالة الحصص الأخيرة»: the share of the day's last periods each teacher may take, in proportion to his weekly load. The last period of a shift on a
/// day is the highest lesson number any of its sections has that day. A last period on an evening day (double shift) counts <see cref="EveningWeight"/>
/// times, a morning one once. The solver and the scorer read this one definition.
/// </summary>
public static class LastPeriodFairness
{
    public const int EveningWeight = 2;

    public static int WeightOf(SchedulingInput input, int day) => input.IsEvening(day) ? EveningWeight : 1;

    /// <summary>The last lesson number of every (shift, day): the most lessons any section with a curriculum has there.</summary>
    public static IReadOnlyDictionary<(long Shift, int Day), int> LastLessons(SchedulingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var staged = input.Lines.Select(line => line.StageId).ToHashSet();
        var last = new Dictionary<(long, int), int>();
        foreach (var section in input.Sections.Where(section => staged.Contains(section.StageId)))
        {
            foreach (var day in section.AllowedByDay.Where(day => day.Lessons > 0 && input.WorkingDays.Contains(day.Day)))
                last[(section.ShiftId, day.Day)] = Math.Max(last.GetValueOrDefault((section.ShiftId, day.Day)), day.Lessons);
        }
        return last;
    }

    /// <summary>
    /// What each teacher may have of the weighted last periods without penalty: the school's total of them shared out by weekly load, rounded up.
    /// Empty when no teacher holds a lesson.
    /// </summary>
    public static IReadOnlyDictionary<long, int> Allowances(SchedulingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var last = LastLessons(input);
        long total = 0;
        foreach (var section in input.Sections.Where(section => input.Lines.Any(line => line.StageId == section.StageId)))
        {
            foreach (var day in section.AllowedByDay.Where(day => day.Lessons > 0 && input.WorkingDays.Contains(day.Day)))
            {
                if (last.GetValueOrDefault((section.ShiftId, day.Day)) == day.Lessons)
                    total += WeightOf(input, day.Day);
            }
        }
        var loads = TeacherDays.Of(input).ToDictionary(item => item.Key, item => (long)item.Value.Load);
        var all = loads.Values.Sum();
        return all == 0 ? new Dictionary<long, int>() : loads.ToDictionary(item => item.Key, item => (int)((total * item.Value + all - 1) / all));
    }
}
