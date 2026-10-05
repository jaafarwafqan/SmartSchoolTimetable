using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;

namespace SmartSchoolTimetable.Application.Curriculum;

/// <summary>
/// The curriculum table (spec 2.5 §3.3): which subjects each stage teaches and how many lessons per week. A
/// subject may appear several times in a stage (ADR 0021). Editing a cell creates, updates or clears an entry.
/// </summary>
public sealed class CurriculumService(IDataStore store, TimeProvider clock)
{
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
                store.Remove(entry);
                AuditTrail.Record(store, clock, "CurriculumEntryDeleted", $"curriculum-entry:{entryId}", "Curriculum cell cleared.");
            }
            else
            {
                if (StoreSaving.TryDomain<CurriculumTableDto>(() => entry.SetWeeklyLessons(command.WeeklyLessons.Value)) is { } invalid)
                    return invalid;
                AuditTrail.Record(store, clock, "CurriculumEntryUpdated", $"curriculum-entry:{entryId}", "Weekly lessons changed.");
            }
        }
        else if (command.WeeklyLessons is { } lessons)
        {
            CurriculumEntry? created = null;
            if (StoreSaving.TryDomain<CurriculumTableDto>(() => created = CurriculumEntry.Create(stage.Id, subject.Id, lessons, command.Label, subject.RequiresDoublePeriod, null)) is { } invalid)
                return invalid;
            store.Add(created!);
            AuditTrail.Record(store, clock, "CurriculumEntryCreated", $"stage:{stage.Id}", "Curriculum entry created.");
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
        AuditTrail.Record(store, clock, "CurriculumEntryUpdated", $"curriculum-entry:{id}", "Curriculum entry updated.");
        return await store.SaveAsync(() => ToDto(entry), "WeeklyLessons", token);
    }

    public async Task<OperationResult<CurriculumEntryDto>> SetArchivedAsync(long id, int version, bool archived, CancellationToken token)
    {
        if (await FindAsync(id, token) is not { } entry)
            return OperationResult.Failure<CurriculumEntryDto>(ErrorCodes.NotFound);
        if (!entry.IsVersion(version))
            return OperationResult.Failure<CurriculumEntryDto>(ErrorCodes.Conflict);
        if (archived)
            entry.Archive(clock.GetUtcNow());
        else
            entry.Restore();
        AuditTrail.Record(store, clock, archived ? "CurriculumEntryArchived" : "CurriculumEntryRestored", $"curriculum-entry:{id}", "Curriculum entry archive state changed.");
        return await store.SaveAsync(() => ToDto(entry), "WeeklyLessons", token);
    }

    public async Task<OperationResult<bool>> DeleteEntryAsync(long id, int version, CancellationToken token)
    {
        if (await FindAsync(id, token) is not { } entry)
            return OperationResult.Failure<bool>(ErrorCodes.NotFound);
        if (!entry.IsVersion(version))
            return OperationResult.Failure<bool>(ErrorCodes.Conflict);
        store.Remove(entry);
        AuditTrail.Record(store, clock, "CurriculumEntryDeleted", $"curriculum-entry:{id}", "Curriculum entry deleted.");
        return await store.SaveAsync(() => true, "WeeklyLessons", token);
    }

    private Task<CurriculumEntry?> FindAsync(long id, CancellationToken token) =>
        store.FirstOrDefaultAsync(store.Query<CurriculumEntry>().Where(entry => entry.Id == id), token);

    internal static CurriculumEntryDto ToDto(CurriculumEntry entry) =>
        new(entry.Id, entry.StageId, entry.SubjectId, entry.WeeklyLessons, entry.Label, entry.NeedsDoublePeriod, entry.Notes, entry.IsArchived, entry.Version, entry.IsSuggested);
}
