using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Workload;

namespace SmartSchoolTimetable.Application.Curriculum;

/// <summary>
/// The curriculum table (spec 2.5 §3.3): which subjects each stage teaches and how many lessons per week. A
/// subject may appear several times in a stage (ADR 0021). Editing a cell creates, updates or clears an entry.
/// </summary>
public sealed class CurriculumService(IDataStore store, TimeProvider clock)
{
    private readonly ReferenceGuard references = new(store);

    public Task<CurriculumTableDto> GetTableAsync(long yearId, CancellationToken token) => CurriculumTableBuilder.BuildAsync(store, yearId, token);

    public async Task<OperationResult<CurriculumTableDto>> SetCellAsync(long yearId, SetCurriculumCellCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FirstOrDefaultAsync(store.Query<Stage>().Where(stage => stage.Id == command.StageId && stage.AcademicYearId == yearId), token) is not { } stage)
            return OperationResult.Failure<CurriculumTableDto>(ErrorCodes.NotFound);
        if (stage.IsArchived)
            return OperationResult.Failure<CurriculumTableDto>(ErrorCodes.StageArchived);
        if (await store.FirstOrDefaultAsync(store.Query<Subject>().Where(subject => subject.Id == command.SubjectId && !subject.IsArchived), token) is not { } subject)
            return OperationResult.Invalid<CurriculumTableDto>("SubjectId", ErrorCodes.InvalidOption);

        if (command.EntryId is { } entryId)
        {
            if (await FindAsync(entryId, token) is not { } entry || entry.StageId != stage.Id)
                return OperationResult.Failure<CurriculumTableDto>(ErrorCodes.NotFound);
            if (!entry.IsVersion(command.Version ?? 0))
                return OperationResult.Failure<CurriculumTableDto>(ErrorCodes.Conflict);
            if (command.WeeklyLessons is null)
            {
                // Clearing is a soft delete (Phase 3 §5.4): the line is archived, the response carries it for the undo
                // notice. Assignments of the line are archived with it only after the owner confirmed.
                if (await ArchiveWorkloadAsync(entry, command.ConfirmWorkload, token) is { } inUse)
                    return OperationResult.Failure<CurriculumTableDto>(inUse);
                AuditTrail.Record(store, clock, AuditEvents.CurriculumEntryCleared, $"curriculum-entry:{entryId}", "Curriculum cell cleared (line archived).");
                return await store.SaveAsync(async ct => await GetTableAsync(yearId, ct) with { Cleared = ToDto(entry) }, "WeeklyLessons", token);
            }
            else
            {
                if (StoreSaving.TryDomain<CurriculumTableDto>(() => entry.SetWeeklyLessons(command.WeeklyLessons.Value)) is { } invalid)
                    return invalid;
                AuditTrail.Record(store, clock, AuditEvents.CurriculumEntryUpdated, $"curriculum-entry:{entryId}", "Weekly lessons changed.");
            }
        }
        else if (command.WeeklyLessons is { } lessons)
        {
            CurriculumEntry? created = null;
            if (StoreSaving.TryDomain<CurriculumTableDto>(() => created = CurriculumEntry.Create(stage.Id, subject.Id, lessons, command.Label, subject.RequiresDoublePeriod, null)) is { } invalid)
                return invalid;
            store.Add(created!);
            AuditTrail.Record(store, clock, AuditEvents.CurriculumEntryCreated, $"stage:{stage.Id}", "Curriculum entry created.");
        }
        return await store.SaveAsync(ct => GetTableAsync(yearId, ct), "WeeklyLessons", token);
    }

    public async Task<OperationResult<CurriculumEntryDto>> UpdateEntryAsync(long id, SaveCurriculumEntryCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindAsync(id, token) is not { } entry)
            return OperationResult.Failure<CurriculumEntryDto>(ErrorCodes.NotFound);
        if (!entry.IsVersion(command.Version))
            return OperationResult.Failure<CurriculumEntryDto>(ErrorCodes.Conflict);
        if (StoreSaving.TryDomain<CurriculumEntryDto>(() => entry.Update(command.WeeklyLessons, command.Label, command.NeedsDoublePeriod, command.Notes)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, AuditEvents.CurriculumEntryUpdated, $"curriculum-entry:{id}", "Curriculum entry updated.");
        return await store.SaveAsync(() => ToDto(entry), "WeeklyLessons", token);
    }

    public Task<OperationResult<CurriculumEntryDto>> SetArchivedAsync(long id, int version, bool archived, CancellationToken token) =>
        SetArchivedAsync(id, new ArchiveEntryCommand(version), archived, token);

    public async Task<OperationResult<CurriculumEntryDto>> SetArchivedAsync(long id, ArchiveEntryCommand command, bool archived, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var version = command.Version;
        if (await FindAsync(id, token) is not { } entry)
            return OperationResult.Failure<CurriculumEntryDto>(ErrorCodes.NotFound);
        if (!entry.IsVersion(version))
            return OperationResult.Failure<CurriculumEntryDto>(ErrorCodes.Conflict);
        // Restoring (also the undo of a cleared cell) needs the stage to be active.
        if (!archived && await store.AnyAsync(store.Query<Stage>().Where(stage => stage.Id == entry.StageId && stage.IsArchived), token))
            return OperationResult.Failure<CurriculumEntryDto>(ErrorCodes.StageArchived);
        if (archived && await ArchiveWorkloadAsync(entry, command.ConfirmWorkload, token) is { } inUse)
            return OperationResult.Failure<CurriculumEntryDto>(inUse);
        if (!archived)
            await RestoreWorkloadAsync(entry, token);
        AuditTrail.Record(store, clock, archived ? AuditEvents.CurriculumEntryArchived : AuditEvents.CurriculumEntryRestored, $"curriculum-entry:{id}", "Curriculum entry archive state changed.");
        return await store.SaveAsync(() => ToDto(entry), "WeeklyLessons", token);
    }

    public async Task<OperationResult<bool>> DeleteEntryAsync(long id, int version, CancellationToken token)
    {
        if (await FindAsync(id, token) is not { } entry)
            return OperationResult.Failure<bool>(ErrorCodes.NotFound);
        if (!entry.IsVersion(version))
            return OperationResult.Failure<bool>(ErrorCodes.Conflict);
        if (await references.DeleteBlockedAsync(ReferenceKinds.CurriculumEntry, id, token) is { } inUse)
            return OperationResult.Failure<bool>(inUse);
        store.Remove(entry);
        AuditTrail.Record(store, clock, AuditEvents.CurriculumEntryDeleted, $"curriculum-entry:{id}", "Curriculum entry deleted.");
        return await store.SaveAsync(() => true, "WeeklyLessons", token);
    }

    /// <summary>
    /// Archives the line, and its active assignments in the same moment when the owner confirmed; returns
    /// <c>WORKLOAD_IN_USE</c> when the line has active assignments and the owner did not confirm.
    /// </summary>
    private async Task<string?> ArchiveWorkloadAsync(CurriculumEntry entry, bool confirmed, CancellationToken token)
    {
        var assignments = await store.ListAsync(store.Query<WorkloadAssignment>().Where(row => row.CurriculumEntryId == entry.Id && !row.IsArchived), token);
        if (assignments.Count > 0 && !confirmed)
            return ErrorCodes.WorkloadInUse;
        var now = clock.GetUtcNow();
        entry.Archive(now);
        foreach (var assignment in assignments)
            assignment.Archive(now);
        if (assignments.Count > 0)
            AuditTrail.Record(store, clock, AuditEvents.WorkloadArchivedWithLine, $"curriculum-entry:{entry.Id}", $"{assignments.Count} assignments archived with the line.", new { count = assignments.Count });
        return null;
    }

    /// <summary>Restoring a line brings back the assignments archived together with it, unless the cell was assigned again meanwhile.</summary>
    private async Task RestoreWorkloadAsync(CurriculumEntry entry, CancellationToken token)
    {
        var archivedAt = entry.ArchivedAt;
        entry.Restore();
        if (archivedAt is null)
            return;
        var archived = await store.ListAsync(store.Query<WorkloadAssignment>().Where(row => row.CurriculumEntryId == entry.Id && row.IsArchived && row.ArchivedAt == archivedAt), token);
        var taken = (await store.ListAsync(store.Query<WorkloadAssignment>().Where(row => row.CurriculumEntryId == entry.Id && !row.IsArchived).Select(row => row.SectionId), token)).ToHashSet();
        foreach (var assignment in archived.Where(row => !taken.Contains(row.SectionId)))
            assignment.Restore();
    }

    private Task<CurriculumEntry?> FindAsync(long id, CancellationToken token) =>
        store.FirstOrDefaultAsync(store.Query<CurriculumEntry>().Where(entry => entry.Id == id), token);

    internal static CurriculumEntryDto ToDto(CurriculumEntry entry) =>
        new(entry.Id, entry.StageId, entry.SubjectId, entry.WeeklyLessons, entry.Label, entry.NeedsDoublePeriod, entry.Notes, entry.IsArchived, entry.Version, entry.IsSuggested);
}
