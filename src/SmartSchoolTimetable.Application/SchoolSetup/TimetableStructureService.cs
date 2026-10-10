using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.SchoolSetup;

public sealed class TimetableStructureService(IDataStore store, TimeProvider clock)
{
    private readonly ReferenceGuard references = new(store);

    public async Task<PagedResult<ShiftDto>> ListShiftsAsync(long yearId, ListQuery query, CancellationToken token)
    {
        var shifts = store.Read<Shift>().Where(item => item.AcademicYearId == yearId);
        if (query.NormalizedSearch.Length > 0)
            shifts = shifts.Where(item => item.NormalizedName.Contains(query.NormalizedSearch));
        shifts = query.SortKey("order") switch
        {
            ("name", false) => shifts.OrderBy(item => item.NormalizedName),
            ("name", true) => shifts.OrderByDescending(item => item.NormalizedName),
            ("order", true) => shifts.OrderByDescending(item => item.DisplayOrder).ThenBy(item => item.NormalizedName),
            _ => shifts.OrderBy(item => item.DisplayOrder).ThenBy(item => item.NormalizedName),
        };
        var days = await WorkingDaysAsync(token);
        return await store.ToPageAsync(shifts, query, shift => ToDto(shift, days), token);
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
        // R3: daily sessions share the year's single shift; a second shift would leave them without a grid.
        if (await SessionPlanService.ActiveAsync(store, yearId, token) is not null)
            return OperationResult.Failure<ShiftDto>(ErrorCodes.SessionsNeedOneShift);
        store.Add(shift!);
        AuditTrail.Record(store, clock, "ShiftCreated", $"academic-year:{yearId}", "Shift created.");
        var createdDays = await WorkingDaysAsync(token);
        return await store.SaveAsync(() => ToDto(shift!, createdDays), nameof(command.Name), token);
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
        var days = await WorkingDaysAsync(token);
        return await store.SaveAsync(() => ToDto(shift, days), nameof(command.Name), token);
    }

    /// <summary>Hard delete, refused while any section (archived included) uses the shift.</summary>
    public async Task<OperationResult<bool>> DeleteShiftAsync(long yearId, long id, int version, CancellationToken token)
    {
        if (await FindShiftAsync(yearId, id, token) is not { } shift)
            return OperationResult.Failure<bool>(ErrorCodes.NotFound);
        if (!shift.IsVersion(version))
            return OperationResult.Failure<bool>(ErrorCodes.Conflict);
        if (await references.DeleteBlockedAsync(ReferenceKinds.Shift, id, token) is { } inUse)
            return OperationResult.Failure<bool>(inUse);
        store.Remove(shift);
        AuditTrail.Record(store, clock, "ShiftDeleted", $"shift:{id}", "Shift deleted.");
        return await store.SaveAsync(() => true, "Name", token);
    }

    /// <summary>Per-day lesson counts of a shift (spec 2.5 §3.1); each count is 0..lesson count, days are working days.</summary>
    public async Task<OperationResult<ShiftDto>> SetDayLessonsAsync(long yearId, long id, SetDayLessonsCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindShiftAsync(yearId, id, token) is not { } shift)
            return OperationResult.Failure<ShiftDto>(ErrorCodes.NotFound);
        if (!shift.IsVersion(command.Version))
            return OperationResult.Failure<ShiftDto>(ErrorCodes.Conflict);
        var days = await WorkingDaysAsync(token);
        var counts = (command.DayLessons ?? []).Select(entry => new DayLessons(entry.Day, entry.Lessons)).ToArray();
        if (StoreSaving.TryDomain<ShiftDto>(() => shift.SetDayLessons(counts, days)) is { } invalid)
            return invalid;
        // ADR 0027: a stage may teach fewer lessons than its shift, never more. Lowering the shift below a stage's
        // own count needs the owner's confirmation (after the impact preview); then those counts are lowered too.
        var impact = await StageImpactAsync(shift, days, token);
        if (impact.Count > 0 && !command.ConfirmStageChanges)
            return OperationResult.Failure<ShiftDto>(ErrorCodes.StageLessonsAboveShift);
        foreach (var (stage, entries) in impact.GroupBy(item => item.Stage).Select(group => (group.Key, group.ToArray())))
        {
            foreach (var entry in entries)
                stage.ClampDayLessons(entry.Day, entry.ShiftLessons);
            AuditTrail.Record(store, clock, "StageDayLessonsLowered", $"stage:{stage.Id}", "Stage lessons lowered to the shortened shift.");
        }
        AuditTrail.Record(store, clock, "ShiftDayLessonsUpdated", $"shift:{id}", "Lessons per day updated.");
        return await store.SaveAsync(() => ToDto(shift, days), "DayLessons", token);
    }

    /// <summary>Which stages would teach more than the shift after the proposed per-day counts (nothing is saved).</summary>
    public async Task<OperationResult<IReadOnlyList<StageLessonsImpactDto>>> PreviewDayLessonsAsync(long yearId, long id, SetDayLessonsCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindShiftAsync(yearId, id, token) is not { } shift)
            return OperationResult.Failure<IReadOnlyList<StageLessonsImpactDto>>(ErrorCodes.NotFound);
        var days = await WorkingDaysAsync(token);
        var proposed = (command.DayLessons ?? []).Select(entry => new DayLessons(entry.Day, entry.Lessons)).ToArray();
        // The preview applies the counts to a detached copy so the tracked shift is never changed.
        var copy = shift.CopyTo(shift.AcademicYearId);
        if (StoreSaving.TryDomain<IReadOnlyList<StageLessonsImpactDto>>(() => copy.SetDayLessons(proposed, days)) is { } invalid)
            return invalid;
        var impact = await StageImpactAsync(copy, days, token, shift.Id);
        return OperationResult.Success<IReadOnlyList<StageLessonsImpactDto>>(impact
            .Select(item => new StageLessonsImpactDto(item.Stage.Id, item.Stage.Name, item.Day, item.StageLessons, item.ShiftLessons)).ToArray());
    }

    private sealed record StageImpact(Stage Stage, int Day, int StageLessons, int ShiftLessons);

    private async Task<List<StageImpact>> StageImpactAsync(Shift shift, IReadOnlyCollection<int> days, CancellationToken token, long? shiftId = null)
    {
        var id = shiftId ?? shift.Id;
        var stageIds = await store.ListAsync(store.Query<Section>().Where(section => section.ShiftId == id && !section.IsArchived).Select(section => section.StageId).Distinct(), token);
        var stages = await store.ListAsync(store.Query<Stage>().Where(stage => stageIds.Contains(stage.Id)), token);
        return stages
            .SelectMany(stage => stage.DayLessonCounts.Where(entry => days.Contains(entry.Day) && entry.Lessons > shift.LessonsOn(entry.Day))
                .Select(entry => new StageImpact(stage, entry.Day, entry.Lessons, shift.LessonsOn(entry.Day))))
            .OrderBy(item => item.Stage.DisplayOrder).ThenBy(item => item.Day)
            .ToList();
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
        // R3: every daily session has the same number of lessons; change the sessions first (or go back to one session).
        if (await SessionPlanService.ActiveAsync(store, yearId, token) is { } sessions && sessions.ShiftId == id
            && drafts.Count(draft => draft.Kind == PeriodKind.Lesson) != shift.LessonCount)
            return OperationResult.Invalid<ShiftDto>(nameof(command.Periods), ErrorCodes.SessionLessonCountMismatch);
        if (StoreSaving.TryDomain<ShiftDto>(() => shift.ReplacePeriods(drafts)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, "ShiftPeriodsUpdated", $"shift:{id}", "Shift periods updated.");
        var days = await WorkingDaysAsync(token);
        return await store.SaveAsync(() => ToDto(shift, days), nameof(command.Periods), token);
    }

    public static OperationResult<GeneratedPeriodsDto> Generate(GeneratePeriodsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = new InputErrors();
        var start = errors.Time(command.FirstStartTime, nameof(command.FirstStartTime));
        if (errors.Any) return OperationResult.Invalid<GeneratedPeriodsDto>(errors.Errors);
        try
        {
            var plan = command.Breaks is { } breaks
                ? new PeriodPlan(start, command.LessonMinutes, command.LessonCount, breaks.Select(slot => new BreakSlot(slot.AfterLesson, slot.Minutes)).ToArray())
                : new PeriodPlan(start, command.LessonMinutes, command.LessonCount, command.BreakMinutes, command.BreakAfterLesson);
            var drafts = PeriodGenerator.Generate(plan with { GapMinutes = command.GapMinutes });
            return OperationResult.Success(new GeneratedPeriodsDto(drafts.Select((period, index) => ToDto(new LessonPeriod(index + 1, period))).ToArray()));
        }
        catch (DomainValidationException error) { return OperationResult.FromDomain<GeneratedPeriodsDto>(error); }
    }

    /// <summary>The day × lesson grid used by blocked-period editors (working days and current lessons per day).</summary>
    public async Task<ScheduleGridDto> GetGridAsync(CancellationToken token) => ScheduleGrids.ToDto(await ScheduleGrids.LoadAsync(store, token));

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

    private async Task<IReadOnlyList<int>> WorkingDaysAsync(CancellationToken token) =>
        (await store.FirstOrDefaultAsync(store.Query<WorkingWeek>(), token) ?? WorkingWeek.CreateDefault()).Days;

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
    internal static ShiftDto ToDto(Shift value, IReadOnlyList<int> workingDays) => new(
        value.Id,
        value.AcademicYearId,
        value.Name,
        value.DisplayOrder,
        ApiText.ToValue(value.Kind),
        value.LessonCount,
        value.Periods.Select(ToDto).ToArray(),
        workingDays.Select(day => new DayLessonsDto(day, value.LessonsOn(day))).ToArray(),
        value.WeeklyLessons(workingDays),
        value.Version);
    private static LessonPeriodDto ToDto(LessonPeriod value) => new(value.Position, ApiText.ToValue(value.Kind), InputParsing.Format(value.StartTime), InputParsing.Format(value.EndTime), value.StartBell, value.EndBell);
    private static WorkingWeekDto ToDto(WorkingWeek value) => new(value.Days, value.WeekStartDay, value.Version);
    private static BellSettingsDto ToDto(BellSettings value) => new(ApiText.ToValue(value.Tone), value.BreakBell, value.Version);
}
