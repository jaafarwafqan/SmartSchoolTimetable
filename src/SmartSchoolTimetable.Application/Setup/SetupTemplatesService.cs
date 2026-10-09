using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Setup;

public sealed record StageTemplateGradeInput(string? GradeKey, IReadOnlyList<string>? Branches, int Sections, long? ShiftId, string? LabelStyle);

public sealed record StageTemplateCommand(string? SchoolType, IReadOnlyList<StageTemplateGradeInput>? Grades);

/// <param name="Action">create or exists (kept as is).</param>
public sealed record StagePlanLineDto(string Key, string Name, string Action, int ExistingSections, int SectionsToAdd);

public sealed record StagePlanDto(IReadOnlyList<StagePlanLineDto> Lines, int Changes);

public sealed record OutOfTypeStageDto(long Id, string Name, int Version);

public sealed record SubjectTemplateCommand(IReadOnlyList<string>? Names);

/// <param name="Action">create or exists.</param>
public sealed record SubjectPlanLineDto(string Name, string Action);

public sealed record SubjectPlanDto(IReadOnlyList<SubjectPlanLineDto> Lines, int Changes);

/// <summary>
/// Applies the data-driven templates (spec 2.5 §4) with a preview. Idempotent (ADR 0022): existing stages and
/// subjects are kept untouched and skipped, sections are only added up to the requested count, nothing is deleted
/// or overwritten. Records are created through the normal services, so every rule and audit entry applies.
/// </summary>
public sealed class SetupTemplatesService(IDataStore store, StagesSectionsService stages, StageCardsService cards, SubjectsService subjects)
{
    private sealed record StagePlan(StagePlanLineDto Line, int DisplayOrder, long? ShiftId, string? LabelStyle, long? ExistingId);

    public static TemplateCatalogDto Catalog() => TemplateCatalog.Current.ToDto();

    public async Task<OperationResult<StagePlanDto>> StagesAsync(long yearId, StageTemplateCommand command, bool apply, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = new InputErrors();
        SchoolType? requestedSchoolType = command.SchoolType is null
            ? null
            : input.Option<SchoolType>(command.SchoolType, nameof(command.SchoolType));
        if (input.Any)
            return input.ToResult<StagePlanDto>();
        if (!await store.AnyAsync(store.Query<AcademicYear>().Where(year => year.Id == yearId), token))
            return OperationResult.Failure<StagePlanDto>(ErrorCodes.NotFound);
        var profile = await store.FirstOrDefaultAsync(store.Query<SchoolProfile>(), token);
        if (profile is null)
            return OperationResult.Failure<StagePlanDto>(ErrorCodes.SetupRequired);
        if (requestedSchoolType is { } requested && requested != profile.SchoolType)
            return OperationResult.Failure<StagePlanDto>(ErrorCodes.StageNotInSchoolType);

        var catalog = TemplateCatalog.Current;
        var grades = command.Grades ?? [];
        if (!AreStagesAllowed(catalog, profile.SchoolType, grades))
            return OperationResult.Failure<StagePlanDto>(ErrorCodes.StageNotInSchoolType);

        var plans = await PlanStagesAsync(yearId, profile.SchoolType, grades, token);
        var preview = new StagePlanDto(plans.Select(plan => plan.Line).ToArray(), plans.Count(plan => plan.Line.Action == "create" || plan.Line.SectionsToAdd > 0));
        if (!apply || preview.Changes == 0)
            return OperationResult.Success(preview);

        return await SetupTransaction.RunAsync(store, async () =>
        {
            foreach (var plan in plans)
            {
                var stageId = plan.ExistingId ?? SetupTransaction.Require(await stages.CreateStageAsync(yearId,
                    new SaveStageCommand(plan.Line.Name, plan.DisplayOrder, 0, plan.Line.Key), token)).Id;
                if (plan.Line.SectionsToAdd > 0)
                {
                    SetupTransaction.Require(await cards.SetSectionCountAsync(yearId, stageId,
                        new SetSectionCountCommand(plan.Line.ExistingSections + plan.Line.SectionsToAdd, plan.ShiftId ?? 0, plan.LabelStyle), token));
                }
            }
            return preview;
        }, token);
    }

    public async Task<OperationResult<IReadOnlyList<OutOfTypeStageDto>>> OutOfTypeStagesAsync(long yearId, CancellationToken token)
    {
        if (!await store.AnyAsync(store.Query<AcademicYear>().Where(year => year.Id == yearId), token))
            return OperationResult.Failure<IReadOnlyList<OutOfTypeStageDto>>(ErrorCodes.NotFound);
        var profile = await store.FirstOrDefaultAsync(store.Query<SchoolProfile>(), token);
        if (profile is null)
            return OperationResult.Failure<IReadOnlyList<OutOfTypeStageDto>>(ErrorCodes.SetupRequired);

        var allowedKeys = TemplateCatalog.Current.StageKeysFor(profile.SchoolType);
        var stages = await store.ListAsync(store.Query<Stage>()
            .Where(stage => stage.AcademicYearId == yearId && !stage.IsArchived && stage.TemplateKey != null), token);
        IReadOnlyList<OutOfTypeStageDto> result = stages
            .Where(stage => !allowedKeys.Contains(stage.TemplateKey!))
            .OrderBy(stage => stage.DisplayOrder)
            .ThenBy(stage => stage.Name)
            .Select(stage => new OutOfTypeStageDto(stage.Id, stage.Name, stage.Version))
            .ToArray();
        return OperationResult.Success(result);
    }

    public async Task<OperationResult<SubjectPlanDto>> SubjectsAsync(SubjectTemplateCommand command, bool apply, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var existing = (await store.ListAsync(store.Query<Subject>().Select(subject => subject.NormalizedName), token)).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = (command.Names ?? [])
            .Select(ArabicText.Clean)
            .Where(name => name.Length > 0 && seen.Add(ArabicText.Normalize(name)))
            .Select(name => new SubjectPlanLineDto(name, existing.Contains(ArabicText.Normalize(name)) ? "exists" : "create"))
            .ToArray();
        var plan = new SubjectPlanDto(lines, lines.Count(line => line.Action == "create"));
        if (!apply || plan.Changes == 0)
            return OperationResult.Success(plan);
        return await SetupTransaction.RunAsync(store, async () =>
        {
            foreach (var line in lines.Where(line => line.Action == "create"))
                SetupTransaction.Require(await subjects.CreateAsync(new SaveSubjectCommand(line.Name, 0, 0, true, false, false, false, null, null, 0), token));
            return plan;
        }, token);
    }

    /// <summary>Suggested subject names for the year's template stages (or the school type when none), in template order.</summary>
    public async Task<IReadOnlyList<string>> SuggestedSubjectsAsync(long yearId, CancellationToken token)
    {
        var catalog = TemplateCatalog.Current;
        var keys = (await store.ListAsync(store.Query<Stage>().Where(stage => stage.AcademicYearId == yearId && !stage.IsArchived && stage.TemplateKey != null)
            .OrderBy(stage => stage.DisplayOrder).Select(stage => stage.TemplateKey!), token)).ToList();
        if (keys.Count == 0 && await store.FirstOrDefaultAsync(store.Query<SchoolProfile>(), token) is { } profile)
            keys = catalog.GradesFor(profile.SchoolType).Select(grade => grade.Key).ToList();
        return keys.SelectMany(catalog.SubjectsFor).Distinct(StringComparer.Ordinal).ToArray();
    }

    private async Task<List<StagePlan>> PlanStagesAsync(long yearId, SchoolType schoolType, IReadOnlyList<StageTemplateGradeInput> grades, CancellationToken token)
    {
        var catalog = TemplateCatalog.Current;
        var ordered = catalog.GradesFor(schoolType).ToList();
        var yearStages = await store.ListAsync(store.Query<Stage>().Where(stage => stage.AcademicYearId == yearId), token);
        var stageIds = yearStages.Select(stage => stage.Id).ToArray();
        var sections = await store.ListAsync(store.Query<Section>().Where(section => stageIds.Contains(section.StageId) && !section.IsArchived), token);
        var plans = new List<StagePlan>();
        foreach (var input in grades)
        {
            var grade = catalog.Grade(input.GradeKey);
            var position = grade is null ? -1 : ordered.IndexOf(grade);
            if (grade is null || position < 0)
            {
                plans.Add(new StagePlan(new StagePlanLineDto(input.GradeKey ?? string.Empty, grade?.Name ?? string.Empty, "notApplicable", 0, 0), 0, null, null, null));
                continue;
            }
            var branches = grade.BranchStem is null || input.Branches is null || input.Branches.Count == 0
                ? [null]
                : input.Branches.Select(key => catalog.Branch(key)).ToArray();
            for (var index = 0; index < branches.Length; index++)
            {
                var (key, name) = TemplateCatalog.Stage(grade, branches[index]);
                if (branches[index] is null && input.Branches is { Count: > 0 } && grade.BranchStem is not null)
                {
                    plans.Add(new StagePlan(new StagePlanLineDto(key, name, "notApplicable", 0, 0), 0, null, null, null));
                    continue;
                }
                var existing = yearStages.FirstOrDefault(stage => stage.TemplateKey == key || stage.NormalizedName == ArabicText.Normalize(name));
                var have = existing is null ? 0 : sections.Count(section => section.StageId == existing.Id);
                var toAdd = existing?.IsArchived == true ? 0 : Math.Max(0, Math.Min(input.Sections, StageCardsService.MaxSections) - have);
                plans.Add(new StagePlan(new StagePlanLineDto(key, existing?.Name ?? name, existing is null ? "create" : "exists", have, toAdd),
                    (position + 1) * 10 + index, input.ShiftId, input.LabelStyle, existing?.Id));
            }
        }
        return plans;
    }

    private static bool AreStagesAllowed(
        TemplateCatalog catalog,
        SchoolType schoolType,
        IReadOnlyList<StageTemplateGradeInput> grades)
    {
        var allowedGrades = catalog.GradesFor(schoolType).ToDictionary(grade => grade.Key, StringComparer.Ordinal);
        var seenGrades = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in grades)
        {
            if (input.GradeKey is not { } key || !allowedGrades.TryGetValue(key, out var grade) || !seenGrades.Add(key))
                return false;

            var branches = input.Branches ?? [];
            if (grade.BranchStem is null)
            {
                if (branches.Count > 0)
                    return false;
                continue;
            }

            if (branches.Count == 0 || branches.Distinct(StringComparer.Ordinal).Count() != branches.Count ||
                branches.Any(branchKey => catalog.Branch(branchKey) is null))
                return false;
        }
        return true;
    }
}
