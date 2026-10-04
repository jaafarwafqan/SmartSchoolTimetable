using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.SchoolSetup;

/// <summary>Year-scoped stage and section setup, with soft archive and concurrency checks.</summary>
public sealed class StagesSectionsService(IDataStore store, TimeProvider clock)
{
    public async Task<PagedResult<StageDto>> ListStagesAsync(long yearId, ListQuery query, CancellationToken token)
    {
        var rows = store.Query<Stage>().Where(row => row.AcademicYearId == yearId);
        if (!query.WithArchived) rows = rows.Where(row => !row.IsArchived);
        if (query.NormalizedSearch.Length > 0) rows = rows.Where(row => row.NormalizedName.Contains(query.NormalizedSearch));
        rows = query.SortKey("order") switch
        {
            ("name", true) => rows.OrderByDescending(row => row.NormalizedName),
            ("name", false) => rows.OrderBy(row => row.NormalizedName),
            ("order", true) => rows.OrderByDescending(row => row.DisplayOrder),
            _ => rows.OrderBy(row => row.DisplayOrder)
        };
        return await store.ToPageAsync(rows, query, ToDto, token);
    }

    public async Task<PagedResult<SectionDto>> ListSectionsAsync(long yearId, long stageId, ListQuery query, CancellationToken token)
    {
        var stageExists = await store.AnyAsync(store.Query<Stage>().Where(row => row.Id == stageId && row.AcademicYearId == yearId), token);
        if (!stageExists) return new PagedResult<SectionDto>([], 0, query.SafePage, query.SafePageSize);
        var rows = store.Query<Section>().Where(row => row.StageId == stageId);
        if (!query.WithArchived) rows = rows.Where(row => !row.IsArchived);
        if (query.NormalizedSearch.Length > 0) rows = rows.Where(row => row.NormalizedLabel.Contains(query.NormalizedSearch));
        rows = query.SortKey("label") switch
        {
            ("label", true) => rows.OrderByDescending(row => row.NormalizedLabel),
            _ => rows.OrderBy(row => row.NormalizedLabel)
        };
        var page = await store.ToPageAsync(rows, query, row => row, token);
        var mapped = new List<SectionDto>(page.Items.Count);
        foreach (var row in page.Items) mapped.Add(await ToDtoAsync(row, token));
        return new PagedResult<SectionDto>(mapped, page.Total, page.Page, page.PageSize);
    }

    public async Task<OperationResult<StageDto>> CreateStageAsync(long yearId, SaveStageCommand command, CancellationToken token)
    {
        if (!await store.AnyAsync(store.Query<AcademicYear>().Where(row => row.Id == yearId), token)) return OperationResult.Failure<StageDto>(ErrorCodes.NotFound);
        var invalid = ValidateStage(command.Name, command.DisplayOrder);
        if (invalid is not null) return OperationResult.Invalid<StageDto>(invalid.Value.Field, invalid.Value.Code);
        var normalized = ArabicText.Normalize(command.Name);
        if (await store.AnyAsync(store.Query<Stage>().Where(row => row.AcademicYearId == yearId && (row.NormalizedName == normalized || row.DisplayOrder == command.DisplayOrder)), token))
            return OperationResult.Invalid<StageDto>(nameof(Stage.Name), ErrorCodes.DuplicateName);
        var stage = Stage.Create(yearId, command.Name, command.DisplayOrder);
        store.Add(stage);
        AuditTrail.Record(store, clock, "StageCreated", $"stage:{stage.Id}", "Stage created.");
        return await SaveStageAsync(stage, token);
    }

    public async Task<OperationResult<StageDto>> UpdateStageAsync(long yearId, long id, SaveStageCommand command, CancellationToken token)
    {
        var stage = await FindStageAsync(yearId, id, token);
        if (stage is null) return OperationResult.Failure<StageDto>(ErrorCodes.NotFound);
        if (!stage.IsVersion(command.Version)) return OperationResult.Failure<StageDto>(ErrorCodes.Conflict);
        var invalid = ValidateStage(command.Name, command.DisplayOrder);
        if (invalid is not null) return OperationResult.Invalid<StageDto>(invalid.Value.Field, invalid.Value.Code);
        var normalized = ArabicText.Normalize(command.Name);
        if (await store.AnyAsync(store.Query<Stage>().Where(row => row.AcademicYearId == yearId && row.Id != id && (row.NormalizedName == normalized || row.DisplayOrder == command.DisplayOrder)), token))
            return OperationResult.Invalid<StageDto>(nameof(Stage.Name), ErrorCodes.DuplicateName);
        try { stage.Update(command.Name, command.DisplayOrder); }
        catch (DomainValidationException error) { return OperationResult.FromDomain<StageDto>(error); }
        AuditTrail.Record(store, clock, "StageUpdated", $"stage:{id}", "Stage updated.");
        return await SaveStageAsync(stage, token);
    }

    public async Task<OperationResult<StageDto>> SetStageArchivedAsync(long yearId, long id, ArchiveCommand command, bool archived, CancellationToken token)
    {
        var stage = await FindStageAsync(yearId, id, token);
        if (stage is null) return OperationResult.Failure<StageDto>(ErrorCodes.NotFound);
        if (!stage.IsVersion(command.Version)) return OperationResult.Failure<StageDto>(ErrorCodes.Conflict);
        if (archived && await store.AnyAsync(store.Query<Section>().Where(row => row.StageId == id && !row.IsArchived), token))
            return OperationResult.Failure<StageDto>(ErrorCodes.RecordInUse);
        if (archived) stage.Archive(clock.GetUtcNow()); else stage.Restore();
        AuditTrail.Record(store, clock, archived ? "StageArchived" : "StageRestored", $"stage:{id}", archived ? "Stage archived." : "Stage restored.");
        return await SaveStageAsync(stage, token);
    }

    public async Task<OperationResult<SectionDto>> CreateSectionAsync(long yearId, long stageId, SaveSectionCommand command, CancellationToken token)
    {
        var stage = await FindStageAsync(yearId, stageId, token);
        if (stage is null || stage.IsArchived) return OperationResult.Failure<SectionDto>(ErrorCodes.NotFound);
        var error = ValidateSection(command);
        if (error is not null) return OperationResult.Invalid<SectionDto>(error.Value.Field, error.Value.Code);
        if (!await store.AnyAsync(store.Query<Shift>().Where(row => row.Id == command.ShiftId && row.AcademicYearId == yearId), token)) return OperationResult.Invalid<SectionDto>(nameof(command.ShiftId), ErrorCodes.ShiftNotInYear);
        var normalized = ArabicText.Normalize(command.Label);
        if (await store.AnyAsync(store.Query<Section>().Where(row => row.StageId == stageId && row.NormalizedLabel == normalized), token)) return OperationResult.Invalid<SectionDto>(nameof(command.Label), ErrorCodes.DuplicateName);
        var section = Section.Create(stageId, command.ShiftId, command.Label, command.StudentCount);
        store.Add(section);
        AuditTrail.Record(store, clock, "SectionCreated", $"section:{section.Id}", "Section created.");
        return await SaveSectionAsync(section, token);
    }

    public async Task<OperationResult<SectionDto>> UpdateSectionAsync(long yearId, long stageId, long id, SaveSectionCommand command, CancellationToken token)
    {
        var section = await FindSectionAsync(yearId, stageId, id, token);
        if (section is null) return OperationResult.Failure<SectionDto>(ErrorCodes.NotFound);
        if (!section.IsVersion(command.Version)) return OperationResult.Failure<SectionDto>(ErrorCodes.Conflict);
        var error = ValidateSection(command);
        if (error is not null) return OperationResult.Invalid<SectionDto>(error.Value.Field, error.Value.Code);
        if (!await store.AnyAsync(store.Query<Shift>().Where(row => row.Id == command.ShiftId && row.AcademicYearId == yearId), token)) return OperationResult.Invalid<SectionDto>(nameof(command.ShiftId), ErrorCodes.ShiftNotInYear);
        var normalized = ArabicText.Normalize(command.Label);
        if (await store.AnyAsync(store.Query<Section>().Where(row => row.StageId == stageId && row.Id != id && row.NormalizedLabel == normalized), token)) return OperationResult.Invalid<SectionDto>(nameof(command.Label), ErrorCodes.DuplicateName);
        try { section.Update(command.ShiftId, command.Label, command.StudentCount); }
        catch (DomainValidationException ex) { return OperationResult.FromDomain<SectionDto>(ex); }
        AuditTrail.Record(store, clock, "SectionUpdated", $"section:{id}", "Section updated.");
        return await SaveSectionAsync(section, token);
    }

    public async Task<OperationResult<SectionDto>> SetSectionArchivedAsync(long yearId, long stageId, long id, ArchiveCommand command, bool archived, CancellationToken token)
    {
        var section = await FindSectionAsync(yearId, stageId, id, token);
        if (section is null) return OperationResult.Failure<SectionDto>(ErrorCodes.NotFound);
        if (!section.IsVersion(command.Version)) return OperationResult.Failure<SectionDto>(ErrorCodes.Conflict);
        if (archived) section.Archive(clock.GetUtcNow()); else section.Restore();
        AuditTrail.Record(store, clock, archived ? "SectionArchived" : "SectionRestored", $"section:{id}", archived ? "Section archived." : "Section restored.");
        return await SaveSectionAsync(section, token);
    }

    private async Task<OperationResult<StageDto>> SaveStageAsync(Stage row, CancellationToken token)
    {
        try { await store.SaveChangesAsync(token); return OperationResult.Success(ToDto(row)); }
        catch (ConcurrencyConflictException) { return OperationResult.Failure<StageDto>(ErrorCodes.Conflict); }
        catch (DataConflictException) { return OperationResult.Failure<StageDto>(ErrorCodes.Conflict); }
    }
    private async Task<OperationResult<SectionDto>> SaveSectionAsync(Section row, CancellationToken token)
    {
        try { await store.SaveChangesAsync(token); return OperationResult.Success(await ToDtoAsync(row, token)); }
        catch (ConcurrencyConflictException) { return OperationResult.Failure<SectionDto>(ErrorCodes.Conflict); }
        catch (DataConflictException) { return OperationResult.Failure<SectionDto>(ErrorCodes.Conflict); }
    }
    private async Task<SectionDto> ToDtoAsync(Section row, CancellationToken token)
    {
        var shift = await store.FirstOrDefaultAsync(store.Query<Shift>().Where(item => item.Id == row.ShiftId), token);
        var days = await store.FirstOrDefaultAsync(store.Query<WorkingWeek>().Where(item => item.Id == WorkingWeek.SingletonId), token);
        return new SectionDto(row.Id, row.StageId, row.Label, row.ShiftId, row.StudentCount, (days?.Days.Count ?? 0) * (shift?.LessonCount ?? 0), row.IsArchived, row.ArchivedAt, row.Version);
    }
    private Task<Stage?> FindStageAsync(long yearId, long id, CancellationToken token) => store.FirstOrDefaultAsync(store.Query<Stage>().Where(row => row.Id == id && row.AcademicYearId == yearId), token);
    private async Task<Section?> FindSectionAsync(long yearId, long stageId, long id, CancellationToken token)
    {
        if (await FindStageAsync(yearId, stageId, token) is null) return null;
        return await store.FirstOrDefaultAsync(store.Query<Section>().Where(row => row.Id == id && row.StageId == stageId), token);
    }
    private static StageDto ToDto(Stage row) => new(row.Id, row.AcademicYearId, row.Name, row.DisplayOrder, row.IsArchived, row.ArchivedAt, row.Version);
    private static (string Field, string Code)? ValidateStage(string? name, int order) => string.IsNullOrWhiteSpace(name) ? (nameof(Stage.Name), ErrorCodes.Required) : name.Trim().Length > Stage.NameMaxLength ? (nameof(Stage.Name), ErrorCodes.ValueTooLong) : order is < 1 or > 999 ? (nameof(Stage.DisplayOrder), ErrorCodes.ValueOutOfRange) : null;
    private static (string Field, string Code)? ValidateSection(SaveSectionCommand command) => string.IsNullOrWhiteSpace(command.Label) ? (nameof(command.Label), ErrorCodes.Required) : command.Label.Trim().Length > Section.LabelMaxLength ? (nameof(command.Label), ErrorCodes.ValueTooLong) : command.StudentCount is < 0 or > 200 ? (nameof(command.StudentCount), ErrorCodes.ValueOutOfRange) : command.ShiftId < 1 ? (nameof(command.ShiftId), ErrorCodes.Required) : null;
}
