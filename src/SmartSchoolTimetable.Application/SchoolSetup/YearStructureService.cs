using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.SchoolSetup;

/// <summary>
/// Year-scoped timetable structure and school stages/sections.
/// </summary>
public sealed class YearStructureService(IDataStore store) : IYearStructure
{
    public async Task<bool> HasStructureAsync(long yearId, CancellationToken cancellationToken) =>
        await store.AnyAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == yearId), cancellationToken)
        || await store.AnyAsync(store.Query<Stage>().Where(stage => stage.AcademicYearId == yearId), cancellationToken);

    public async Task CopyAsync(long sourceYearId, long targetYearId, CancellationToken cancellationToken)
    {
        var sourceShifts = await store.ListAsync(
            store.Query<Shift>().Where(shift => shift.AcademicYearId == sourceYearId), cancellationToken);
        foreach (var shift in sourceShifts)
            store.Add(shift.CopyTo(targetYearId));
        var sourceStages = await store.ListAsync(store.Query<Stage>().Where(stage => stage.AcademicYearId == sourceYearId), cancellationToken);
        foreach (var stage in sourceStages) store.Add(stage.CopyTo(targetYearId));
        await store.SaveChangesAsync(cancellationToken);

        var targetShifts = await store.ListAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == targetYearId), cancellationToken);
        var targetStages = await store.ListAsync(store.Query<Stage>().Where(stage => stage.AcademicYearId == targetYearId), cancellationToken);
        var sourceSections = await store.ListAsync(store.Query<Section>().Where(section => sourceStages.Select(stage => stage.Id).Contains(section.StageId)), cancellationToken);
        foreach (var section in sourceSections)
        {
            var sourceStage = sourceStages.Single(stage => stage.Id == section.StageId);
            var sourceShift = sourceShifts.Single(shift => shift.Id == section.ShiftId);
            var targetStage = targetStages.Single(stage => stage.NormalizedName == sourceStage.NormalizedName);
            var targetShift = targetShifts.Single(shift => shift.DisplayOrder == sourceShift.DisplayOrder);
            store.Add(section.CopyTo(targetStage.Id, targetShift.Id));
        }
    }

    public async Task<string?> DeletionBlockAsync(long yearId, CancellationToken cancellationToken)
    {
        var hasStages = await store.AnyAsync(store.Query<Stage>().Where(stage => stage.AcademicYearId == yearId), cancellationToken);
        return await HasStructureAsync(yearId, cancellationToken) || hasStages ? ErrorCodes.YearStructureInUse : null;
    }
}
