using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;

namespace SmartSchoolTimetable.Application.Curriculum;

/// <summary>
/// Typing savers (spec 2.5 §4.2), each with a preview: copy one stage's curriculum to other stages (creates only
/// lines the target lacks; never overwrites or deletes), and set the same weekly lessons for one subject line
/// across chosen stages. Running either twice changes nothing the second time.
/// </summary>
public sealed class CurriculumHelpersService(IDataStore store, TimeProvider clock)
{
    private sealed record Context(IReadOnlyDictionary<long, Stage> Stages, IReadOnlyDictionary<long, Subject> Subjects, IReadOnlyList<CurriculumEntry> Entries);

    public async Task<OperationResult<CurriculumPlanDto>> CopyAsync(long yearId, CopyCurriculumCommand command, bool apply, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var context = await LoadAsync(yearId, token);
        if (!context.Stages.TryGetValue(command.FromStageId, out var source))
            return OperationResult.Failure<CurriculumPlanDto>(ErrorCodes.NotFound);
        var lines = new List<CurriculumPlanLineDto>();
        foreach (var targetId in (command.ToStageIds ?? []).Distinct())
        {
            var target = context.Stages.GetValueOrDefault(targetId);
            foreach (var entry in context.Entries.Where(entry => entry.StageId == source.Id))
            {
                // Lines of an archived subject are not copied.
                if (!context.Subjects.TryGetValue(entry.SubjectId, out var subject))
                    continue;
                var action = target is null || target.Id == source.Id || target.IsArchived ? "notApplicable"
                    : context.Entries.Any(existing => existing.StageId == target.Id && existing.SameLineAs(entry.SubjectId, entry.Label)) ? "exists"
                    : "create";
                lines.Add(new CurriculumPlanLineDto(targetId, target?.Name ?? string.Empty, subject.Id, subject.Name, entry.Label, entry.WeeklyLessons, action));
                if (apply && action == "create")
                    store.Add(entry.CopyTo(target!.Id));
            }
        }
        return await FinishAsync(lines, apply, "CurriculumCopied", $"stage:{source.Id}", token);
    }

    public async Task<OperationResult<CurriculumPlanDto>> SetAcrossAsync(long yearId, SetLessonsAcrossCommand command, bool apply, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.WeeklyLessons is < CurriculumEntry.MinWeeklyLessons or > CurriculumEntry.MaxWeeklyLessons)
            return OperationResult.Invalid<CurriculumPlanDto>("WeeklyLessons", ErrorCodes.ValueOutOfRange);
        var context = await LoadAsync(yearId, token);
        if (!context.Subjects.TryGetValue(command.SubjectId, out var subject))
            return OperationResult.Invalid<CurriculumPlanDto>("SubjectId", ErrorCodes.InvalidOption);
        var lines = new List<CurriculumPlanLineDto>();
        foreach (var stageId in (command.StageIds ?? []).Distinct())
        {
            var stage = context.Stages.GetValueOrDefault(stageId);
            var matches = stage is null ? [] : context.Entries.Where(entry => entry.StageId == stage.Id && entry.SameLineAs(subject.Id, command.Label)).ToArray();
            var action = stage is null || stage.IsArchived ? "notApplicable"
                : matches.Length > 1 ? "ambiguous"
                : matches.Length == 0 ? "create"
                : matches[0].WeeklyLessons == command.WeeklyLessons ? "unchanged" : "update";
            lines.Add(new CurriculumPlanLineDto(stageId, stage?.Name ?? string.Empty, subject.Id, subject.Name, command.Label, command.WeeklyLessons, action));
            if (!apply)
                continue;
            if (action == "create")
                store.Add(CurriculumEntry.Create(stage!.Id, subject.Id, command.WeeklyLessons, command.Label, subject.RequiresDoublePeriod, null));
            else if (action == "update")
                matches[0].SetWeeklyLessons(command.WeeklyLessons);
        }
        return await FinishAsync(lines, apply, "CurriculumLessonsSet", $"subject:{subject.Id}", token);
    }

    private async Task<OperationResult<CurriculumPlanDto>> FinishAsync(List<CurriculumPlanLineDto> lines, bool apply, string eventType, string target, CancellationToken token)
    {
        var changes = lines.Count(line => line.Action is "create" or "update");
        var plan = new CurriculumPlanDto(lines, changes);
        if (!apply || changes == 0)
            return OperationResult.Success(plan);
        AuditTrail.Record(store, clock, eventType, target, $"{changes} curriculum lines changed.");
        return await store.SaveAsync(() => plan, "WeeklyLessons", token);
    }

    private async Task<Context> LoadAsync(long yearId, CancellationToken token)
    {
        var stages = await store.ListAsync(store.Query<Stage>().Where(stage => stage.AcademicYearId == yearId), token);
        var stageIds = stages.Select(stage => stage.Id).ToArray();
        var subjects = await store.ListAsync(store.Query<Subject>().Where(subject => !subject.IsArchived), token);
        var entries = await store.ListAsync(store.Query<CurriculumEntry>().Where(entry => stageIds.Contains(entry.StageId) && !entry.IsArchived), token);
        return new Context(stages.ToDictionary(stage => stage.Id), subjects.ToDictionary(subject => subject.Id), entries);
    }
}
