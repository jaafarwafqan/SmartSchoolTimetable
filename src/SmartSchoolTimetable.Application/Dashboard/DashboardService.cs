using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Curriculum;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.Workload;

namespace SmartSchoolTimetable.Application.Dashboard;

public sealed record DashboardCountDto(string Key, int Value);

/// <param name="Key">Stable step id the UI maps to a label and a screen.</param>
public sealed record ChecklistItemDto(string Key, bool Done);

/// <param name="Curriculum">Planned lessons of each active stage of the current year against capacity, per shift.</param>
/// <param name="SetupFinished">False until the setup wizard is finished: the dashboard offers «استكمال الإعداد».</param>
public sealed record DashboardSummaryDto(
    IReadOnlyList<DashboardCountDto> Counts,
    IReadOnlyList<ChecklistItemDto> Checklist,
    IReadOnlyList<CurriculumStageDto> Curriculum,
    bool SetupFinished);

/// <summary>
/// Real counts from the database and a computed setup checklist; nothing is estimated or invented. Year-scoped
/// counts use the current year. "Capacity gaps" = active sections whose weekly capacity is 0 (DECISIONS_PENDING #6).
/// </summary>
public sealed class DashboardService(IDataStore store)
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var profile = await store.FirstOrDefaultAsync(store.Read<SchoolProfile>(), cancellationToken);
        var currentYear = await SchoolContextService.CurrentYearAsync(store, cancellationToken);
        var years = await store.CountAsync(store.Read<AcademicYear>(), cancellationToken);
        var week = await store.FirstOrDefaultAsync(store.Read<WorkingWeek>(), cancellationToken);
        var subjects = await store.CountAsync(store.Read<Subject>().Where(row => !row.IsArchived), cancellationToken);
        var teachers = await store.CountAsync(store.Read<Teacher>().Where(row => !row.IsArchived), cancellationToken);

        var yearId = currentYear?.Id ?? 0;
        var shifts = await store.ListAsync(store.Read<Shift>().Where(row => row.AcademicYearId == yearId), cancellationToken);
        var stages = (await store.ListAsync(
            store.Read<Stage>().Where(row => row.AcademicYearId == yearId && !row.IsArchived), cancellationToken)).ToDictionary(row => row.Id);
        var stageIds = stages.Keys.ToList();
        var sections = await store.ListAsync(
            store.Read<Section>().Where(row => !row.IsArchived && stageIds.Contains(row.StageId)), cancellationToken);
        var sectionIds = sections.Select(section => section.Id).ToArray();
        var curriculumEntries = await store.ListAsync(
            store.Read<CurriculumEntry>().Where(row => !row.IsArchived && stageIds.Contains(row.StageId)), cancellationToken);
        var entryIds = curriculumEntries.Select(entry => entry.Id).ToArray();
        var assignments = await store.CountAsync(store.Read<WorkloadAssignment>()
            .Where(row => !row.IsArchived && sectionIds.Contains(row.SectionId) && entryIds.Contains(row.CurriculumEntryId)), cancellationToken);
        var workloadCells = sections.Sum(section => curriculumEntries.Count(entry => entry.StageId == section.StageId));
        var shiftById = shifts.ToDictionary(shift => shift.Id);
        var capacityGaps = sections.Count(section => Section.WeeklyCapacity(week, shiftById.GetValueOrDefault(section.ShiftId), stages.GetValueOrDefault(section.StageId)) == 0);

        var counts = new List<DashboardCountDto>
        {
            new("teachers", teachers),
            new("academicYears", years),
            new("stages", stageIds.Count),
            new("sections", sections.Count),
            new("subjects", subjects),
            new("capacityGaps", capacityGaps),
        };
        var checklist = new List<ChecklistItemDto>
        {
            new("schoolProfile", profile?.IsFilled == true),
            new("academicYear", currentYear is not null && currentYear.CurrentTermId is not null),
            new("timetableStructure", shifts.Count > 0 && shifts.All(shift => shift.LessonCount > 0) && week?.DayCount > 0),
            new("stagesSections", stageIds.Count > 0 && sections.Count > 0),
            new("subjects", subjects > 0),
            new("teachers", teachers > 0),
            new("workload", workloadCells > 0 && assignments == workloadCells),
        };
        var curriculum = await CurriculumTableBuilder.BuildAsync(store, yearId, cancellationToken);
        var progress = await store.FirstOrDefaultAsync(store.Read<SetupProgress>(), cancellationToken);
        return new DashboardSummaryDto(counts, checklist, curriculum.Stages, progress?.IsFinished == true);
    }
}
