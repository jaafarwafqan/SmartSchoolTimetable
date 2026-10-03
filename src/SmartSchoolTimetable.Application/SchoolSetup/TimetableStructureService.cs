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
            ("order", true) => shifts.OrderByDescending(item => item.DisplayOrder),
            _ => shifts.OrderBy(item => item.DisplayOrder)
        };
        return await store.ToPageAsync(shifts, query, ToDto, token);
    }

    public async Task<OperationResult<ShiftDto>> CreateShiftAsync(long yearId, SaveShiftCommand command, CancellationToken token)
    {
        if (!await store.AnyAsync(store.Query<AcademicYear>().Where(year => year.Id == yearId), token))
            return OperationResult.Failure<ShiftDto>(ErrorCodes.NotFound);
        var validation = new InputErrors();
        ValidateName(command.Name, validation);
        if (command.DisplayOrder is < 1 or > Shift.MaxDisplayOrder)
            validation.Add(nameof(command.DisplayOrder), ErrorCodes.ValueOutOfRange);
        if (validation.Any) return OperationResult.Invalid<ShiftDto>(validation.Errors);
        var normalized = ArabicText.Normalize(command.Name);
        var duplicate = await store.AnyAsync(store.Query<Shift>().Where(item => item.AcademicYearId == yearId && item.NormalizedName == normalized), token)
            || await store.AnyAsync(store.Query<Shift>().Where(item => item.AcademicYearId == yearId && item.DisplayOrder == command.DisplayOrder), token);
        if (duplicate) return OperationResult.Invalid<ShiftDto>(nameof(command.Name), ErrorCodes.DuplicateName);
        var shift = Shift.Create(yearId, command.Name, command.DisplayOrder);
        store.Add(shift);
        AuditTrail.Record(store, clock, "ShiftCreated", "shift", "Shift created.");
        return await SaveShiftResultAsync(shift, token);
    }

    public async Task<OperationResult<ShiftDto>> UpdateShiftAsync(long yearId, long id, SaveShiftCommand command, CancellationToken token)
    {
        var shift = await FindShiftAsync(yearId, id, token);
        if (shift is null) return OperationResult.Failure<ShiftDto>(ErrorCodes.NotFound);
        if (shift.Version != command.Version) return OperationResult.Failure<ShiftDto>(ErrorCodes.Conflict);
        var validation = new InputErrors();
        ValidateName(command.Name, validation);
        if (command.DisplayOrder is < 1 or > Shift.MaxDisplayOrder) validation.Add(nameof(command.DisplayOrder), ErrorCodes.ValueOutOfRange);
        if (validation.Any) return OperationResult.Invalid<ShiftDto>(validation.Errors);
        var normalized = ArabicText.Normalize(command.Name);
        if (await store.AnyAsync(store.Query<Shift>().Where(item => item.AcademicYearId == yearId && item.Id != id && (item.NormalizedName == normalized || item.DisplayOrder == command.DisplayOrder)), token))
            return OperationResult.Invalid<ShiftDto>(nameof(command.Name), ErrorCodes.DuplicateName);
        try { shift.Update(command.Name, command.DisplayOrder); }
        catch (DomainValidationException error) { return OperationResult.FromDomain<ShiftDto>(error); }
        AuditTrail.Record(store, clock, "ShiftUpdated", "shift", "Shift updated.");
        return await SaveShiftResultAsync(shift, token);
    }

    public async Task<OperationResult<ShiftDto>> ReplacePeriodsAsync(long yearId, long id, ReplacePeriodsCommand command, CancellationToken token)
    {
        var shift = await FindShiftAsync(yearId, id, token);
        if (shift is null) return OperationResult.Failure<ShiftDto>(ErrorCodes.NotFound);
        if (shift.Version != command.Version) return OperationResult.Failure<ShiftDto>(ErrorCodes.Conflict);
        var input = new InputErrors();
        var drafts = new List<PeriodDraft>();
        if (command.Periods is null) input.Add(nameof(command.Periods), ErrorCodes.Required);
        else for (var index = 0; index < command.Periods.Count; index++)
        {
            var item = command.Periods[index];
            var kindText = item.Kind switch
            {
                "lesson" => nameof(PeriodKind.Lesson),
                "break" => nameof(PeriodKind.Break),
                _ => item.Kind
            };
            var kind = kindText switch
            {
                nameof(PeriodKind.Lesson) => PeriodKind.Lesson,
                nameof(PeriodKind.Break) => PeriodKind.Break,
                _ => InvalidKind(input, index)
            };
            var start = input.Time(item.StartTime, $"Periods[{index}].StartTime");
            var end = input.Time(item.EndTime, $"Periods[{index}].EndTime");
            drafts.Add(new PeriodDraft(kind, start, end, item.StartBell, item.EndBell));
        }
        if (input.Any) return OperationResult.Invalid<ShiftDto>(input.Errors);
        try { shift.ReplacePeriods(drafts); }
        catch (DomainValidationException error) { return OperationResult.FromDomain<ShiftDto>(error); }
        AuditTrail.Record(store, clock, "ShiftPeriodsUpdated", "shift", "Shift periods updated.");
        return await SaveShiftResultAsync(shift, token);
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
        try { week.Update(command.Days, command.WeekStartDay); }
        catch (DomainValidationException error) { return OperationResult.FromDomain<WorkingWeekDto>(error); }
        AuditTrail.Record(store, clock, "WorkingWeekUpdated", "working-week", "Working week updated.");
        try { await store.SaveChangesAsync(token); return OperationResult.Success(ToDto(week)); }
        catch (ConcurrencyConflictException) { return OperationResult.Failure<WorkingWeekDto>(ErrorCodes.Conflict); }
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
        var tone = (command.Tone ?? string.Empty).ToLowerInvariant() switch
        {
            "classic" => BellTone.Classic,
            "chime" => BellTone.Chime,
            "beeps" => BellTone.Beeps,
            "soft" => BellTone.Soft,
            _ => InvalidTone(input)
        };
        if (input.Any) return OperationResult.Invalid<BellSettingsDto>(input.Errors);
        try { settings.Update(tone, command.BreakBell); }
        catch (DomainValidationException error) { return OperationResult.FromDomain<BellSettingsDto>(error); }
        AuditTrail.Record(store, clock, "BellSettingsUpdated", "bell-settings", "Bell settings updated.");
        try { await store.SaveChangesAsync(token); return OperationResult.Success(ToDto(settings)); }
        catch (ConcurrencyConflictException) { return OperationResult.Failure<BellSettingsDto>(ErrorCodes.Conflict); }
    }

    private async Task<OperationResult<ShiftDto>> SaveShiftResultAsync(Shift shift, CancellationToken token)
    {
        try { await store.SaveChangesAsync(token); return OperationResult.Success(ToDto(shift)); }
        catch (ConcurrencyConflictException) { return OperationResult.Failure<ShiftDto>(ErrorCodes.Conflict); }
        catch (DataConflictException) { return OperationResult.Failure<ShiftDto>(ErrorCodes.Conflict); }
    }

    private Task<Shift?> FindShiftAsync(long yearId, long id, CancellationToken token) => store.FirstOrDefaultAsync(store.Query<Shift>().Where(item => item.Id == id && item.AcademicYearId == yearId), token);
    private static PeriodKind InvalidKind(InputErrors errors, int index)
    {
        errors.Add($"Periods[{index}].Kind", ErrorCodes.InvalidOption);
        return default;
    }

    private static BellTone InvalidTone(InputErrors errors)
    {
        errors.Add(nameof(BellSettings.Tone), ErrorCodes.InvalidOption);
        return default;
    }

    private static void ValidateName(string? name, InputErrors errors)
    {
        if (string.IsNullOrWhiteSpace(name)) errors.Add(nameof(Shift.Name), ErrorCodes.Required);
        else if (name.Trim().Length > Shift.NameMaxLength) errors.Add(nameof(Shift.Name), ErrorCodes.ValueTooLong);
    }
    private async Task<WorkingWeek> LoadWeekAsync(CancellationToken token) => await store.FirstOrDefaultAsync(store.Query<WorkingWeek>().Where(item => item.Id == WorkingWeek.SingletonId), token) ?? WorkingWeek.CreateDefault();
    private async Task<BellSettings> LoadBellAsync(CancellationToken token) => await store.FirstOrDefaultAsync(store.Query<BellSettings>().Where(item => item.Id == BellSettings.SingletonId), token) ?? BellSettings.CreateDefault();
    private static ShiftDto ToDto(Shift value) => new(value.Id, value.AcademicYearId, value.Name, value.DisplayOrder, value.LessonCount, value.Periods.Select(ToDto).ToArray(), value.Version);
    private static LessonPeriodDto ToDto(LessonPeriod value) => new(value.Position, ApiText.ToValue(value.Kind), InputParsing.Format(value.StartTime), InputParsing.Format(value.EndTime), value.StartBell, value.EndBell);
    private static WorkingWeekDto ToDto(WorkingWeek value) => new(value.Days, value.WeekStartDay, value.Version);
    private static BellSettingsDto ToDto(BellSettings value) => new(ApiText.ToValue(value.Tone), value.BreakBell, value.Version);
}
