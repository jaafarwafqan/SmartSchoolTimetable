using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Stages;

/// <summary>Loads the working week, shifts and stages once per page (no per-row queries); capacity uses the stage's own counts.</summary>
internal sealed class SectionMapper(WorkingWeek? week, IReadOnlyDictionary<long, Shift> shifts, IReadOnlyDictionary<long, Stage> stages)
{
    public static async Task<SectionMapper> LoadAsync(IDataStore store, IReadOnlyCollection<Section> sections, CancellationToken token)
    {
        var week = await store.FirstOrDefaultAsync(store.Read<WorkingWeek>(), token);
        var shiftIds = sections.Select(section => section.ShiftId).Distinct().ToArray();
        var shifts = await store.ListAsync(store.Read<Shift>().Where(shift => shiftIds.Contains(shift.Id)), token);
        var stageIds = sections.Select(section => section.StageId).Distinct().ToArray();
        var stages = await store.ListAsync(store.Read<Stage>().Where(stage => stageIds.Contains(stage.Id)), token);
        return new SectionMapper(week, shifts.ToDictionary(shift => shift.Id), stages.ToDictionary(stage => stage.Id));
    }

    public SectionDto ToDto(Section row) => new(
        row.Id,
        row.StageId,
        row.Label,
        row.ShiftId,
        row.StudentCount,
        Section.WeeklyCapacity(week, shifts.GetValueOrDefault(row.ShiftId), stages.GetValueOrDefault(row.StageId)),
        row.IsArchived,
        row.ArchivedAt,
        row.Version);
}
