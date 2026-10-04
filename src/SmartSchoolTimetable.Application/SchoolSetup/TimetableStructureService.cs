using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.SchoolSetup;

public sealed class TimetableStructureService(IDataStore store, TimeProvider clock)
{
    public async Task<PagedResult<ShiftDto>> ListShiftsAsync(long yearId, ListQuery query, CancellationToken token)
    {
        var shifts = store.Query<Shift>().Where(item => item.AcademicYearId == yearId);
        if (query.NormalizedSearch.Length > 0)
            shifts = shifts.Where(item => item.NormalizedName.Contains(query.NormalizedSearch));
        shifts = query.SortKey("order") switch
        {
            ("name", false) => shifts.OrderBy(item => item.NormalizedName),
            ("name", true) => shifts.OrderByDescending(item => item.NormalizedName),
            ("order", true) => shifts.OrderByDescending(item => item.DisplayOrder).ThenBy(item => item.NormalizedName),
            _ => shifts.OrderBy(item => item.DisplayOrder).ThenBy(item => item.NormalizedName),
        };
        return await store.ToPageAsync(shifts, query, ToDto, token);
    }

    public async Task<OperationResult<ShiftDto>> CreateShiftAsync(long yearId, SaveShiftCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!await store.AnyAsync(store.Query<AcademicYear>().Where(year => year.Id == yearId), token))
            return OperationResult.Failure<ShiftDto>(ErrorCodes.NotFound);
        Shift? shift = null;
        if (StoreSaving.TryDomain<ShiftDto>(() => shift = Shift.Create(yearId, command.Name, command.DisplayOrder)) is { } invalid)
            return invalid;
        if (await ShiftNameTakenAsync(yearId, null, command.Name, token))
            return OperationResult.Invalid<ShiftDto>(nameof(command.Name), ErrorCodes.DuplicateName);
        store.Add(shift!);
        AuditTrail.Record(store, clock, "ShiftCreated", $"academic-year:{yearId}", "Shift created.");
        return await store.SaveAsync(() => ToDto(shift!), nameof(command.Name), token);
    }

    public async Task<OperationResult<ShiftDto>> UpdateShiftAsync(long yearId, long id, SaveShiftCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindShiftAsync(yearId, id, token) is not { } shift)
            return OperationResult.Failure<ShiftDto>(ErrorCodes.NotFound);
        if (!shift.IsVersion(command.Version))
            return OperationResult.Failure<ShiftDto>(ErrorCodes.Conflict);
        if (await ShiftNameTakenAsync(yearId, id, command.Name, token))
            return OperationResult.Invalid<ShiftDto>(nameof(command.Name), ErrorCodes.DuplicateName);
        if (StoreSaving.TryDomain<ShiftDto>(() => shift.Update(command.Name, command.DisplayOrder)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, "ShiftUpdated", $"shift:{id}", "Shift updated.");
        return await store.SaveAsync(() => ToDto(shift), nameof(command.Name), token);
    }

    /// <summary>Hard delete, refused while any section (archived included) uses the shift.</summary>
    public async Task<OperationResult<bool>> DeleteShiftAsync(long yearId, long id, int version, CancellationToken token)
    {
        if (await FindShiftAsync(yearId, id, token) is not { } shift)
            return OperationResult.Failure<bool>(ErrorCodes.NotFound);
        if (!shift.IsVersion(version))
            return OperationResult.Failure<bool>(ErrorCodes.Conflict);
        if (await store.AnyAsync(store.Query<Section>().Where(section => section.ShiftId == id), token))
            return OperationResult.Failure<bool>(ErrorCodes.RecordInUse);
        store.Remove(shift);
        AuditTrail.Record(store, clock, "ShiftDeleted", $"shift:{id}", "Shift deleted.");
        return await store.SaveAsync(() => true, "Name", token);
    }

    public async Task<OperationResult<ShiftDto>> ReplacePeriodsAsync(long yearId, long id, ReplacePeriodsCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindShiftAsync(yearId, id, token) is not { } shift)
            return OperationResult.Failure<ShiftDto>(ErrorCodes.NotFound);
        if (!shift.IsVersion(command.Version))
            return OperationResult.Failure<ShiftDto>(ErrorCodes.Conflict);
        var input = new InputErrors();
        var drafts = new List<PeriodDraft>();
        if (command.Periods is null)
            input.Add(nameof(command.Periods), ErrorCodes.Required);
        for (var index = 0; index < (command.Periods?.Count ?? 0); index++)
        {
            var item = command.Periods![index];
            var kind = input.Option<PeriodKind>(item.Kind, $"Periods[{index}].Kind");
            var start = input.Time(item.StartTime, $"Periods[{index}].StartTime");
            var end = input.Time(item.EndTime, $"Periods[{index}].EndTime");
            drafts.Add(new PeriodDraft(kind, start, end, item.StartBell, item.EndBell));
        }
        if (input.Any)
            return input.ToResult<ShiftDto>();
        if (StoreSaving.TryDomain<ShiftDto>(() => shift.ReplacePeriods(drafts)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, "ShiftPeriodsUpdated", $"shift:{id}", "Shift periods updated.");
        return await store.SaveAsync(() => ToDto(shift), nameof(command.Periods), token);
    }

    public static OperationResult<GeneratedPeriodsDto> Generate(GeneratePeriodsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = new InputErrors();
        var start = errors.Time(command.FirstStartTime, nameof(command.FirstStartTime));
        if (errors.Any) return OperationResult.Invalid<GeneratedPeriodsDto>(errors.Errors);
        try
        {
            var drafts = PeriodGenerator.Generate(new PeriodPlan(start, command.LessonMinutes, command.LessonCount, command.BreakMinutes, command.BreakAfterLesson));
            return OperationResult.Success(new GeneratedPeriodsDto(drafts.Select((period, index) => ToDto(new LessonPeriod(index + 1, period))).ToArray()));
        }
        catch (DomainValidationException error) { return OperationResult.FromDomain<GeneratedPeriodsDto>(error); }
    }

    public async Task<WorkingWeekDto> GetWorkingWeekAsync(CancellationToken token) => ToDto(await LoadWeekAsync(token));

    public async Task<OperationResult<WorkingWeekDto>> UpdateWorkingWeekAsync(UpdateWorkingWeekCommand command, CancellationToken token)
    {
        var week = await store.FirstOrDefaultAsync(store.Query<WorkingWeek>().Where(item => item.Id == WorkingWeek.SingletonId), token);
        if (week is null)
        {
            if (command.Version != 0) return OperationResult.Failure<WorkingWeekDto>(ErrorCodes.Conflict);
            week = WorkingWeek.CreateDefault();
            store.Add(week);
        }
        if (week.Version != command.Version) return OperationResult.Failure<WorkingWeekDto>(ErrorCodes.Conflict);
        if (StoreSaving.TryDomain<WorkingWeekDto>(() => week.Update(command.Days, command.WeekStartDay)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, "WorkingWeekUpdated", "working-week", "Working week updated.");
        return await store.SaveAsync(() => ToDto(week), "Days", token);
    }

    public async Task<BellSettingsDto> GetBellSettingsAsync(CancellationToken token) => ToDto(await LoadBellAsync(token));

    public async Task<OperationResult<BellSettingsDto>> UpdateBellSettingsAsync(UpdateBellSettingsCommand command, CancellationToken token)
    {
        var settings = await store.FirstOrDefaultAsync(store.Query<BellSettings>().Where(item => item.Id == BellSettings.SingletonId), token);
        if (settings is null)
        {
            if (command.Version != 0) return OperationResult.Failure<BellSettingsDto>(ErrorCodes.Conflict);
            settings = BellSettings.CreateDefault();
            store.Add(settings);
        }
        if (settings.Version != command.Version) return OperationResult.Failure<BellSettingsDto>(ErrorCodes.Conflict);
        var input = new InputErrors();
        var tone = input.Option<BellTone>(command.Tone, nameof(command.Tone));
        if (input.Any) return OperationResult.Invalid<BellSettingsDto>(input.Errors);
        if (StoreSaving.TryDomain<BellSettingsDto>(() => settings.Update(tone, command.BreakBell)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, "BellSettingsUpdated", "bell-settings", "Bell settings updated.");
        return await store.SaveAsync(() => ToDto(settings), nameof(command.Tone), token);
    }

    private Task<Shift?> FindShiftAsync(long yearId, long id, CancellationToken token) =>
        store.FirstOrDefaultAsync(store.Query<Shift>().Where(item => item.Id == id && item.AcademicYearId == yearId), token);

    private async Task<bool> ShiftNameTakenAsync(long yearId, long? exceptId, string? name, CancellationToken token)
    {
        var normalized = ArabicText.Normalize(name);
        return normalized.Length > 0 && await store.AnyAsync(
            store.Query<Shift>().Where(item => item.AcademicYearId == yearId && item.Id != exceptId && item.NormalizedName == normalized),
            token);
    }

    private async Task<WorkingWeek> LoadWeekAsync(CancellationToken token) => await store.FirstOrDefaultAsync(store.Query<WorkingWeek>().Where(item => item.Id == WorkingWeek.SingletonId), token) ?? WorkingWeek.CreateDefault();
    private async Task<BellSettings> LoadBellAsync(CancellationToken token) => await store.FirstOrDefaultAsync(store.Query<BellSettings>().Where(item => item.Id == BellSettings.SingletonId), token) ?? BellSettings.CreateDefault();
    private static ShiftDto ToDto(Shift value) => new(value.Id, value.AcademicYearId, value.Name, value.DisplayOrder, value.LessonCount, value.Periods.Select(ToDto).ToArray(), value.Version);
    private static LessonPeriodDto ToDto(LessonPeriod value) => new(value.Position, ApiText.ToValue(value.Kind), InputParsing.Format(value.StartTime), InputParsing.Format(value.EndTime), value.StartBell, value.EndBell);
    private static WorkingWeekDto ToDto(WorkingWeek value) => new(value.Days, value.WeekStartDay, value.Version);
    private static BellSettingsDto ToDto(BellSettings value) => new(ApiText.ToValue(value.Tone), value.BreakBell, value.Version);
}
