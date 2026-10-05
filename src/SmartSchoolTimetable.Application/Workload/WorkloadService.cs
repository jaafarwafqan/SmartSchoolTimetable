using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Domain.Workload;

namespace SmartSchoolTimetable.Application.Workload;

/// <summary>
/// «الأنصبة» (Phase 3 §2.3, §4): who teaches each curriculum line in each section. One active assignment per
/// (section, line); lessons come from the line. Bulk actions are previewed first and never overwrite an existing
/// teacher unless the owner chose to; applying a plan writes exactly what its preview showed.
/// </summary>
public sealed class WorkloadService(IDataStore store, TimeProvider clock)
{
    public const string Create = "create";
    public const string Replace = "replace";
    public const string Skip = "skip";
    public const string Unchanged = "unchanged";
    public const string Transfer = "transfer";
    public const string Remove = "remove";

    private static readonly string[] ChangingActions = [Create, Replace, Transfer, Remove];

    public async Task<OperationResult<WorkloadMatrixDto>> GetMatrixAsync(long yearId, long? stageId, CancellationToken token)
    {
        if (await WorkloadData.LoadAsync(store, yearId, forUpdate: false, token) is not { } data)
            return OperationResult.Failure<WorkloadMatrixDto>(ErrorCodes.NotFound);
        var summaries = data.Stages.Select(stage =>
        {
            var lines = data.EntriesOf(stage.Id).Select(entry => entry.Id).ToHashSet();
            var sections = data.SectionsOf(stage.Id).ToArray();
            var assigned = data.Assignments.Count(row => lines.Contains(row.CurriculumEntryId) && sections.Any(section => section.Id == row.SectionId));
            return new WorkloadStageSummaryDto(stage.Id, stage.Name, assigned, lines.Count * sections.Length);
        }).ToArray();
        var chosen = stageId is { } id ? data.Stages.FirstOrDefault(stage => stage.Id == id) : data.Stages.Count > 0 ? data.Stages[0] : null;
        if (stageId is not null && chosen is null)
            return OperationResult.Failure<WorkloadMatrixDto>(ErrorCodes.NotFound);
        return OperationResult.Success(new WorkloadMatrixDto(summaries, chosen is null ? null : StageMatrix(data, chosen)));
    }

    public async Task<OperationResult<IReadOnlyList<TeacherLoadDto>>> GetTeacherLoadsAsync(long yearId, CancellationToken token)
    {
        if (await WorkloadData.LoadAsync(store, yearId, forUpdate: false, token) is not { } data)
            return OperationResult.Failure<IReadOnlyList<TeacherLoadDto>>(ErrorCodes.NotFound);
        return OperationResult.Success<IReadOnlyList<TeacherLoadDto>>(data.Teachers.Values
            .Where(teacher => !teacher.IsArchived)
            .OrderBy(teacher => teacher.NormalizedFullName, StringComparer.Ordinal)
            .Select(teacher => LoadOf(data, teacher))
            .ToArray());
    }

    public async Task<OperationResult<WorkloadStageDto>> SetCellAsync(long yearId, SetWorkloadCellCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await WorkloadData.LoadAsync(store, yearId, forUpdate: true, token) is not { } data)
            return OperationResult.Failure<WorkloadStageDto>(ErrorCodes.NotFound);
        if (data.Section(command.SectionId) is not { } section)
            return OperationResult.Invalid<WorkloadStageDto>(nameof(command.SectionId), ErrorCodes.InvalidOption);
        if (data.Entry(command.EntryId) is not { } entry || entry.StageId != section.StageId)
            return OperationResult.Invalid<WorkloadStageDto>(nameof(command.EntryId), ErrorCodes.InvalidOption);
        if (command.TeacherId is { } teacherId && !ActiveTeacher(data, teacherId))
            return OperationResult.Invalid<WorkloadStageDto>(nameof(command.TeacherId), ErrorCodes.InvalidOption);

        var current = data.Assignment(section.Id, entry.Id);
        if (command.AssignmentId is { } assignmentId)
        {
            if (current is null || current.Id != assignmentId || !current.IsVersion(command.Version ?? 0))
                return OperationResult.Failure<WorkloadStageDto>(ErrorCodes.Conflict);
            if (command.TeacherId is { } next)
            {
                if (current.Reassign(next))
                    AuditTrail.Record(store, clock, "WorkloadReassigned", $"workload:{current.Id}", "Assignment given to another teacher.");
            }
            else
            {
                current.Archive(clock.GetUtcNow());
                AuditTrail.Record(store, clock, "WorkloadCleared", $"workload:{current.Id}", "Assignment cleared.");
            }
        }
        else if (command.TeacherId is { } teacher)
        {
            // Someone assigned the cell after it was read: the owner reloads instead of overwriting it.
            if (current is not null)
                return OperationResult.Failure<WorkloadStageDto>(ErrorCodes.Conflict);
            store.Add(WorkloadAssignment.Create(section.Id, entry.Id, teacher));
            AuditTrail.Record(store, clock, "WorkloadAssigned", $"section:{section.Id}", "Line assigned to a teacher.");
        }
        var stageId = section.StageId;
        return await store.SaveAsync(async ct => StageMatrix((await WorkloadData.LoadAsync(store, yearId, forUpdate: false, ct))!, data.Stages.First(stage => stage.Id == stageId)), "TeacherId", token);
    }

    public Task<OperationResult<WorkloadPlanDto>> AssignAcrossStageAsync(long yearId, AssignAcrossStageCommand command, bool apply, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PlanAsync(yearId, apply, "WorkloadAssignedAcrossStage", data =>
        {
            if (!ActiveTeacher(data, command.TeacherId))
                return OperationResult.Invalid<IReadOnlyList<Change>>(nameof(command.TeacherId), ErrorCodes.InvalidOption);
            if (data.Entry(command.EntryId) is not { } entry)
                return OperationResult.Invalid<IReadOnlyList<Change>>(nameof(command.EntryId), ErrorCodes.InvalidOption);
            return OperationResult.Success<IReadOnlyList<Change>>(data.SectionsOf(entry.StageId)
                .Select(section => Assign(data, section, entry, command.TeacherId, command.Overwrite)).ToArray());
        }, token);
    }

    public Task<OperationResult<WorkloadPlanDto>> ClassTeacherAsync(long yearId, ClassTeacherCommand command, bool apply, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PlanAsync(yearId, apply, "WorkloadClassTeacher", data =>
        {
            if (!ActiveTeacher(data, command.TeacherId))
                return OperationResult.Invalid<IReadOnlyList<Change>>(nameof(command.TeacherId), ErrorCodes.InvalidOption);
            if (data.Section(command.SectionId) is not { } section)
                return OperationResult.Invalid<IReadOnlyList<Change>>(nameof(command.SectionId), ErrorCodes.InvalidOption);
            var lines = data.EntriesOf(section.StageId).ToArray();
            var chosen = command.EntryIds is { Count: > 0 } ids ? lines.Where(entry => ids.Contains(entry.Id)).ToArray() : lines;
            if (command.EntryIds is { Count: > 0 } requested && chosen.Length != requested.Distinct().Count())
                return OperationResult.Invalid<IReadOnlyList<Change>>(nameof(command.EntryIds), ErrorCodes.InvalidOption);
            return OperationResult.Success<IReadOnlyList<Change>>(chosen.Select(entry => Assign(data, section, entry, command.TeacherId, command.Overwrite)).ToArray());
        }, token);
    }

    public Task<OperationResult<WorkloadPlanDto>> TransferAsync(long yearId, TransferWorkloadCommand command, bool apply, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PlanAsync(yearId, apply, "WorkloadTransferred", data =>
        {
            if (!data.Teachers.ContainsKey(command.FromTeacherId))
                return OperationResult.Invalid<IReadOnlyList<Change>>(nameof(command.FromTeacherId), ErrorCodes.InvalidOption);
            if (command.ToTeacherId == command.FromTeacherId || !ActiveTeacher(data, command.ToTeacherId))
                return OperationResult.Invalid<IReadOnlyList<Change>>(nameof(command.ToTeacherId), ErrorCodes.InvalidOption);
            return OperationResult.Success<IReadOnlyList<Change>>(OrderedAssignmentsOf(data, command.FromTeacherId)
                .Select(row => new Change(row, data.Section(row.SectionId)!, data.Entry(row.CurriculumEntryId)!, row.TeacherId, command.ToTeacherId, Transfer)).ToArray());
        }, token);
    }

    public Task<OperationResult<WorkloadPlanDto>> RemoveAsync(long yearId, RemoveWorkloadCommand command, bool apply, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PlanAsync(yearId, apply, "WorkloadRemoved", data =>
        {
            if (!data.Teachers.ContainsKey(command.TeacherId))
                return OperationResult.Invalid<IReadOnlyList<Change>>(nameof(command.TeacherId), ErrorCodes.InvalidOption);
            return OperationResult.Success<IReadOnlyList<Change>>(OrderedAssignmentsOf(data, command.TeacherId)
                .Select(row => new Change(row, data.Section(row.SectionId)!, data.Entry(row.CurriculumEntryId)!, row.TeacherId, null, Remove)).ToArray());
        }, token);
    }

    private sealed record Change(WorkloadAssignment? Current, Section Section, CurriculumEntry Entry, long? Before, long? After, string Action);

    /// <summary>One cell of a bulk assignment: create, replace (only when overwriting), skip or unchanged.</summary>
    private static Change Assign(WorkloadData data, Section section, CurriculumEntry entry, long teacherId, bool overwrite)
    {
        var current = data.Assignment(section.Id, entry.Id);
        var action = current is null ? Create : current.TeacherId == teacherId ? Unchanged : overwrite ? Replace : Skip;
        return new Change(current, section, entry, current?.TeacherId, action is Create or Replace ? teacherId : current?.TeacherId, action);
    }

    private static IEnumerable<WorkloadAssignment> OrderedAssignmentsOf(WorkloadData data, long teacherId) =>
        data.Assignments.Where(row => row.TeacherId == teacherId && !row.IsArchived)
            .Select(row => (Row: row, Section: data.Section(row.SectionId)!))
            .OrderBy(pair => data.Stages.ToList().FindIndex(stage => stage.Id == pair.Section.StageId))
            .ThenBy(pair => pair.Section.NormalizedLabel, StringComparer.Ordinal)
            .Select(pair => pair.Row);

    /// <summary>Builds the plan; when applying, writes exactly the planned changes in one save.</summary>
    private async Task<OperationResult<WorkloadPlanDto>> PlanAsync(long yearId, bool apply, string auditEvent,
        Func<WorkloadData, OperationResult<IReadOnlyList<Change>>> plan, CancellationToken token)
    {
        if (await WorkloadData.LoadAsync(store, yearId, forUpdate: apply, token) is not { } data)
            return OperationResult.Failure<WorkloadPlanDto>(ErrorCodes.NotFound);
        var planned = plan(data);
        if (!planned.Succeeded)
            return planned.Cast<WorkloadPlanDto>();
        var changes = planned.Value!;
        var dto = ToPlan(data, changes);
        if (!apply || dto.Changes == 0)
            return OperationResult.Success(dto);
        var now = clock.GetUtcNow();
        foreach (var change in changes)
        {
            switch (change.Action)
            {
                case Create:
                    store.Add(WorkloadAssignment.Create(change.Section.Id, change.Entry.Id, change.After!.Value));
                    break;
                case Replace or Transfer:
                    change.Current!.Reassign(change.After!.Value);
                    break;
                case Remove:
                    change.Current!.Archive(now);
                    break;
            }
        }
        AuditTrail.Record(store, clock, auditEvent, $"academic-year:{yearId}", $"{dto.Changes} workload changes.");
        return await store.SaveAsync(() => dto, "TeacherId", token);
    }

    private static WorkloadPlanDto ToPlan(WorkloadData data, IReadOnlyList<Change> changes)
    {
        string? Name(long? id) => id is { } value && data.Teachers.TryGetValue(value, out var teacher) ? teacher.FullName : null;
        var lines = changes.Select(change => new WorkloadPlanLineDto(
            change.Section.Id, data.StageOf(change.Section).Name, change.Section.Label, change.Entry.Id, data.SubjectName(change.Entry),
            change.Entry.Label, change.Entry.WeeklyLessons, Name(change.Before), Name(change.After), change.Action)).ToArray();
        var moving = changes.Where(change => ChangingActions.Contains(change.Action)).ToArray();
        var affected = moving.SelectMany(change => new[] { change.Before, change.After }).OfType<long>().Distinct();
        var loads = affected.Where(data.Teachers.ContainsKey).Select(id =>
        {
            var teacher = data.Teachers[id];
            var before = data.AssignedLessons(id);
            var after = before
                - moving.Where(change => change.Before == id).Sum(change => change.Entry.WeeklyLessons)
                + moving.Where(change => change.After == id).Sum(change => change.Entry.WeeklyLessons);
            return new TeacherLoadChangeDto(id, teacher.FullName, before, after, data.LimitOf(teacher));
        }).OrderBy(load => load.FullName, StringComparer.Ordinal).ToArray();
        return new WorkloadPlanDto(lines, moving.Length, loads);
    }

    private static bool ActiveTeacher(WorkloadData data, long teacherId) =>
        data.Teachers.TryGetValue(teacherId, out var teacher) && !teacher.IsArchived;

    private static WorkloadStageDto StageMatrix(WorkloadData data, Stage stage)
    {
        var lines = data.EntriesOf(stage.Id).ToArray();
        var rows = data.SectionsOf(stage.Id).Select(section =>
        {
            var cells = lines.Select(entry =>
            {
                var assignment = data.Assignment(section.Id, entry.Id);
                return new WorkloadCellDto(entry.Id, assignment?.Id, assignment?.TeacherId, assignment?.Version,
                    assignment is not null && data.OutsideSpecialization(assignment.TeacherId, entry));
            }).ToArray();
            var assigned = cells.Where(cell => cell.AssignmentId is not null).ToArray();
            return new WorkloadSectionRowDto(section.Id, section.Label, data.ShiftName(section), assigned.Length, lines.Length,
                assigned.Sum(cell => lines.First(entry => entry.Id == cell.EntryId).WeeklyLessons), lines.Sum(entry => entry.WeeklyLessons), cells);
        }).ToArray();
        var columns = lines.Select(entry =>
        {
            var subject = data.Subjects[entry.SubjectId];
            return new WorkloadLineDto(entry.Id, subject.Id, subject.Name, subject.ColorIndex, entry.Label, entry.WeeklyLessons);
        }).ToArray();
        return new WorkloadStageDto(stage.Id, stage.Name, columns, rows);
    }

    private static TeacherLoadDto LoadOf(WorkloadData data, Teacher teacher)
    {
        var own = data.Assignments.Where(row => row.TeacherId == teacher.Id && !row.IsArchived).ToArray();
        var availability = data.AvailabilityOf(teacher, own);
        var released = data.ReleasedForYear(teacher);
        var available = released ? 0 : availability.ByDayLimit;
        var limit = availability.Available;
        var assigned = own.Sum(data.LessonsOf);
        var assignments = own.Select(row =>
        {
            var section = data.Section(row.SectionId)!;
            var entry = data.Entry(row.CurriculumEntryId)!;
            return new TeacherAssignmentDto(row.Id, section.Id, data.StageOf(section).Name, section.Label, entry.Id, data.SubjectName(entry), entry.Label,
                entry.WeeklyLessons, data.OutsideSpecialization(teacher.Id, entry));
        }).ToArray();
        return new TeacherLoadDto(teacher.Id, teacher.FullName, teacher.ShortName,
            teacher.Specializations.Select(item => item.SubjectId).Order().ToArray(), assigned, teacher.MaxLessonsPerWeek, available, limit,
            WorkloadData.Status(assigned, limit), released, assignments, teacher.Version);
    }
}
