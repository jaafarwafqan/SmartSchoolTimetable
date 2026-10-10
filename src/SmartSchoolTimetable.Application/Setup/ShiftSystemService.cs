using SmartSchoolTimetable.Application.Backup;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Setup;

public static class ShiftSystems
{
    public const string Morning = "morning";
    public const string Evening = "evening";
    public const string DoubleByDays = "dual";

    public static readonly IReadOnlyList<string> All = [Morning, Evening, DoubleByDays];

    public static string Of(StudyType type) => type switch
    {
        StudyType.Evening => Evening,
        StudyType.Dual => DoubleByDays,
        _ => Morning,
    };

    public static StudyType? Parse(string? value) => value switch
    {
        Morning => StudyType.Morning,
        Evening => StudyType.Evening,
        DoubleByDays => StudyType.Dual,
        _ => null,
    };
}

public sealed record LegacyShiftDto(long Id, string Name, string Kind, int Sections);

/// <summary>
/// The school's system of work: «صباحي» (one morning timing), «مسائي» (one evening timing) or «مزدوج» (the same sections
/// alternate between a morning and an evening session by day). It is one shift in every case; only the times differ.
/// </summary>
/// <param name="System">morning, evening or dual (دوام مزدوج).</param>
/// <param name="Legacy">The current year still has the old two-shift layout (sections split by shift): it must be converted first.</param>
public sealed record ShiftSystemDto(string System, bool Legacy, IReadOnlyList<LegacyShiftDto> LegacyShifts, long? ShiftId, int ProfileVersion);

/// <param name="Main">The timing of the shift: morning (morning and double) or evening (evening).</param>
/// <param name="Evening">Double only: the evening session's start, lesson length and breaks (its lesson count is the main one).</param>
/// <param name="SessionDays">Double only: the session of every working day in each semester.</param>
public sealed record SaveShiftSystemCommand(
    string? System,
    IReadOnlyList<int>? Days,
    int? WeekStartDay,
    WizardShiftInput? Main,
    WizardShiftInput? Evening,
    IReadOnlyList<SessionDayInput>? SessionDays);

/// <param name="TargetSystem">morning, evening or dual.</param>
public sealed record ConvertLegacyCommand(string? TargetSystem);

public sealed record ConversionResultDto(string AutomaticBackupPath, ShiftSystemDto System);

/// <summary>
/// One place that owns the shift layout (owner decision, reverses DECISIONS #82): sections and teachers never differ by
/// shift. Every year has exactly one shift; «مزدوج» adds an evening session and a day→session mapping to it.
/// </summary>
public sealed class ShiftSystemService(IDataStore store, TimeProvider clock, TimetableStructureService structure, SessionPlanService sessions, IDatabaseBackup backup)
{
    public const string MorningName = "الدوام الصباحي";
    public const string EveningName = "الدوام المسائي";
    public const string DoubleName = "الدوام المزدوج";

    public async Task<ShiftSystemDto> GetAsync(CancellationToken token)
    {
        var profile = await ProfileAsync(token);
        var year = await SchoolContextService.CurrentYearAsync(store, token);
        if (year is null)
            return new ShiftSystemDto(ShiftSystems.Of(profile.StudyType), false, [], null, profile.Version);
        var shifts = await store.ListAsync(store.Read<Shift>().Where(shift => shift.AcademicYearId == year.Id).OrderBy(shift => shift.DisplayOrder), token);
        if (shifts.Count <= 1)
            return new ShiftSystemDto(ShiftSystems.Of(profile.StudyType), false, [], shifts.FirstOrDefault()?.Id, profile.Version);
        var ids = shifts.Select(shift => shift.Id).ToArray();
        var sections = await store.ListAsync(store.Read<Section>().Where(section => ids.Contains(section.ShiftId)), token);
        var legacy = shifts.Select(shift => new LegacyShiftDto(shift.Id, shift.Name, ApiText.ToValue(shift.Kind), sections.Count(section => section.ShiftId == shift.Id))).ToArray();
        return new ShiftSystemDto(ShiftSystems.Of(profile.StudyType), true, legacy, null, profile.Version);
    }

    public Task<OperationResult<ShiftSystemDto>> SaveAsync(SaveShiftSystemCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        return SetupTransaction.RunAsync(store, () => SaveInTransactionAsync(command, token), token);
    }

    /// <summary>The body of <see cref="SaveAsync"/> for callers that already run inside a transaction (the setup wizard's timing step).</summary>
    internal async Task<ShiftSystemDto> SaveInTransactionAsync(SaveShiftSystemCommand command, CancellationToken token)
    {
        if (ShiftSystems.Parse(command.System) is not { } mode)
            return SetupTransaction.Require(OperationResult.Invalid<ShiftSystemDto>(nameof(command.System), ErrorCodes.InvalidOption));
        if (await SchoolContextService.CurrentYearAsync(store, token) is not { } year)
            return SetupTransaction.Require(OperationResult.Failure<ShiftSystemDto>(ErrorCodes.NoCurrentYear));
        if (command.Days is not null)
        {
            var week = await structure.GetWorkingWeekAsync(token);
            SetupTransaction.Require(await structure.UpdateWorkingWeekAsync(new UpdateWorkingWeekCommand(command.Days, command.WeekStartDay ?? week.WeekStartDay, week.Version), token));
        }
        var shift = (await ApplyModeInTransactionAsync(mode, token))!;
        if (command.Main is { } main)
        {
            var periods = SetupTransaction.Require(TimetableStructureService.Generate(new GeneratePeriodsCommand(
                main.FirstStartTime, main.LessonMinutes, main.LessonCount, 0, null, main.Breaks ?? [], main.GapMinutes)));
            var saved = SetupTransaction.Require(await structure.ReplacePeriodsAsync(year.Id, shift.Id, new ReplacePeriodsCommand(
                periods.Periods.Select(period => new PeriodInput(period.Kind, period.StartTime, period.EndTime, period.StartBell, period.EndBell)).ToArray(), shift.Version), token));
            SetupTransaction.Require(await structure.SetDayLessonsAsync(year.Id, shift.Id, new SetDayLessonsCommand(main.DayLessons ?? [], saved.Version), token));
        }
        if (mode == StudyType.Dual)
        {
            var evening = command.Evening;
            var current = (await structure.ListShiftsAsync(year.Id, new ListQuery(null, null, 1, 100, null), token)).Items.Single();
            if (evening is null || command.SessionDays is null)
                return SetupTransaction.Require(OperationResult.Invalid<ShiftSystemDto>(nameof(command.Evening), ErrorCodes.Required));
            var eveningPeriods = SetupTransaction.Require(TimetableStructureService.Generate(new GeneratePeriodsCommand(
                evening.FirstStartTime, evening.LessonMinutes, current.LessonCount, 0, null, evening.Breaks ?? [], evening.GapMinutes)));
            var plan = SetupTransaction.Require(await sessions.GetAsync(token));
            SetupTransaction.Require(await sessions.SaveAsync(new SaveSessionPlanCommand(
                ApiText.ToValue(SessionSystem.TwoSessions),
                [new SessionTimingInput(ApiText.ToValue(SessionKind.Evening), eveningPeriods.Periods.Select(period => new PeriodInput(period.Kind, period.StartTime, period.EndTime, period.StartBell, period.EndBell)).ToArray())],
                command.SessionDays, plan.Version), token));
        }
        return await GetAsync(token);
    }

    /// <summary>
    /// Makes the year's single shift fit the system: creates it, or re-labels the one that exists (morning or double =
    /// the morning kind, evening = the evening kind), and leaves daily sessions only for «مزدوج». Refused while the old
    /// two-shift layout is still there (<see cref="ErrorCodes.ShiftSystemLegacy"/>).
    /// </summary>
    internal async Task<ShiftDto?> ApplyModeInTransactionAsync(StudyType mode, CancellationToken token)
    {
        var profile = await store.FirstOrDefaultAsync(store.Query<SchoolProfile>(), token)
            ?? throw new InvalidOperationException("The school profile row is created at database initialization.");
        profile.SetStudyType(mode, clock.GetUtcNow());
        if (await SchoolContextService.CurrentYearAsync(store, token) is not { } year)
        {
            await store.SaveChangesAsync(token);
            return null;
        }
        var shifts = await store.ListAsync(store.Query<Shift>().Where(item => item.AcademicYearId == year.Id), token);
        if (shifts.Count > 1)
            SetupTransaction.Require(OperationResult.Failure<ShiftDto>(ErrorCodes.ShiftSystemLegacy));
        var kind = mode == StudyType.Evening ? ShiftKind.Evening : ShiftKind.Morning;
        var name = mode switch { StudyType.Evening => EveningName, StudyType.Dual => DoubleName, _ => MorningName };
        var shift = shifts.SingleOrDefault();
        if (shift is null)
        {
            shift = Shift.Create(year.Id, name, 1, kind);
            store.Add(shift);
        }
        else
        {
            shift.SetKind(kind);
            if (IsStandardName(shift.NormalizedName))
                shift.Update(name, shift.DisplayOrder);
        }
        if (mode != StudyType.Dual && await SessionPlanService.ActiveAsync(store, year.Id, token) is not null)
        {
            var plan = SetupTransaction.Require(await sessions.GetAsync(token));
            await store.SaveChangesAsync(token);
            SetupTransaction.Require(await sessions.SaveAsync(new SaveSessionPlanCommand(ApiText.ToValue(SessionSystem.OneSession), [], [], plan.Version), token));
        }
        AuditTrail.Record(store, clock, AuditEvents.ShiftModeChanged, $"academic-year:{year.Id}", $"Shift system set to {mode}.", new { system = ShiftSystems.Of(mode) });
        await store.SaveChangesAsync(token);
        var week = await structure.GetWorkingWeekAsync(token);
        return TimetableStructureService.ToDto(shift, week.Days);
    }

    /// <summary>Sets only the system (no timings): used when the current year already exists in the wizard's school step.</summary>
    public Task<OperationResult<ShiftSystemDto>> SetModeAsync(string? system, CancellationToken token) =>
        SetupTransaction.RunAsync(store, async () =>
        {
            if (ShiftSystems.Parse(system) is not { } mode)
                return SetupTransaction.Require(OperationResult.Invalid<ShiftSystemDto>(nameof(system), ErrorCodes.InvalidOption));
            await ApplyModeInTransactionAsync(mode, token);
            return await GetAsync(token);
        }, token);

    /// <summary>
    /// Guided conversion of the old two-shift layout (one-time): an automatic backup first, then in each such year all sections
    /// move to one shift, the other shift goes (for «مزدوج» its timing becomes the evening session), and the school's
    /// system is set. Nothing is converted if the backup fails.
    /// </summary>
    public async Task<OperationResult<ConversionResultDto>> ConvertLegacyAsync(ConvertLegacyCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (ShiftSystems.Parse(command.TargetSystem) is not { } target)
            return OperationResult.Invalid<ConversionResultDto>(nameof(command.TargetSystem), ErrorCodes.InvalidOption);
        var years = await store.ListAsync(store.Read<AcademicYear>().OrderBy(year => year.Id), token);
        var multi = new List<AcademicYear>();
        foreach (var year in years)
        {
            if (await store.CountAsync(store.Read<Shift>().Where(shift => shift.AcademicYearId == year.Id), token) > 1)
                multi.Add(year);
        }
        if (multi.Count == 0)
            return OperationResult.Failure<ConversionResultDto>(ErrorCodes.Conflict);

        Directory.CreateDirectory(backup.AutomaticBackupFolder);
        var file = Path.Combine(backup.AutomaticBackupFolder, $"pre-conversion-{clock.GetUtcNow().ToLocalTime():yyyyMMdd-HHmmss}.db");
        if (File.Exists(file))
            return OperationResult.Failure<ConversionResultDto>(ErrorCodes.BackupFileExists);
        try
        {
            await backup.CreateAsync(file, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return OperationResult.Failure<ConversionResultDto>(ErrorCodes.BackupFailed);
        }

        var converted = await SetupTransaction.RunAsync(store, async () =>
        {
            foreach (var year in multi)
                await ConvertYearAsync(year, target, token);
            var profile = await store.FirstOrDefaultAsync(store.Query<SchoolProfile>(), token)
                ?? throw new InvalidOperationException("The school profile row is created at database initialization.");
            profile.SetStudyType(target, clock.GetUtcNow());
            AuditTrail.Record(store, clock, AuditEvents.ShiftSystemConverted, "school", $"The two-shift layout was converted to {target}.", new { system = ShiftSystems.Of(target) });
            await store.SaveChangesAsync(token);
            return await GetAsync(token);
        }, token);
        return converted.Succeeded ? OperationResult.Success(new ConversionResultDto(file, converted.Value!)) : converted.Cast<ConversionResultDto>();
    }

    private async Task ConvertYearAsync(AcademicYear year, StudyType target, CancellationToken token)
    {
        var shifts = await store.ListAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == year.Id).OrderBy(shift => shift.DisplayOrder), token);
        var wantEvening = target == StudyType.Evening;
        bool IsEvening(Shift shift) => shift.Kind == ShiftKind.Evening || shift.NormalizedName == ArabicText.Normalize(EveningName);
        var main = (wantEvening ? shifts.FirstOrDefault(IsEvening) : shifts.FirstOrDefault(shift => !IsEvening(shift))) ?? shifts[0];
        var others = shifts.Where(shift => shift.Id != main.Id).ToArray();
        var otherIds = others.Select(shift => shift.Id).ToArray();
        foreach (var section in await store.ListAsync(store.Query<Section>().Where(section => otherIds.Contains(section.ShiftId)), token))
            section.Update(main.Id, section.Label, section.StudentCount);
        await store.SaveChangesAsync(token);

        var week = await structure.GetWorkingWeekAsync(token);
        var eveningSource = others.FirstOrDefault(IsEvening) ?? others.FirstOrDefault();
        foreach (var other in others)
            store.Remove(other);
        main.SetKind(wantEvening ? ShiftKind.Evening : ShiftKind.Morning);
        main.Update(target switch { StudyType.Evening => EveningName, StudyType.Dual => DoubleName, _ => MorningName }, 1);
        await store.SaveChangesAsync(token);

        if (target == StudyType.Dual && eveningSource is not null && await store.FirstOrDefaultAsync(store.Query<SessionPlan>().Where(plan => plan.AcademicYearId == year.Id), token) is null)
        {
            var lessons = eveningSource.Periods.Where(period => period.Kind == PeriodKind.Lesson).OrderBy(period => period.Position).ToArray();
            var eveningLessons = Math.Max(1, main.LessonCount);
            var first = lessons.FirstOrDefault();
            var length = first is null ? 45 : Math.Max(1, (int)(first.EndTime - first.StartTime).TotalMinutes);
            var breaks = new List<BreakSlotDto>();
            for (var index = 0; index < lessons.Length - 1 && index < eveningLessons - 1; index++)
            {
                var gap = (int)(lessons[index + 1].StartTime - lessons[index].EndTime).TotalMinutes;
                if (gap > 0)
                    breaks.Add(new BreakSlotDto(index + 1, Math.Min(gap, 120)));
            }
            var start = first is null ? new TimeOnly(13, 0) : first.StartTime;
            var drafts = PeriodGenerator.Generate(new PeriodPlan(start, length, eveningLessons, breaks.Select(slot => new BreakSlot(slot.AfterLesson, slot.Minutes)).ToArray()));
            var half = (week.Days.Count + 1) / 2;
            var mapping = new List<SessionDay>();
            foreach (var (day, index) in week.Days.Select((day, index) => (day, index)))
            {
                mapping.Add(new SessionDay(1, day, index < half ? SessionKind.Morning : SessionKind.Evening));
                mapping.Add(new SessionDay(2, day, index < half ? SessionKind.Evening : SessionKind.Morning));
            }
            var plan = SessionPlan.Create(year.Id, main.Id);
            store.Add(plan);
            plan.Replace(SessionSystem.TwoSessions, new Dictionary<SessionKind, IReadOnlyList<PeriodDraft>> { [SessionKind.Evening] = drafts }, mapping, main.LessonCount, week.Days);
            await store.SaveChangesAsync(token);
        }
    }

    private static bool IsStandardName(string normalized) =>
        normalized == ArabicText.Normalize(MorningName) || normalized == ArabicText.Normalize(EveningName) || normalized == ArabicText.Normalize(DoubleName);

    private async Task<SchoolProfile> ProfileAsync(CancellationToken token) =>
        await store.FirstOrDefaultAsync(store.Read<SchoolProfile>(), token)
            ?? throw new InvalidOperationException("The school profile row is created at database initialization.");
}
