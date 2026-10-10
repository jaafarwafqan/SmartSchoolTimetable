using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Setup;

/// <param name="Status">
/// apply (a new suggestion), same (already as suggested), manual (the owner set the counts by hand; never overwritten),
/// aboveCapacity (the weekly total does not fit the shift's periods), belowDays (fewer lessons than working days),
/// noCurriculum (nothing planned yet).
/// </param>
/// <param name="Capacity">The most lessons a week the stage's shift(s) allow (all shifts must fit in a dual stage).</param>
/// <param name="ChangedSinceSuggestion">The counts came from an earlier suggestion and the curriculum total changed since.</param>
public sealed record DailySuggestionStageDto(
    long StageId,
    string StageName,
    int WeeklyTotal,
    int Capacity,
    IReadOnlyList<DayLessonsDto> Suggested,
    IReadOnlyList<DayLessonsDto> Current,
    string Status,
    bool ChangedSinceSuggestion,
    int Version);

public sealed record DailySuggestionDto(IReadOnlyList<DailySuggestionStageDto> Stages);

public sealed record ApplyDailySuggestionCommand(IReadOnlyList<long>? StageIds);

/// <summary>
/// "اقتراح توزيع الحصص اليومية" (ADR 0030): each stage's daily lessons derived from its weekly curriculum total,
/// spread evenly with the extra lessons on the earlier days, never above the shift on any day (in a dual-shift stage,
/// every shift of its sections must fit). Applied only to the stages the owner confirms; counts the owner set by hand
/// are never overwritten.
/// </summary>
public sealed class DailySuggestionService(IDataStore store, TimeProvider clock)
{
    private sealed record StagePlan(Stage Stage, DailySuggestionStageDto Dto, DistributionSuggestion Suggestion, Func<int, int> MaxOnDay, IReadOnlyList<int> Days);

    public async Task<DailySuggestionDto> PreviewAsync(long yearId, CancellationToken token) =>
        new((await PlanAsync(yearId, token)).Select(plan => plan.Dto).ToArray());

    public async Task<OperationResult<DailySuggestionDto>> ApplyAsync(long yearId, ApplyDailySuggestionCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var plans = await PlanAsync(yearId, token);
        var chosen = (command.StageIds ?? []).ToHashSet();
        if (chosen.Count == 0 || chosen.Any(id => plans.All(plan => plan.Stage.Id != id)))
            return OperationResult.Invalid<DailySuggestionDto>("StageIds", ErrorCodes.InvalidOption);
        var selected = plans.Where(plan => chosen.Contains(plan.Stage.Id)).ToArray();
        if (selected.Any(plan => plan.Dto.Status == "aboveCapacity"))
            return OperationResult.Failure<DailySuggestionDto>(ErrorCodes.DailyTotalAboveShift);
        foreach (var plan in selected.Where(plan => plan.Dto.Status == "apply"))
        {
            plan.Stage.ApplySuggestedDayLessons(plan.Suggestion.Days, plan.Days, plan.MaxOnDay);
            AuditTrail.Record(store, clock, AuditEvents.StageDayLessonsSuggested, $"stage:{plan.Stage.Id}", "Daily lessons set from the curriculum total.");
        }
        await store.SaveChangesAsync(token);
        return OperationResult.Success(await PreviewAsync(yearId, token));
    }

    private async Task<List<StagePlan>> PlanAsync(long yearId, CancellationToken token)
    {
        var stages = await store.ListAsync(store.Query<Stage>().Where(stage => stage.AcademicYearId == yearId && !stage.IsArchived)
            .OrderBy(stage => stage.DisplayOrder).ThenBy(stage => stage.NormalizedName), token);
        var stageIds = stages.Select(stage => stage.Id).ToArray();
        var entries = await store.ListAsync(store.Query<CurriculumEntry>().Where(entry => stageIds.Contains(entry.StageId) && !entry.IsArchived), token);
        var capacities = await StageCapacity.LoadAsync(store, yearId, stages, token);

        var plans = new List<StagePlan>();
        foreach (var stage in stages)
        {
            var (days, MaxOnDay) = capacities[stage.Id];
            var total = entries.Where(entry => entry.StageId == stage.Id).Sum(entry => entry.WeeklyLessons);
            var suggestion = DailyDistribution.Suggest(total, days, MaxOnDay);
            var own = stage.DayLessonCounts.ToDictionary(entry => entry.Day, entry => entry.Lessons);
            var current = days.Select(day => new DayLessonsDto(day, Math.Min(own.GetValueOrDefault(day, MaxOnDay(day)), MaxOnDay(day)))).ToArray();
            var suggested = suggestion.Days.Select(entry => new DayLessonsDto(entry.Day, entry.Lessons)).ToArray();
            var same = suggestion.Possible && current.SequenceEqual(suggested);
            var manual = own.Count > 0 && !stage.DayLessonsSuggested;
            var status = suggestion.Problem switch
            {
                DistributionProblem.NoCurriculum => "noCurriculum",
                DistributionProblem.AboveShiftCapacity => "aboveCapacity",
                DistributionProblem.BelowWorkingDays => "belowDays",
                _ => manual ? "manual" : same ? "same" : "apply",
            };
            var dto = new DailySuggestionStageDto(stage.Id, stage.Name, total, days.Sum(MaxOnDay), suggested, current, status,
                stage.DayLessonsSuggested && suggestion.Possible && !same, stage.Version);
            plans.Add(new StagePlan(stage, dto, suggestion, MaxOnDay, days));
        }
        return plans;
    }
}
