using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Teachers;

/// <summary>
/// Teachers with constraints checked against the current schedule grid; unique short names after normalization;
/// soft archive; hard delete (nothing references teachers before Phase 3); bulk add with a preview.
/// </summary>
public sealed class TeachersService(IDataStore store, TimeProvider clock)
{
    public async Task<PagedResult<TeacherDto>> ListAsync(ListQuery query, bool? released, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(query);
        var rows = store.Query<Teacher>();
        if (!query.WithArchived)
            rows = rows.Where(row => !row.IsArchived);
        if (released is { } onlyReleased)
            rows = rows.Where(row => row.FullyReleased == onlyReleased);
        var search = query.NormalizedSearch;
        if (search.Length > 0)
            rows = rows.Where(row => row.NormalizedFullName.Contains(search) || row.NormalizedShortName.Contains(search));
        rows = query.SortKey("name") switch
        {
            ("name", true) => rows.OrderByDescending(row => row.NormalizedFullName),
            ("shortName", false) => rows.OrderBy(row => row.NormalizedShortName),
            ("shortName", true) => rows.OrderByDescending(row => row.NormalizedShortName),
            _ => rows.OrderBy(row => row.NormalizedFullName),
        };
        return await store.ToPageAsync(rows, query, ToDto, token);
    }

    public async Task<OperationResult<TeacherDto>> CreateAsync(SaveTeacherCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Quick add sends only the full name: the short name is proposed like in bulk add (DECISIONS_PENDING #13).
        if (string.IsNullOrWhiteSpace(command.ShortName) && !string.IsNullOrWhiteSpace(command.FullName))
            command = command with { ShortName = await ProposeShortNameAsync(command.FullName, token) };
        var input = new InputErrors();
        var details = ToDetails(command, input);
        if (input.Any)
            return input.ToResult<TeacherDto>();
        var grid = await ScheduleGrids.LoadAsync(store, token);
        Teacher? teacher = null;
        if (StoreSaving.TryDomain<TeacherDto>(() => teacher = Teacher.Create(details, grid)) is { } invalid)
            return invalid;
        if (await ShortNameTakenAsync(null, command.ShortName, token))
            return OperationResult.Invalid<TeacherDto>(nameof(command.ShortName), ErrorCodes.DuplicateName);
        store.Add(teacher!);
        AuditTrail.Record(store, clock, "TeacherCreated", "teacher", "Teacher created.");
        return await store.SaveAsync(() => ToDto(teacher!), nameof(command.ShortName), token);
    }

    public async Task<OperationResult<TeacherDto>> UpdateAsync(long id, SaveTeacherCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = new InputErrors();
        var details = ToDetails(command, input);
        if (input.Any)
            return input.ToResult<TeacherDto>();
        if (await FindAsync(id, token) is not { } teacher)
            return OperationResult.Failure<TeacherDto>(ErrorCodes.NotFound);
        if (!teacher.IsVersion(command.Version))
            return OperationResult.Failure<TeacherDto>(ErrorCodes.Conflict);
        if (await ShortNameTakenAsync(id, command.ShortName, token))
            return OperationResult.Invalid<TeacherDto>(nameof(command.ShortName), ErrorCodes.DuplicateName);
        var grid = await ScheduleGrids.LoadAsync(store, token);
        if (StoreSaving.TryDomain<TeacherDto>(() => teacher.Update(details, grid)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, "TeacherUpdated", $"teacher:{id}", "Teacher updated.");
        return await store.SaveAsync(() => ToDto(teacher), nameof(command.ShortName), token);
    }

    public async Task<OperationResult<TeacherDto>> SetArchivedAsync(long id, int version, bool archived, CancellationToken token)
    {
        if (await FindAsync(id, token) is not { } teacher)
            return OperationResult.Failure<TeacherDto>(ErrorCodes.NotFound);
        if (!teacher.IsVersion(version))
            return OperationResult.Failure<TeacherDto>(ErrorCodes.Conflict);
        if (archived)
            teacher.Archive(clock.GetUtcNow());
        else
            teacher.Restore();
        AuditTrail.Record(store, clock, archived ? "TeacherArchived" : "TeacherRestored", $"teacher:{id}", archived ? "Teacher archived." : "Teacher restored.");
        return await store.SaveAsync(() => ToDto(teacher), "ShortName", token);
    }

    public async Task<OperationResult<bool>> DeleteAsync(long id, int version, CancellationToken token)
    {
        if (await FindAsync(id, token) is not { } teacher)
            return OperationResult.Failure<bool>(ErrorCodes.NotFound);
        if (!teacher.IsVersion(version))
            return OperationResult.Failure<bool>(ErrorCodes.Conflict);
        store.Remove(teacher);
        AuditTrail.Record(store, clock, "TeacherDeleted", $"teacher:{id}", "Teacher deleted.");
        return await store.SaveAsync(() => true, "ShortName", token);
    }

    public async Task<OperationResult<BulkPreviewDto>> PreviewBulkAsync(BulkTeachersCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (CheckBulkSize(command) is { } invalid)
            return invalid.Cast<BulkPreviewDto>();
        var lines = await TeacherBulkAdd.ClassifyAsync(store, command.Names!, token);
        return OperationResult.Success(new BulkPreviewDto(lines, lines.Count(line => line.Status == TeacherBulkAdd.Ready)));
    }

    /// <summary>Creates every "ready" line in one save; refuses the batch if any line is not ready (re-checked).</summary>
    public async Task<OperationResult<BulkCreatedDto>> CreateBulkAsync(BulkTeachersCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (CheckBulkSize(command) is { } invalid)
            return invalid;
        var lines = await TeacherBulkAdd.ClassifyAsync(store, command.Names!, token);
        var rejected = lines.Where(line => line.Status != TeacherBulkAdd.Ready)
            .Select(line => new FieldError($"Names[{line.Line - 1}]", line.Status == "tooLong" ? ErrorCodes.ValueTooLong : ErrorCodes.DuplicateName))
            .ToArray();
        if (rejected.Length > 0)
            return OperationResult.Invalid<BulkCreatedDto>(rejected);
        if (lines.Count == 0)
            return OperationResult.Invalid<BulkCreatedDto>("Names", ErrorCodes.Required);
        var grid = await ScheduleGrids.LoadAsync(store, token);
        foreach (var line in lines)
            store.Add(Teacher.Create(new TeacherDetails(line.FullName, line.ShortName, null, null, false, null, null, null, null, null, null), grid));
        AuditTrail.Record(store, clock, "TeachersBulkCreated", "teacher", $"{lines.Count} teachers created.");
        return await store.SaveAsync(() => new BulkCreatedDto(lines.Count), "Names", token);
    }

    private static OperationResult<BulkCreatedDto>? CheckBulkSize(BulkTeachersCommand command) =>
        command.Names is null || command.Names.All(string.IsNullOrWhiteSpace)
            ? OperationResult.Invalid<BulkCreatedDto>("Names", ErrorCodes.Required)
            : command.Names.Count > TeacherBulkAdd.MaxLines
                ? OperationResult.Invalid<BulkCreatedDto>("Names", ErrorCodes.ValueOutOfRange)
                : null;

    private static TeacherDetails ToDetails(SaveTeacherCommand command, InputErrors input) => new(
        command.FullName,
        command.ShortName,
        command.OffDays,
        ScheduleGrids.FromDtos(command.BlockedPeriods),
        command.FullyReleased,
        command.ReleaseReason,
        command.FullyReleased ? input.OptionalDate(command.ReleaseFrom, nameof(command.ReleaseFrom)) : null,
        command.FullyReleased ? input.OptionalDate(command.ReleaseTo, nameof(command.ReleaseTo)) : null,
        command.MaxLessonsPerDay,
        command.MaxLessonsPerWeek,
        command.Notes);

    /// <summary>A free short name, or null (then the required-field error asks the owner to type one).</summary>
    private async Task<string?> ProposeShortNameAsync(string fullName, CancellationToken token)
    {
        var taken = (await store.ListAsync(store.Query<Teacher>().Select(row => row.NormalizedShortName), token)).ToHashSet(StringComparer.Ordinal);
        return TeacherNames.ProposeShortName(fullName, taken.Contains);
    }

    private Task<Teacher?> FindAsync(long id, CancellationToken token) =>
        store.FirstOrDefaultAsync(store.Query<Teacher>().Where(row => row.Id == id), token);

    private async Task<bool> ShortNameTakenAsync(long? exceptId, string? shortName, CancellationToken token)
    {
        var normalized = ArabicText.Normalize(shortName);
        return normalized.Length > 0 && await store.AnyAsync(
            store.Query<Teacher>().Where(row => row.Id != exceptId && row.NormalizedShortName == normalized), token);
    }

    private static TeacherDto ToDto(Teacher row) => new(
        row.Id,
        row.FullName,
        row.ShortName,
        row.OffDays,
        ScheduleGrids.ToDtos(row.BlockedPeriods),
        row.FullyReleased,
        row.ReleaseReason,
        InputParsing.Format(row.ReleaseFrom),
        InputParsing.Format(row.ReleaseTo),
        row.MaxLessonsPerDay,
        row.MaxLessonsPerWeek,
        row.Notes,
        row.IsArchived,
        row.ArchivedAt,
        row.Version);
}
