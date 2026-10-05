using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Subjects;

/// <summary>
/// Subjects: unique normalized names, palette colours, priority, flags and blocked periods checked against the
/// current schedule grid. Soft archive; hard delete is allowed because nothing references subjects before Phase 3.
/// </summary>
public sealed class SubjectsService(IDataStore store, TimeProvider clock)
{
    private readonly ReferenceGuard references = new(store);

    public async Task<PagedResult<SubjectDto>> ListAsync(ListQuery query, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(query);
        var rows = store.Read<Subject>();
        if (!query.WithArchived)
            rows = rows.Where(row => !row.IsArchived);
        var search = query.NormalizedSearch;
        if (search.Length > 0)
            rows = rows.Where(row => row.NormalizedName.Contains(search));
        rows = query.SortKey("name") switch
        {
            ("name", true) => rows.OrderByDescending(row => row.NormalizedName),
            ("priority", false) => rows.OrderBy(row => row.Priority).ThenBy(row => row.NormalizedName),
            ("priority", true) => rows.OrderByDescending(row => row.Priority).ThenBy(row => row.NormalizedName),
            _ => rows.OrderBy(row => row.NormalizedName),
        };
        return await store.ToPageAsync(rows, query, ToDto, token);
    }

    public async Task<OperationResult<SubjectDto>> CreateAsync(SaveSubjectCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Quick add sends only the name: colour 0 means "next free palette colour", priority 0 the default 3.
        if (command.ColorIndex == 0 || command.Priority == 0)
            command = command with
            {
                ColorIndex = command.ColorIndex == 0 ? await NextColorAsync(token) : command.ColorIndex,
                Priority = command.Priority == 0 ? Subject.DefaultPriority : command.Priority,
            };
        var grid = await ScheduleGrids.LoadAsync(store, token);
        Subject? subject = null;
        if (StoreSaving.TryDomain<SubjectDto>(() => subject = Subject.Create(ToDetails(command), grid)) is { } invalid)
            return invalid;
        if (await NameTakenAsync(null, command.Name, token))
            return OperationResult.Invalid<SubjectDto>(nameof(command.Name), ErrorCodes.DuplicateName);
        store.Add(subject!);
        AuditTrail.Record(store, clock, "SubjectCreated", "subject", "Subject created.");
        return await store.SaveAsync(() => ToDto(subject!), nameof(command.Name), token);
    }

    public async Task<OperationResult<SubjectDto>> UpdateAsync(long id, SaveSubjectCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindAsync(id, token) is not { } subject)
            return OperationResult.Failure<SubjectDto>(ErrorCodes.NotFound);
        if (!subject.IsVersion(command.Version))
            return OperationResult.Failure<SubjectDto>(ErrorCodes.Conflict);
        if (await NameTakenAsync(id, command.Name, token))
            return OperationResult.Invalid<SubjectDto>(nameof(command.Name), ErrorCodes.DuplicateName);
        var grid = await ScheduleGrids.LoadAsync(store, token);
        if (StoreSaving.TryDomain<SubjectDto>(() => subject.Update(ToDetails(command), grid)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, "SubjectUpdated", $"subject:{id}", "Subject updated.");
        return await store.SaveAsync(() => ToDto(subject), nameof(command.Name), token);
    }

    public async Task<OperationResult<SubjectDto>> SetArchivedAsync(long id, int version, bool archived, CancellationToken token)
    {
        if (await FindAsync(id, token) is not { } subject)
            return OperationResult.Failure<SubjectDto>(ErrorCodes.NotFound);
        if (!subject.IsVersion(version))
            return OperationResult.Failure<SubjectDto>(ErrorCodes.Conflict);
        if (archived && await references.ArchiveBlockedAsync(ReferenceKinds.Subject, id, token) is { } inUse)
            return OperationResult.Failure<SubjectDto>(inUse);
        if (archived)
            subject.Archive(clock.GetUtcNow());
        else
            subject.Restore();
        AuditTrail.Record(store, clock, archived ? "SubjectArchived" : "SubjectRestored", $"subject:{id}", archived ? "Subject archived." : "Subject restored.");
        return await store.SaveAsync(() => ToDto(subject), "Name", token);
    }

    public async Task<OperationResult<bool>> DeleteAsync(long id, int version, CancellationToken token)
    {
        if (await FindAsync(id, token) is not { } subject)
            return OperationResult.Failure<bool>(ErrorCodes.NotFound);
        if (!subject.IsVersion(version))
            return OperationResult.Failure<bool>(ErrorCodes.Conflict);
        if (await references.DeleteBlockedAsync(ReferenceKinds.Subject, id, token) is { } inUse)
            return OperationResult.Failure<bool>(inUse);
        store.Remove(subject);
        AuditTrail.Record(store, clock, "SubjectDeleted", $"subject:{id}", "Subject deleted.");
        return await store.SaveAsync(() => true, "Name", token);
    }

    private static SubjectDetails ToDetails(SaveSubjectCommand command) => new(
        command.Name,
        command.ColorIndex,
        command.Priority,
        command.DistributionEnabled,
        command.SpreadAcrossDays,
        command.Heavy,
        command.RequiresDoublePeriod,
        command.Notes,
        ScheduleGrids.FromDtos(command.BlockedPeriods));

    /// <summary>The first palette colour no active subject uses; when all ten are used, colours cycle.</summary>
    private async Task<int> NextColorAsync(CancellationToken token)
    {
        var used = await store.ListAsync(store.Query<Subject>().Where(row => !row.IsArchived).Select(row => row.ColorIndex), token);
        var free = Enumerable.Range(1, Subject.ColorCount).FirstOrDefault(color => !used.Contains(color));
        return free != 0 ? free : used.Count % Subject.ColorCount + 1;
    }

    private Task<Subject?> FindAsync(long id, CancellationToken token) =>
        store.FirstOrDefaultAsync(store.Query<Subject>().Where(row => row.Id == id), token);

    private async Task<bool> NameTakenAsync(long? exceptId, string? name, CancellationToken token)
    {
        var normalized = ArabicText.Normalize(name);
        return normalized.Length > 0 && await store.AnyAsync(
            store.Query<Subject>().Where(row => row.Id != exceptId && row.NormalizedName == normalized), token);
    }

    private static SubjectDto ToDto(Subject row) => new(
        row.Id,
        row.Name,
        row.ColorIndex,
        row.Priority,
        row.DistributionEnabled,
        row.SpreadAcrossDays,
        row.Heavy,
        row.RequiresDoublePeriod,
        ScheduleGrids.ToDtos(row.BlockedPeriods),
        row.Notes,
        row.IsArchived,
        row.ArchivedAt,
        row.Version);
}
