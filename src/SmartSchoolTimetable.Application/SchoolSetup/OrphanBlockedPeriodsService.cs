using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Teachers;

namespace SmartSchoolTimetable.Application.SchoolSetup;

/// <summary>A teacher or subject with blocked slots outside the current grid.</summary>
/// <param name="Kind">"teacher" or "subject".</param>
public sealed record OrphanOwnerDto(string Kind, long Id, string Name, int Version, IReadOnlyList<BlockedPeriodDto> Periods);

public sealed record OrphanBlockedPeriodsDto(IReadOnlyList<OrphanOwnerDto> Owners, int Total);

public sealed record OrphanOwnerVersion(string Kind, long Id, int Version);

/// <summary>The owners shown in the preview with the versions that were read; only these are cleaned.</summary>
public sealed record CleanOrphanBlockedPeriodsCommand(IReadOnlyList<OrphanOwnerVersion>? Owners);

/// <summary>
/// Orphan blocked periods (Phase 3 §5.5): blocked slots of teachers and subjects that no longer exist after the
/// working days, shifts or lessons per day changed. They are detected and previewed, and removed only after the
/// owner confirms; until then they stay (the readiness validator reports them) and nothing is deleted silently.
/// </summary>
public sealed class OrphanBlockedPeriodsService(IDataStore store, TimeProvider clock)
{
    public const string TeacherKind = "teacher";
    public const string SubjectKind = "subject";

    public async Task<OrphanBlockedPeriodsDto> GetAsync(CancellationToken token)
    {
        var grid = await ScheduleGrids.LoadAsync(store, token);
        var (teachers, subjects) = await LoadAsync(token);
        return Report(grid, teachers, subjects);
    }

    public async Task<OperationResult<OrphanBlockedPeriodsDto>> CleanAsync(CleanOrphanBlockedPeriodsCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var grid = await ScheduleGrids.LoadAsync(store, token);
        var (teachers, subjects) = await LoadAsync(token);
        var confirmed = grid.LessonsPerDay == 0 ? [] : command.Owners ?? [];
        foreach (var owner in confirmed)
        {
            var (version, drop) = owner.Kind switch
            {
                TeacherKind when teachers.FirstOrDefault(row => row.Id == owner.Id) is { } teacher => (teacher.Version, () => teacher.DropBlockedOutside(grid)),
                SubjectKind when subjects.FirstOrDefault(row => row.Id == owner.Id) is { } subject => (subject.Version, () => subject.DropBlockedOutside(grid)),
                _ => (-1, (Func<IReadOnlyList<BlockedPeriod>>?)null),
            };
            if (drop is null)
                return OperationResult.Failure<OrphanBlockedPeriodsDto>(ErrorCodes.NotFound);
            if (version != owner.Version)
                return OperationResult.Failure<OrphanBlockedPeriodsDto>(ErrorCodes.Conflict);
            var removed = drop();
            if (removed.Count > 0)
                AuditTrail.Record(store, clock, "OrphanBlockedPeriodsRemoved", $"{owner.Kind}:{owner.Id}", $"Removed {removed.Count} blocked periods outside the timetable grid.");
        }
        return await store.SaveAsync(() => Report(grid, teachers, subjects), "BlockedPeriods", token);
    }

    private async Task<(List<Teacher> Teachers, List<Subject> Subjects)> LoadAsync(CancellationToken token) =>
        (await store.ListAsync(store.Query<Teacher>().Where(row => !row.IsArchived).OrderBy(row => row.NormalizedFullName), token),
         await store.ListAsync(store.Query<Subject>().Where(row => !row.IsArchived).OrderBy(row => row.NormalizedName), token));

    private static OrphanBlockedPeriodsDto Report(ScheduleGrid grid, IEnumerable<Teacher> teachers, IEnumerable<Subject> subjects)
    {
        // Without periods there is no grid to compare with (for example, between years): nothing is an orphan.
        if (grid.LessonsPerDay == 0)
            return new OrphanBlockedPeriodsDto([], 0);
        var owners = teachers
            .Select(row => Owner(TeacherKind, row.Id, row.FullName, row.Version, row.BlockedPeriods, grid))
            .Concat(subjects.Select(row => Owner(SubjectKind, row.Id, row.Name, row.Version, row.BlockedPeriods, grid)))
            .Where(owner => owner.Periods.Count > 0)
            .ToArray();
        return new OrphanBlockedPeriodsDto(owners, owners.Sum(owner => owner.Periods.Count));
    }

    private static OrphanOwnerDto Owner(string kind, long id, string name, int version, IEnumerable<BlockedPeriod> periods, ScheduleGrid grid) =>
        new(kind, id, name, version, ScheduleGrids.ToDtos(periods.Where(period => !grid.Contains(period))));
}
