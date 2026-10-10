using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Curriculum;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Setup;

public sealed record WizardSchoolCommand(string? Name, string? SchoolType, string? ShiftMode, string? PrincipalName);

public sealed record WizardTermInput(string? Name, string? StartDate, string? EndDate);

public sealed record WizardYearCommand(string? Label, string? StartDate, string? EndDate, IReadOnlyList<WizardTermInput>? Terms);

/// <param name="Kind">morning or evening; one block per shift of the chosen shift mode.</param>
public sealed record WizardShiftInput(
    string? Kind,
    string? FirstStartTime,
    int LessonMinutes,
    int LessonCount,
    IReadOnlyList<BreakSlotDto>? Breaks,
    IReadOnlyList<DayLessonsDto>? DayLessons,
    int GapMinutes = 0);

public sealed record WizardTimingCommand(IReadOnlyList<int>? Days, int WeekStartDay, IReadOnlyList<WizardShiftInput>? Shifts);

/// <param name="Code">emptyCurriculum, noSections, under or over (the UI writes the Arabic text).</param>
public sealed record SetupWarningDto(string Code, string StageName, string? ShiftName, int Value);

public sealed record SetupReviewDto(
    string SchoolName,
    string? YearLabel,
    int Shifts,
    int Stages,
    int Sections,
    int Subjects,
    int CurriculumLines,
    int Teachers,
    IReadOnlyList<SetupWarningDto> Warnings);

/// <summary>
/// The setup wizard's server steps (spec 2.5 §5, ADR 0023). Steps 1–3 combine several services, so each runs as one
/// transaction (`SetupTransaction`) and records the step in the setup progress inside it. Every write goes through
/// the normal services (their validation, version checks and audit entries); running a step again with the same
/// input leaves the data as it is. Steps 4–6 use the template, curriculum and teacher endpoints directly.
/// </summary>
public sealed class SetupWizardService(
    IDataStore store,
    SchoolProfileService profiles,
    AcademicYearService years,
    TimetableStructureService structure,
    ShiftModeService shiftMode,
    SetupProgressService progress)
{
    public const int SchoolStep = 1;
    public const int YearStep = 2;
    public const int TimingStep = 3;
    public const int ReviewStep = 8;

    public Task<OperationResult<SetupProgressDto>> SaveSchoolAsync(WizardSchoolCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        return SetupTransaction.RunAsync(store, async () =>
        {
            var profile = await profiles.GetAsync(token);
            var saved = SetupTransaction.Require(await profiles.UpdateAsync(new UpdateSchoolProfileCommand(
                command.Name, command.SchoolType, command.ShiftMode, command.PrincipalName, profile.ScheduleOfficerName,
                profile.TimeZone, profile.NumeralSystem, profile.CalendarDisplay, profile.Version), token));
            // With a current year, the shifts follow the chosen mode now; otherwise the timing step creates them.
            if (await SchoolContextService.CurrentYearAsync(store, token) is not null)
                SetupTransaction.Require(await shiftMode.SetAsync(new SetShiftModeCommand(saved.StudyType, saved.Version), token));
            return await CompleteAsync(SchoolStep, token);
        }, token);
    }

    public Task<OperationResult<SetupProgressDto>> SaveYearAsync(WizardYearCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        return SetupTransaction.RunAsync(store, async () =>
        {
            var label = ArabicText.Normalize(command.Label);
            var existing = (await store.ListAsync(store.Query<AcademicYear>(), token)).FirstOrDefault(year => year.NormalizedLabel == label);
            var year = existing is null
                ? SetupTransaction.Require(await years.CreateAsync(new SaveAcademicYearCommand(command.Label, command.StartDate, command.EndDate, 0), token))
                : SetupTransaction.Require(await years.UpdateAsync(existing.Id, new SaveAcademicYearCommand(command.Label, command.StartDate, command.EndDate, existing.Version), token));
            if (!year.IsCurrent)
                year = SetupTransaction.Require(await years.SetCurrentAsync(year.Id, year.Version, token));
            foreach (var term in command.Terms ?? [])
            {
                var match = year.Terms.FirstOrDefault(item => ArabicText.Normalize(item.Name) == ArabicText.Normalize(term.Name));
                var save = new SaveTermCommand(term.Name, term.StartDate, term.EndDate, year.Version);
                year = SetupTransaction.Require(match is null
                    ? await years.AddTermAsync(year.Id, save, token)
                    : await years.UpdateTermAsync(year.Id, match.Id, save, token));
            }
            if (year.Terms.Count > 0 && !year.Terms.Any(term => term.IsCurrent))
                SetupTransaction.Require(await years.SetCurrentTermAsync(year.Id, year.Terms[0].Id, year.Version, token));
            return await CompleteAsync(YearStep, token);
        }, token);
    }

    public Task<OperationResult<SetupProgressDto>> SaveTimingAsync(WizardTimingCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        return SetupTransaction.RunAsync(store, async () =>
        {
            if (await SchoolContextService.CurrentYearAsync(store, token) is not { } year)
                return SetupTransaction.Require(OperationResult.Failure<SetupProgressDto>(ErrorCodes.NoCurrentYear));
            var week = await structure.GetWorkingWeekAsync(token);
            SetupTransaction.Require(await structure.UpdateWorkingWeekAsync(new UpdateWorkingWeekCommand(command.Days, command.WeekStartDay, week.Version), token));
            var profile = await profiles.GetAsync(token);
            var shifts = SetupTransaction.Require(await shiftMode.SetAsync(new SetShiftModeCommand(profile.StudyType, profile.Version), token)).Shifts;

            var inputs = command.Shifts ?? [];
            if (inputs.Select(input => input.Kind).Distinct().Count() != inputs.Count || inputs.Any(input => shifts.All(shift => shift.Kind != input.Kind)))
                return SetupTransaction.Require(OperationResult.Invalid<SetupProgressDto>("Shifts", ErrorCodes.InvalidOption));
            foreach (var input in inputs)
            {
                var shift = shifts.Single(item => item.Kind == input.Kind);
                var periods = SetupTransaction.Require(TimetableStructureService.Generate(new GeneratePeriodsCommand(
                    input.FirstStartTime, input.LessonMinutes, input.LessonCount, 0, null, input.Breaks ?? [], input.GapMinutes)));
                shift = SetupTransaction.Require(await structure.ReplacePeriodsAsync(year.Id, shift.Id, new ReplacePeriodsCommand(
                    periods.Periods.Select(period => new PeriodInput(period.Kind, period.StartTime, period.EndTime, period.StartBell, period.EndBell)).ToArray(),
                    shift.Version), token));
                SetupTransaction.Require(await structure.SetDayLessonsAsync(year.Id, shift.Id, new SetDayLessonsCommand(input.DayLessons ?? [], shift.Version), token));
            }
            return await CompleteAsync(TimingStep, token);
        }, token);
    }

    /// <summary>Step 7: real counts of the current year and what still needs attention (nothing is blocked).</summary>
    public async Task<SetupReviewDto> ReviewAsync(CancellationToken token)
    {
        var profile = await store.FirstOrDefaultAsync(store.Query<SchoolProfile>(), token);
        var year = await SchoolContextService.CurrentYearAsync(store, token);
        var yearId = year?.Id ?? 0;
        var table = await CurriculumTableBuilder.BuildAsync(store, yearId, token);
        var stageIds = table.Stages.Select(stage => stage.Id).ToArray();
        var warnings = new List<SetupWarningDto>();
        foreach (var stage in table.Stages)
        {
            if (stage.Totals.Count == 0)
                warnings.Add(new SetupWarningDto("noSections", stage.Name, null, 0));
            if (stage.PlannedLessons == 0)
                warnings.Add(new SetupWarningDto("emptyCurriculum", stage.Name, null, 0));
            else
                warnings.AddRange(stage.Totals.Where(total => total.Status != "equal")
                    .Select(total => new SetupWarningDto(total.Status, stage.Name, total.ShiftName, Math.Abs(total.Difference))));
        }
        return new SetupReviewDto(
            profile?.Name ?? string.Empty,
            year?.Label,
            await store.CountAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == yearId), token),
            stageIds.Length,
            await store.CountAsync(store.Query<Section>().Where(section => stageIds.Contains(section.StageId) && !section.IsArchived), token),
            await store.CountAsync(store.Query<Subject>().Where(subject => !subject.IsArchived), token),
            await store.CountAsync(store.Query<CurriculumEntry>().Where(entry => stageIds.Contains(entry.StageId) && !entry.IsArchived), token),
            await store.CountAsync(store.Query<Teacher>().Where(teacher => !teacher.IsArchived), token),
            warnings);
    }

    /// <summary>Marks the step done and moves to the next one (inside the step's transaction).</summary>
    private async Task<SetupProgressDto> CompleteAsync(int step, CancellationToken token)
    {
        var current = await progress.GetAsync(token);
        var completed = current.CompletedSteps.Append(step).Distinct().Order().ToArray();
        return SetupTransaction.Require(await progress.SaveAsync(new SaveSetupProgressCommand(
            Math.Max(current.CurrentStep, step + 1), completed, current.SkippedSteps, current.IsFinished, current.Version), token));
    }
}
