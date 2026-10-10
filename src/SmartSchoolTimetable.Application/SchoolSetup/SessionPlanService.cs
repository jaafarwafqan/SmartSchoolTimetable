using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.SchoolSetup;

/// <summary>The timing of one daily session; morning is the structural shift's own periods (read-only here).</summary>
public sealed record SessionTimingDto(string Session, IReadOnlyList<LessonPeriodDto> Periods);

public sealed record SessionDayDto(int Term, int Day, string Session);

/// <param name="System">oneSession (default), twoSessions (دوام مزدوج) or threeSessions (reserved, «قريباً»).</param>
/// <param name="ShiftId">The year's single shift the sessions belong to; null when the year has no shift or several.</param>
/// <param name="Available">False when the year does not have exactly one shift (the old two-shift mode, or no shift yet).</param>
/// <param name="LessonCount">Lessons per day every session must have.</param>
public sealed record SessionPlanDto(
    string System,
    long? ShiftId,
    bool Available,
    int LessonCount,
    IReadOnlyList<int> WorkingDays,
    IReadOnlyList<SessionTimingDto> Timings,
    IReadOnlyList<SessionDayDto> Days,
    int Version);

public sealed record SessionTimingInput(string? Session, IReadOnlyList<PeriodInput>? Periods);

public sealed record SessionDayInput(int Term, int Day, string? Session);

/// <param name="Timings">The sessions other than morning (ignored for oneSession).</param>
/// <param name="Version">The plan's version; 0 when the year has none yet.</param>
public sealed record SaveSessionPlanCommand(string? System, IReadOnlyList<SessionTimingInput>? Timings, IReadOnlyList<SessionDayInput>? Days, int Version);

/// <summary>
/// «نظام الدوام اليومي» (R3): one school, one set of sections and one timetable; each working day falls in a daily
/// session (صباحي / مسائي) per semester, and only the clock times change. The structural shift's periods are the
/// morning timing; the other sessions keep their own start, lengths and breaks with the same number of lessons.
/// </summary>
public sealed class SessionPlanService(IDataStore store, TimeProvider clock)
{
    public async Task<OperationResult<SessionPlanDto>> GetAsync(CancellationToken token)
    {
        if (await SchoolContextService.CurrentYearAsync(store, token) is not { } year)
            return OperationResult.Failure<SessionPlanDto>(ErrorCodes.NoCurrentYear);
        var (shift, plan, days) = await LoadAsync(year.Id, token);
        return OperationResult.Success(ToDto(shift, plan, days));
    }

    public async Task<OperationResult<SessionPlanDto>> SaveAsync(SaveSessionPlanCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await SchoolContextService.CurrentYearAsync(store, token) is not { } year)
            return OperationResult.Failure<SessionPlanDto>(ErrorCodes.NoCurrentYear);
        var (shift, plan, days) = await LoadAsync(year.Id, token);
        if ((plan?.Version ?? 0) != command.Version)
            return OperationResult.Failure<SessionPlanDto>(ErrorCodes.Conflict);

        var input = new InputErrors();
        var system = input.Option<SessionSystem>(command.System, nameof(command.System));
        var timings = new Dictionary<SessionKind, IReadOnlyList<PeriodDraft>>();
        var mapping = new List<SessionDay>();
        if (system != SessionSystem.OneSession && !input.Any)
        {
            foreach (var (timing, index) in (command.Timings ?? []).Select((timing, index) => (timing, index)))
            {
                var kind = input.Option<SessionKind>(timing.Session, $"Timings[{index}].Session");
                var drafts = new List<PeriodDraft>();
                for (var row = 0; row < (timing.Periods?.Count ?? 0); row++)
                {
                    var item = timing.Periods![row];
                    var period = input.Option<PeriodKind>(item.Kind, $"Timings[{index}].Periods[{row}].Kind");
                    var start = input.Time(item.StartTime, $"Timings[{index}].Periods[{row}].StartTime");
                    var end = input.Time(item.EndTime, $"Timings[{index}].Periods[{row}].EndTime");
                    drafts.Add(new PeriodDraft(period, start, end));
                }
                if (!timings.TryAdd(kind, drafts))
                    input.Add($"Timings[{index}].Session", ErrorCodes.DuplicateName);
            }
            foreach (var (day, index) in (command.Days ?? []).Select((day, index) => (day, index)))
                mapping.Add(new SessionDay(day.Term, day.Day, input.Option<SessionKind>(day.Session, $"Days[{index}].Session")));
        }
        if (input.Any)
            return input.ToResult<SessionPlanDto>();
        // The sessions share ONE timetable grid, so they need the year's single shift (not the old two-shift mode).
        if (system != SessionSystem.OneSession && shift is null)
            return OperationResult.Failure<SessionPlanDto>(ErrorCodes.SessionsNeedOneShift);
        if (system == SessionSystem.OneSession && plan is null)
            return OperationResult.Success(ToDto(shift, plan, days));

        if (plan is null)
        {
            plan = SessionPlan.Create(year.Id, shift!.Id);
            store.Add(plan);
        }
        var lessonCount = shift?.LessonCount ?? 0;
        if (StoreSaving.TryDomain<SessionPlanDto>(() => plan.Replace(system, timings, mapping, lessonCount, days)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, "SessionPlanUpdated", $"academic-year:{year.Id}", $"Daily sessions set to {system}.");
        var saved = plan;
        return await store.SaveAsync(() => ToDto(shift, saved, days), nameof(command.System), token);
    }

    /// <summary>True when the year uses several daily sessions (their lesson count is then fixed to the shift's).</summary>
    public static async Task<SessionPlan?> ActiveAsync(IDataStore store, long yearId, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(store);
        var plan = await store.FirstOrDefaultAsync(store.Query<SessionPlan>().Where(item => item.AcademicYearId == yearId), token);
        return plan is { System: not SessionSystem.OneSession } ? plan : null;
    }

    private async Task<(Shift? Shift, SessionPlan? Plan, IReadOnlyList<int> Days)> LoadAsync(long yearId, CancellationToken token)
    {
        var shifts = await store.ListAsync(store.Query<Shift>().Where(item => item.AcademicYearId == yearId), token);
        var plan = await store.FirstOrDefaultAsync(store.Query<SessionPlan>().Where(item => item.AcademicYearId == yearId), token);
        var week = await store.FirstOrDefaultAsync(store.Query<WorkingWeek>(), token) ?? WorkingWeek.CreateDefault();
        return (shifts.Count == 1 ? shifts[0] : null, plan, week.Days);
    }

    private static SessionPlanDto ToDto(Shift? shift, SessionPlan? plan, IReadOnlyList<int> days)
    {
        var system = plan?.System ?? SessionSystem.OneSession;
        var timings = new List<SessionTimingDto>();
        if (shift is not null)
            timings.Add(new SessionTimingDto(ApiText.ToValue(SessionKind.Morning), shift.Periods.Select(ToDto).ToArray()));
        foreach (var group in (plan?.Periods ?? []).GroupBy(period => period.Session))
            timings.Add(new SessionTimingDto(ApiText.ToValue(group.Key), group.OrderBy(period => period.Position).Select(ToDto).ToArray()));
        return new SessionPlanDto(
            ApiText.ToValue(system),
            shift?.Id,
            shift is not null,
            shift?.LessonCount ?? 0,
            days,
            timings,
            (plan?.Days ?? []).Where(day => days.Contains(day.Day)).Select(day => new SessionDayDto(day.Term, day.Day, ApiText.ToValue(day.Session))).ToArray(),
            plan?.Version ?? 0);
    }

    private static LessonPeriodDto ToDto(LessonPeriod value) =>
        new(value.Position, ApiText.ToValue(value.Kind), InputParsing.Format(value.StartTime), InputParsing.Format(value.EndTime), value.StartBell, value.EndBell);

    private static LessonPeriodDto ToDto(SessionPeriod value) =>
        new(value.Position, ApiText.ToValue(value.Kind), InputParsing.Format(value.StartTime), InputParsing.Format(value.EndTime), value.Kind == PeriodKind.Lesson, value.Kind == PeriodKind.Lesson);
}
