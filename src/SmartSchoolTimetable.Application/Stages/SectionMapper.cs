using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Stages;

/// <summary>Loads the working week and the referenced shifts once per page (no per-row queries).</summary>
internal sealed class SectionMapper(WorkingWeek? week, IReadOnlyDictionary<long, Shift> shifts)
{
    public static async Task<SectionMapper> LoadAsync(IDataStore store, IReadOnlyCollection<Section> sections, CancellationToken token)
    {
        var week = await store.FirstOrDefaultAsync(store.Query<WorkingWeek>(), token);
        var shiftIds = sections.Select(section => section.ShiftId).Distinct().ToArray();
        var shifts = await store.ListAsync(store.Query<Shift>().Where(shift => shiftIds.Contains(shift.Id)), token);
        return new SectionMapper(week, shifts.ToDictionary(shift => shift.Id));
    }

    public SectionDto ToDto(Section row) => new(
        row.Id,
        row.StageId,
        row.Label,
        row.ShiftId,
        row.StudentCount,
        Section.WeeklyCapacity(week, shifts.GetValueOrDefault(row.ShiftId)),
        row.IsArchived,
        row.ArchivedAt,
        row.Version);
}
