using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Stages;

/// <summary>
/// Year-scoped stages and their sections. Rules: names unique per year (stages) or per stage (sections) after
/// Arabic normalization; soft archive; a stage with active sections cannot be archived; hard delete only when
/// nothing references the record; every edit carries the version that was read.
/// </summary>
public sealed class StagesSectionsService(IDataStore store, TimeProvider clock)
{
    private readonly ReferenceGuard references = new(store);

    public async Task<PagedResult<StageDto>> ListStagesAsync(long yearId, ListQuery query, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(query);
        var rows = store.Read<Stage>().Where(row => row.AcademicYearId == yearId);
        if (!query.WithArchived)
            rows = rows.Where(row => !row.IsArchived);
        var search = query.NormalizedSearch;
        if (search.Length > 0)
            rows = rows.Where(row => row.NormalizedName.Contains(search));
        rows = query.SortKey("order") switch
        {
            ("name", true) => rows.OrderByDescending(row => row.NormalizedName),
            ("name", false) => rows.OrderBy(row => row.NormalizedName),
            ("order", true) => rows.OrderByDescending(row => row.DisplayOrder).ThenBy(row => row.NormalizedName),
            _ => rows.OrderBy(row => row.DisplayOrder).ThenBy(row => row.NormalizedName),
        };
        return await store.ToPageAsync(rows, query, ToDto, token);
    }

    public async Task<PagedResult<SectionDto>> ListSectionsAsync(long yearId, long stageId, ListQuery query, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (await FindStageAsync(yearId, stageId, token) is null)
            return new PagedResult<SectionDto>([], 0, query.SafePage, query.SafePageSize);
        var rows = store.Read<Section>().Where(row => row.StageId == stageId);
        if (!query.WithArchived)
            rows = rows.Where(row => !row.IsArchived);
        var search = query.NormalizedSearch;
        if (search.Length > 0)
            rows = rows.Where(row => row.NormalizedLabel.Contains(search));
        rows = query.SortKey("label") switch
        {
            ("label", true) => rows.OrderByDescending(row => row.NormalizedLabel),
            _ => rows.OrderBy(row => row.NormalizedLabel),
        };
        var page = await store.ToPageAsync(rows, query, row => row, token);
        var mapper = await SectionMapper.LoadAsync(store, page.Items, token);
        return new PagedResult<SectionDto>(page.Items.Select(mapper.ToDto).ToArray(), page.Total, page.Page, page.PageSize);
    }

    public async Task<OperationResult<StageDto>> CreateStageAsync(long yearId, SaveStageCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!await store.AnyAsync(store.Query<AcademicYear>().Where(row => row.Id == yearId), token))
            return OperationResult.Failure<StageDto>(ErrorCodes.NotFound);
        Stage? stage = null;
        if (StoreSaving.TryDomain<StageDto>(() => stage = Stage.Create(yearId, command.Name, command.DisplayOrder, command.TemplateKey)) is { } invalid)
            return invalid;
        if (await StageNameTakenAsync(yearId, null, command.Name, token))
            return OperationResult.Invalid<StageDto>(nameof(command.Name), ErrorCodes.DuplicateName);
        store.Add(stage!);
        AuditTrail.Record(store, clock, "StageCreated", $"academic-year:{yearId}", "Stage created.");
        return await store.SaveAsync(() => ToDto(stage!), nameof(command.Name), token);
    }

    public async Task<OperationResult<StageDto>> UpdateStageAsync(long yearId, long id, SaveStageCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindStageAsync(yearId, id, token) is not { } stage)
            return OperationResult.Failure<StageDto>(ErrorCodes.NotFound);
        if (!stage.IsVersion(command.Version))
            return OperationResult.Failure<StageDto>(ErrorCodes.Conflict);
        if (await StageNameTakenAsync(yearId, id, command.Name, token))
            return OperationResult.Invalid<StageDto>(nameof(command.Name), ErrorCodes.DuplicateName);
        if (StoreSaving.TryDomain<StageDto>(() => stage.Update(command.Name, command.DisplayOrder)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, "StageUpdated", $"stage:{id}", "Stage updated.");
        return await store.SaveAsync(() => ToDto(stage), nameof(command.Name), token);
    }

    public async Task<OperationResult<StageDto>> SetStageArchivedAsync(long yearId, long id, ArchiveCommand command, bool archived, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindStageAsync(yearId, id, token) is not { } stage)
            return OperationResult.Failure<StageDto>(ErrorCodes.NotFound);
        if (!stage.IsVersion(command.Version))
            return OperationResult.Failure<StageDto>(ErrorCodes.Conflict);
        if (archived && await references.ArchiveBlockedAsync(ReferenceKinds.Stage, id, token) is { } inUse)
            return OperationResult.Failure<StageDto>(inUse);
        if (archived)
            stage.Archive(clock.GetUtcNow());
        else
            stage.Restore();
        AuditTrail.Record(store, clock, archived ? "StageArchived" : "StageRestored", $"stage:{id}", archived ? "Stage archived." : "Stage restored.");
        return await store.SaveAsync(() => ToDto(stage), "Name", token);
    }

    /// <summary>Hard delete, allowed only while nothing refers to the stage (archived sections and lines included).</summary>
    public async Task<OperationResult<bool>> DeleteStageAsync(long yearId, long id, int version, CancellationToken token)
    {
        if (await FindStageAsync(yearId, id, token) is not { } stage)
            return OperationResult.Failure<bool>(ErrorCodes.NotFound);
        if (!stage.IsVersion(version))
            return OperationResult.Failure<bool>(ErrorCodes.Conflict);
        if (await references.DeleteBlockedAsync(ReferenceKinds.Stage, id, token) is { } inUse)
            return OperationResult.Failure<bool>(inUse);
        store.Remove(stage);
        AuditTrail.Record(store, clock, "StageDeleted", $"stage:{id}", "Stage deleted.");
        return await store.SaveAsync(() => true, "Name", token);
    }

    public async Task<OperationResult<SectionDto>> CreateSectionAsync(long yearId, long stageId, SaveSectionCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindStageAsync(yearId, stageId, token) is not { } stage)
            return OperationResult.Failure<SectionDto>(ErrorCodes.NotFound);
        if (stage.IsArchived)
            return OperationResult.Failure<SectionDto>(ErrorCodes.StageArchived);
        Section? section = null;
        if (StoreSaving.TryDomain<SectionDto>(() => section = Section.Create(stageId, command.ShiftId, command.Label, command.StudentCount)) is { } invalid)
            return invalid;
        if (await CheckSectionReferencesAsync(yearId, stageId, null, command, token) is { } rejected)
            return rejected;
        store.Add(section!);
        AuditTrail.Record(store, clock, "SectionCreated", $"stage:{stageId}", "Section created.");
        return await store.SaveAsync(ct => MapSectionAsync(section!, ct), nameof(command.Label), token);
    }

    public async Task<OperationResult<SectionDto>> UpdateSectionAsync(long yearId, long stageId, long id, SaveSectionCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindSectionAsync(yearId, stageId, id, token) is not { } section)
            return OperationResult.Failure<SectionDto>(ErrorCodes.NotFound);
        if (!section.IsVersion(command.Version))
            return OperationResult.Failure<SectionDto>(ErrorCodes.Conflict);
        if (await CheckSectionReferencesAsync(yearId, stageId, id, command, token) is { } rejected)
            return rejected;
        if (StoreSaving.TryDomain<SectionDto>(() => section.Update(command.ShiftId, command.Label, command.StudentCount)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, "SectionUpdated", $"section:{id}", "Section updated.");
        return await store.SaveAsync(ct => MapSectionAsync(section, ct), nameof(command.Label), token);
    }

    public async Task<OperationResult<SectionDto>> SetSectionArchivedAsync(long yearId, long stageId, long id, ArchiveCommand command, bool archived, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindStageAsync(yearId, stageId, token) is not { } stage
            || await FindSectionInStageAsync(stageId, id, token) is not { } section)
            return OperationResult.Failure<SectionDto>(ErrorCodes.NotFound);
        if (!section.IsVersion(command.Version))
            return OperationResult.Failure<SectionDto>(ErrorCodes.Conflict);
        if (!archived && stage.IsArchived)
            return OperationResult.Failure<SectionDto>(ErrorCodes.StageArchived);
        if (archived && await references.ArchiveBlockedAsync(ReferenceKinds.Section, id, token) is { } inUse)
            return OperationResult.Failure<SectionDto>(inUse);
        if (archived)
            section.Archive(clock.GetUtcNow());
        else
            section.Restore();
        AuditTrail.Record(store, clock, archived ? "SectionArchived" : "SectionRestored", $"section:{id}", archived ? "Section archived." : "Section restored.");
        return await store.SaveAsync(ct => MapSectionAsync(section, ct), "Label", token);
    }

    /// <summary>Hard delete, allowed only while nothing refers to the section (reference guard).</summary>
    public async Task<OperationResult<bool>> DeleteSectionAsync(long yearId, long stageId, long id, int version, CancellationToken token)
    {
        if (await FindSectionAsync(yearId, stageId, id, token) is not { } section)
            return OperationResult.Failure<bool>(ErrorCodes.NotFound);
        if (!section.IsVersion(version))
            return OperationResult.Failure<bool>(ErrorCodes.Conflict);
        if (await references.DeleteBlockedAsync(ReferenceKinds.Section, id, token) is { } inUse)
            return OperationResult.Failure<bool>(inUse);
        store.Remove(section);
        AuditTrail.Record(store, clock, "SectionDeleted", $"section:{id}", "Section deleted.");
        return await store.SaveAsync(() => true, "Label", token);
    }

    private async Task<OperationResult<SectionDto>?> CheckSectionReferencesAsync(long yearId, long stageId, long? exceptId, SaveSectionCommand command, CancellationToken token)
    {
        if (!await store.AnyAsync(store.Query<Shift>().Where(row => row.Id == command.ShiftId && row.AcademicYearId == yearId), token))
            return OperationResult.Invalid<SectionDto>(nameof(command.ShiftId), ErrorCodes.ShiftNotInYear);
        var normalized = ArabicText.Normalize(command.Label);
        var taken = await store.AnyAsync(
            store.Query<Section>().Where(row => row.StageId == stageId && row.Id != exceptId && row.NormalizedLabel == normalized),
            token);
        return taken ? OperationResult.Invalid<SectionDto>(nameof(command.Label), ErrorCodes.DuplicateName) : null;
    }

    private async Task<SectionDto> MapSectionAsync(Section section, CancellationToken token) =>
        (await SectionMapper.LoadAsync(store, [section], token)).ToDto(section);

    private async Task<bool> StageNameTakenAsync(long yearId, long? exceptId, string? name, CancellationToken token)
    {
        var normalized = ArabicText.Normalize(name);
        return normalized.Length > 0 && await store.AnyAsync(
            store.Query<Stage>().Where(row => row.AcademicYearId == yearId && row.Id != exceptId && row.NormalizedName == normalized),
            token);
    }

    private Task<Stage?> FindStageAsync(long yearId, long id, CancellationToken token) =>
        store.FirstOrDefaultAsync(store.Query<Stage>().Where(row => row.Id == id && row.AcademicYearId == yearId), token);

    private Task<Section?> FindSectionInStageAsync(long stageId, long id, CancellationToken token) =>
        store.FirstOrDefaultAsync(store.Query<Section>().Where(row => row.Id == id && row.StageId == stageId), token);

    private async Task<Section?> FindSectionAsync(long yearId, long stageId, long id, CancellationToken token) =>
        await FindStageAsync(yearId, stageId, token) is null ? null : await FindSectionInStageAsync(stageId, id, token);

    internal static StageDto ToDto(Stage row) =>
        new(row.Id, row.AcademicYearId, row.Name, row.DisplayOrder, row.TemplateKey,
            row.DayLessonCounts.Select(entry => new DayLessonsDto(entry.Day, entry.Lessons)).ToArray(), row.IsArchived, row.ArchivedAt, row.Version);

    /// <summary>
    /// The stage's own lessons per working day (ADR 0027). Each count is at most what the stage's shifts teach that
    /// day (the shifts of its active sections; all shifts of the year while it has none).
    /// </summary>
    public async Task<OperationResult<StageDto>> SetStageDayLessonsAsync(long yearId, long id, SetStageDayLessonsCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FirstOrDefaultAsync(store.Query<Stage>().Where(stage => stage.Id == id && stage.AcademicYearId == yearId), token) is not { } stage)
            return OperationResult.Failure<StageDto>(ErrorCodes.NotFound);
        if (!stage.IsVersion(command.Version))
            return OperationResult.Failure<StageDto>(ErrorCodes.Conflict);
        if (stage.IsArchived)
            return OperationResult.Failure<StageDto>(ErrorCodes.StageArchived);
        var days = (await store.FirstOrDefaultAsync(store.Query<WorkingWeek>(), token))?.Days ?? [];
        var shiftIds = await store.ListAsync(store.Query<Section>().Where(section => section.StageId == id && !section.IsArchived).Select(section => section.ShiftId).Distinct(), token);
        var shifts = await store.ListAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == yearId && (shiftIds.Count == 0 || shiftIds.Contains(shift.Id))), token);
        var counts = (command.DayLessons ?? []).Select(entry => new DayLessons(entry.Day, entry.Lessons)).ToArray();
        if (StoreSaving.TryDomain<StageDto>(() => stage.SetDayLessons(counts, days, day => shifts.Count == 0 ? 0 : shifts.Max(shift => shift.LessonsOn(day)))) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, "StageDayLessonsUpdated", $"stage:{id}", "Stage lessons per day updated.");
        return await store.SaveAsync(() => ToDto(stage), "DayLessons", token);
    }
}
