using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.SchoolSetup;

public sealed record SetShiftModeCommand(string? Mode, int Version);

public sealed record AffectedSectionDto(long SectionId, string StageName, string Label, string ShiftName, bool IsArchived);

/// <param name="ShiftsToCreate">Shift kinds (morning, evening) the mode adds.</param>
/// <param name="ShiftsToRemove">Names of shifts the mode removes (only possible when no section uses them).</param>
public sealed record ShiftModeImpactDto(
    string Mode,
    bool Allowed,
    IReadOnlyList<string> ShiftsToCreate,
    IReadOnlyList<string> ShiftsToRemove,
    IReadOnlyList<AffectedSectionDto> AffectedSections);

public sealed record ShiftModeDto(string Mode, IReadOnlyList<ShiftDto> Shifts, int ProfileVersion);

/// <summary>
/// Shift mode = the school's study type (spec 2.5 §3.2): morning only, evening only, or dual. Applying a mode
/// creates (or adopts by name) the morning/evening shifts of the current year and removes the ones it no longer
/// needs — refused while any section (active or archived) uses such a shift (DECISIONS_PENDING #20).
/// </summary>
public sealed class ShiftModeService(IDataStore store, TimeProvider clock)
{
    public const string MorningName = "الدوام الصباحي";
    public const string EveningName = "الدوام المسائي";

    private sealed record Plan(StudyType Mode, AcademicYear Year, IReadOnlyList<ShiftKind> Create, IReadOnlyList<Shift> Adopt, IReadOnlyList<Shift> Remove, IReadOnlyList<AffectedSectionDto> Affected);

    public async Task<OperationResult<ShiftModeImpactDto>> GetImpactAsync(string? mode, CancellationToken token)
    {
        var planned = await PlanAsync(mode, token);
        if (!planned.Succeeded)
            return planned.Cast<ShiftModeImpactDto>();
        var plan = planned.Value!;
        return OperationResult.Success(new ShiftModeImpactDto(
            ApiText.ToValue(plan.Mode),
            plan.Affected.Count == 0,
            plan.Create.Select(ApiText.ToValue).ToArray(),
            plan.Remove.Select(shift => shift.Name).ToArray(),
            plan.Affected));
    }

    public async Task<OperationResult<ShiftModeDto>> SetAsync(SetShiftModeCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var planned = await PlanAsync(command.Mode, token);
        if (!planned.Succeeded)
            return planned.Cast<ShiftModeDto>();
        var plan = planned.Value!;
        var profile = await store.FirstOrDefaultAsync(store.Query<SchoolProfile>(), token)
            ?? throw new InvalidOperationException("The school profile row is created at database initialization.");
        if (!profile.IsVersion(command.Version))
            return OperationResult.Failure<ShiftModeDto>(ErrorCodes.Conflict);
        if (plan.Affected.Count > 0)
            return OperationResult.Failure<ShiftModeDto>(ErrorCodes.ShiftModeInUse);
        // R3: the two-shift mode (separate sections per shift) cannot be combined with daily sessions of one shift.
        if (plan.Mode == StudyType.Dual && plan.Create.Count + plan.Adopt.Count > 0 && await SessionPlanService.ActiveAsync(store, plan.Year.Id, token) is not null)
            return OperationResult.Failure<ShiftModeDto>(ErrorCodes.SessionsNeedOneShift);

        profile.SetStudyType(plan.Mode, clock.GetUtcNow());
        foreach (var shift in plan.Adopt)
            shift.SetKind(shift.NormalizedName == ArabicText.Normalize(MorningName) ? ShiftKind.Morning : ShiftKind.Evening);
        foreach (var shift in plan.Remove)
            store.Remove(shift);
        foreach (var kind in plan.Create)
            store.Add(Shift.Create(plan.Year.Id, kind == ShiftKind.Morning ? MorningName : EveningName, kind == ShiftKind.Morning ? 1 : 2, kind));
        AuditTrail.Record(store, clock, "ShiftModeChanged", $"academic-year:{plan.Year.Id}", $"Shift mode set to {plan.Mode}.");
        var saved = await store.SaveAsync(() => true, "Mode", token);
        if (!saved.Succeeded)
            return saved.Cast<ShiftModeDto>();

        var week = await store.FirstOrDefaultAsync(store.Query<WorkingWeek>(), token) ?? WorkingWeek.CreateDefault();
        var shifts = await store.ListAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == plan.Year.Id).OrderBy(shift => shift.DisplayOrder), token);
        return OperationResult.Success(new ShiftModeDto(
            ApiText.ToValue(plan.Mode),
            shifts.Select(shift => TimetableStructureService.ToDto(shift, week.Days)).ToArray(),
            profile.Version));
    }

    private async Task<OperationResult<Plan>> PlanAsync(string? modeText, CancellationToken token)
    {
        var input = new InputErrors();
        var mode = input.Option<StudyType>(modeText, "Mode");
        if (input.Any)
            return input.ToResult<Plan>();
        if (await SchoolContextService.CurrentYearAsync(store, token) is not { } year)
            return OperationResult.Failure<Plan>(ErrorCodes.NoCurrentYear);

        var shifts = await store.ListAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == year.Id), token);
        ShiftKind[] needed = mode switch
        {
            StudyType.Morning => [ShiftKind.Morning],
            StudyType.Evening => [ShiftKind.Evening],
            _ => [ShiftKind.Morning, ShiftKind.Evening],
        };
        var create = new List<ShiftKind>();
        var adopt = new List<Shift>();
        foreach (var kind in needed)
        {
            if (shifts.Any(shift => shift.Kind == kind))
                continue;
            var standardName = ArabicText.Normalize(kind == ShiftKind.Morning ? MorningName : EveningName);
            if (shifts.FirstOrDefault(shift => shift.Kind == ShiftKind.Other && shift.NormalizedName == standardName) is { } sameName)
                adopt.Add(sameName);
            else
                create.Add(kind);
        }
        var remove = shifts.Where(shift => shift.Kind != ShiftKind.Other && !needed.Contains(shift.Kind)).ToArray();
        var removeIds = remove.Select(shift => shift.Id).ToArray();
        var sections = await store.ListAsync(store.Query<Section>().Where(section => removeIds.Contains(section.ShiftId)), token);
        var stageIds = sections.Select(section => section.StageId).Distinct().ToArray();
        var stages = (await store.ListAsync(store.Query<Stage>().Where(stage => stageIds.Contains(stage.Id)), token)).ToDictionary(stage => stage.Id);
        var affected = sections
            .Select(section => new AffectedSectionDto(section.Id, stages.GetValueOrDefault(section.StageId)?.Name ?? string.Empty, section.Label,
                remove.First(shift => shift.Id == section.ShiftId).Name, section.IsArchived))
            .OrderBy(section => section.StageName, StringComparer.Ordinal).ThenBy(section => section.Label, StringComparer.Ordinal)
            .ToArray();
        return OperationResult.Success(new Plan(mode, year, create, adopt, remove, affected));
    }
}
