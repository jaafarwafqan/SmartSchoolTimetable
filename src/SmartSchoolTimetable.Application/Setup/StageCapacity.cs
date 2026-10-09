using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Setup;

/// <param name="Days">The working days in week order.</param>
/// <param name="MaxOnDay">The most lessons the stage's shift(s) allow on a day (all shifts must fit in a dual stage).</param>
internal sealed record StageCapacityInfo(IReadOnlyList<int> Days, Func<int, int> MaxOnDay)
{
    public int Weekly => Days.Sum(MaxOnDay);
}

/// <summary>How many lessons a stage can hold: its sections' shifts over the working days (ADR 0030). A stage with no
/// section yet is measured against the largest shift; a stage taught in two shifts must fit the smaller one.</summary>
internal static class StageCapacity
{
    public static async Task<IReadOnlyDictionary<long, StageCapacityInfo>> LoadAsync(IDataStore store, long yearId, IReadOnlyList<Stage> stages, CancellationToken token)
    {
        var week = await store.FirstOrDefaultAsync(store.Query<WorkingWeek>(), token);
        IReadOnlyList<int> days = week is null ? [] : DailyDistribution.InWeekOrder(week.Days, week.WeekStartDay);
        var stageIds = stages.Select(stage => stage.Id).ToArray();
        var sections = await store.ListAsync(store.Query<Section>().Where(section => stageIds.Contains(section.StageId) && !section.IsArchived), token);
        var shifts = await store.ListAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == yearId), token);
        var result = new Dictionary<long, StageCapacityInfo>();
        foreach (var stage in stages)
        {
            var used = sections.Where(section => section.StageId == stage.Id).Select(section => section.ShiftId).Distinct().ToHashSet();
            var relevant = shifts.Where(shift => used.Count == 0 || used.Contains(shift.Id)).ToArray();
            // A stage taught in two shifts must fit both: the smaller shift bounds each day.
            int MaxOnDay(int day) => relevant.Length == 0 ? 0 : used.Count == 0 ? relevant.Max(shift => shift.LessonsOn(day)) : relevant.Min(shift => shift.LessonsOn(day));
            result[stage.Id] = new StageCapacityInfo(days, MaxOnDay);
        }
        return result;
    }
}
