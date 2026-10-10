using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.SchoolSetup;

/// <summary>
/// Year-scoped structure: shifts with their periods and daily sessions, stages and sections. Calendar days are never copied.
/// </summary>
public sealed class YearStructureService(IDataStore store) : IYearStructure
{
    public async Task<bool> HasStructureAsync(long yearId, CancellationToken cancellationToken) =>
        await store.AnyAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == yearId), cancellationToken)
        || await store.AnyAsync(store.Query<Stage>().Where(stage => stage.AcademicYearId == yearId), cancellationToken);

    /// <summary>
    /// Copies the structure into the target year. Copies are saved first so their ids exist, then each section
    /// is linked to the copy of its own stage and shift through a source-id map (names and orders may repeat).
    /// </summary>
    public async Task CopyAsync(long sourceYearId, long targetYearId, CancellationToken cancellationToken)
    {
        var sourceShifts = await store.ListAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == sourceYearId), cancellationToken);
        var sourceStages = await store.ListAsync(store.Query<Stage>().Where(stage => stage.AcademicYearId == sourceYearId), cancellationToken);
        var shiftCopies = sourceShifts.ToDictionary(shift => shift.Id, shift => shift.CopyTo(targetYearId));
        var stageCopies = sourceStages.ToDictionary(stage => stage.Id, stage => stage.CopyTo(targetYearId));
        foreach (var copy in shiftCopies.Values)
            store.Add(copy);
        foreach (var copy in stageCopies.Values)
            store.Add(copy);
        await store.SaveChangesAsync(cancellationToken);

        // R3 daily sessions follow their shift: double shift, the evening timing and breaks, and the mapping of both semesters.
        if (await store.FirstOrDefaultAsync(store.Query<SessionPlan>().Where(plan => plan.AcademicYearId == sourceYearId), cancellationToken) is { } sessions
            && shiftCopies.TryGetValue(sessions.ShiftId, out var sessionShift))
            store.Add(sessions.CopyTo(targetYearId, sessionShift.Id));

        var stageIds = stageCopies.Keys.ToArray();
        var sourceSections = await store.ListAsync(store.Query<Section>().Where(section => stageIds.Contains(section.StageId)), cancellationToken);
        foreach (var section in sourceSections)
            store.Add(section.CopyTo(stageCopies[section.StageId].Id, shiftCopies[section.ShiftId].Id));
        // The curriculum is part of the structure (spec 2.5 §3.3): each stage copy keeps its lines (archived ones stay behind).
        var sourceEntries = await store.ListAsync(store.Query<CurriculumEntry>().Where(entry => stageIds.Contains(entry.StageId) && !entry.IsArchived), cancellationToken);
        foreach (var entry in sourceEntries)
            store.Add(entry.CopyTo(stageCopies[entry.StageId].Id));
    }

    public async Task<string?> DeletionBlockAsync(long yearId, CancellationToken cancellationToken) =>
        await HasStructureAsync(yearId, cancellationToken) ? ErrorCodes.YearStructureInUse : null;
}
