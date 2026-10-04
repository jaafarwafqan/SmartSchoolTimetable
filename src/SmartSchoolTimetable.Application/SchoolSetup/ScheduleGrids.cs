using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.SchoolSetup;

/// <param name="Days">Working days in display order (ISO numbers).</param>
/// <param name="LessonsPerDay">Most lessons per day of any shift in the current year; 0 until periods exist.</param>
public sealed record ScheduleGridDto(IReadOnlyList<int> Days, int LessonsPerDay);

public sealed record BlockedPeriodDto(int Day, int LessonNumber);

/// <summary>Loads the grid that blocked periods and teacher limits are validated against.</summary>
public static class ScheduleGrids
{
    public static async Task<ScheduleGrid> LoadAsync(IDataStore store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        var week = await store.FirstOrDefaultAsync(store.Query<WorkingWeek>(), cancellationToken) ?? WorkingWeek.CreateDefault();
        var year = await SchoolContextService.CurrentYearAsync(store, cancellationToken);
        var shifts = year is null
            ? []
            : await store.ListAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == year.Id), cancellationToken);
        return new ScheduleGrid(week.Days, shifts.Count == 0 ? 0 : shifts.Max(shift => shift.LessonCount));
    }

    public static ScheduleGridDto ToDto(ScheduleGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return new ScheduleGridDto(grid.WorkingDays, grid.LessonsPerDay);
    }

    public static IReadOnlyList<BlockedPeriod> FromDtos(IReadOnlyList<BlockedPeriodDto>? periods) =>
        (periods ?? []).Select(period => new BlockedPeriod(period.Day, period.LessonNumber)).ToArray();

    public static IReadOnlyList<BlockedPeriodDto> ToDtos(IEnumerable<BlockedPeriod> periods) =>
        periods.Select(period => new BlockedPeriodDto(period.Day, period.LessonNumber)).ToArray();
}
