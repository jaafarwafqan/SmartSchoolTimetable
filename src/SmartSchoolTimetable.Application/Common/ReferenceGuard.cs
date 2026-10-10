using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Domain.Workload;

namespace SmartSchoolTimetable.Application.Common;

/// <summary>Record kinds the reference guard answers for (route values of <c>GET /references/{kind}/{id}</c>).</summary>
public static class ReferenceKinds
{
    public const string Subject = "subject";
    public const string Teacher = "teacher";
    public const string Section = "section";
    public const string Stage = "stage";
    public const string Shift = "shift";
    public const string Resource = "resource";
    public const string CurriculumEntry = "curriculumEntry";

    public static readonly IReadOnlyList<string> All = [Subject, Teacher, Section, Stage, Shift, Resource, CurriculumEntry];
}

/// <summary>Dependent kinds listed in a reference report (the frontend names them in Arabic).</summary>
public static class DependentKinds
{
    public const string Section = "section";
    public const string CurriculumEntry = "curriculumEntry";
    public const string Subject = "subject";
    public const string WorkloadAssignment = "workloadAssignment";
}

/// <summary>One kind of dependent record: how many are active and archived, and a few names to show.</summary>
public sealed record DependentGroupDto(string Kind, int Active, int Archived, IReadOnlyList<string> Samples, string ErrorCode);

/// <summary>
/// "What refers to this record". Archive is blocked by active dependents, delete by any dependent (archived ones
/// keep history and a foreign key). <see cref="ArchiveBlockedBy"/> / <see cref="DeleteBlockedBy"/> carry the error
/// code the matching action returns, so the preview and the action can never disagree.
/// </summary>
public sealed record ReferenceReportDto(string Kind, long Id, IReadOnlyList<DependentGroupDto> Dependents)
{
    public string? ArchiveBlockedBy => Dependents.FirstOrDefault(group => group.Active > 0)?.ErrorCode;

    public string? DeleteBlockedBy => Dependents.FirstOrDefault(group => group.Active + group.Archived > 0)?.ErrorCode;
}

/// <summary>
/// The single place that answers which records depend on a subject, teacher, section, stage, shift, resource or
/// curriculum entry (Phase 3 §5.3). Every delete and archive path asks it before changing anything; the preview
/// endpoint returns the same report so the dialog lists the dependents before the owner confirms.
/// Teachers, sections and curriculum lines are referenced by workload assignments (3C); resources by subjects (3B).
/// </summary>
public sealed class ReferenceGuard(IDataStore store)
{
    public const int SampleSize = 5;

    public async Task<ReferenceReportDto?> InspectAsync(string kind, long id, CancellationToken token)
    {
        DependentGroupDto[]? groups = kind switch
        {
            ReferenceKinds.Stage => [await SectionsAsync(store.Read<Section>().Where(row => row.StageId == id), token),
                await CurriculumAsync(store.Read<CurriculumEntry>().Where(row => row.StageId == id), token)],
            ReferenceKinds.Subject => [await CurriculumAsync(store.Read<CurriculumEntry>().Where(row => row.SubjectId == id), token)],
            ReferenceKinds.Shift => [await SectionsAsync(store.Read<Section>().Where(row => row.ShiftId == id), token)],
            ReferenceKinds.Resource => [await SubjectsAsync(store.Read<Subject>().Where(row => row.RequiredResourceId == id), token)],
            ReferenceKinds.Teacher => [await AssignmentsAsync(store.Read<WorkloadAssignment>().Where(row => row.TeacherId == id), token)],
            ReferenceKinds.Section => [await AssignmentsAsync(store.Read<WorkloadAssignment>().Where(row => row.SectionId == id), token)],
            ReferenceKinds.CurriculumEntry => [await AssignmentsAsync(store.Read<WorkloadAssignment>().Where(row => row.CurriculumEntryId == id), token)],
            _ => null,
        };
        return groups is null ? null : new ReferenceReportDto(kind, id, groups.Where(group => group.Active + group.Archived > 0).ToArray());
    }

    /// <summary>The error code that blocks archiving, or null when nothing active depends on the record.</summary>
    public async Task<string?> ArchiveBlockedAsync(string kind, long id, CancellationToken token) =>
        (await InspectAsync(kind, id, token))?.ArchiveBlockedBy;

    /// <summary>The error code that blocks a hard delete, or null when nothing at all depends on the record.</summary>
    public async Task<string?> DeleteBlockedAsync(string kind, long id, CancellationToken token) =>
        (await InspectAsync(kind, id, token))?.DeleteBlockedBy;

    private async Task<DependentGroupDto> SectionsAsync(IQueryable<Section> sections, CancellationToken token)
    {
        var rows = await store.ListAsync(
            from section in sections
            join stage in store.Read<Stage>() on section.StageId equals stage.Id
            orderby section.IsArchived, stage.DisplayOrder, section.NormalizedLabel
            select new NamedRow(stage.Name + " / " + section.Label, section.IsArchived), token);
        return Group(DependentKinds.Section, rows, ErrorCodes.RecordInUse);
    }

    private async Task<DependentGroupDto> CurriculumAsync(IQueryable<CurriculumEntry> entries, CancellationToken token)
    {
        var rows = await store.ListAsync(
            from entry in entries
            join stage in store.Read<Stage>() on entry.StageId equals stage.Id
            join subject in store.Read<Subject>() on entry.SubjectId equals subject.Id
            orderby entry.IsArchived, stage.DisplayOrder, subject.NormalizedName
            select new NamedRow(stage.Name + " / " + subject.Name + (entry.Label == null ? "" : " (" + entry.Label + ")"), entry.IsArchived), token);
        return Group(DependentKinds.CurriculumEntry, rows, ErrorCodes.CurriculumInUse);
    }

    /// <summary>Workload assignments, named «المرحلة / الشعبة: المادة — المعلم».</summary>
    private async Task<DependentGroupDto> AssignmentsAsync(IQueryable<WorkloadAssignment> assignments, CancellationToken token)
    {
        var rows = await store.ListAsync(
            from assignment in assignments
            join section in store.Read<Section>() on assignment.SectionId equals section.Id
            join stage in store.Read<Stage>() on section.StageId equals stage.Id
            join entry in store.Read<CurriculumEntry>() on assignment.CurriculumEntryId equals entry.Id
            join subject in store.Read<Subject>() on entry.SubjectId equals subject.Id
            join teacher in store.Read<Teacher>() on assignment.TeacherId equals teacher.Id
            orderby assignment.IsArchived, stage.DisplayOrder, section.NormalizedLabel, subject.NormalizedName
            select new NamedRow(stage.Name + " / " + section.Label + ": " + subject.Name + " — " + teacher.FullName, assignment.IsArchived), token);
        return Group(DependentKinds.WorkloadAssignment, rows, ErrorCodes.WorkloadInUse);
    }

    private async Task<DependentGroupDto> SubjectsAsync(IQueryable<Subject> subjects, CancellationToken token)
    {
        var rows = await store.ListAsync(
            subjects.OrderBy(subject => subject.IsArchived).ThenBy(subject => subject.NormalizedName)
                .Select(subject => new NamedRow(subject.Name, subject.IsArchived)), token);
        return Group(DependentKinds.Subject, rows, ErrorCodes.ResourceInUse);
    }

    private static DependentGroupDto Group(string kind, List<NamedRow> rows, string errorCode) => new(
        kind,
        rows.Count(row => !row.Archived),
        rows.Count(row => row.Archived),
        rows.Take(SampleSize).Select(row => row.Name).ToArray(),
        errorCode);

    private sealed record NamedRow(string Name, bool Archived);
}
