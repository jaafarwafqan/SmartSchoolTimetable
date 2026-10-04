using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Dashboard;

public sealed record DashboardCountDto(string Key, int Value);

/// <param name="Key">Stable step id the UI maps to a label and a screen.</param>
public sealed record ChecklistItemDto(string Key, bool Done);

public sealed record DashboardSummaryDto(IReadOnlyList<DashboardCountDto> Counts, IReadOnlyList<ChecklistItemDto> Checklist);

/// <summary>Real counts from the database and a computed setup checklist; nothing is estimated or invented.</summary>
public sealed class DashboardService(IDataStore store)
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var profile = await store.FirstOrDefaultAsync(store.Query<SchoolProfile>(), cancellationToken);
        var currentYear = await SchoolContextService.CurrentYearAsync(store, cancellationToken);
        var years = await store.CountAsync(store.Query<AcademicYear>(), cancellationToken);
        var shifts = currentYear is null ? 0 : await store.CountAsync(store.Query<Shift>().Where(row => row.AcademicYearId == currentYear.Id), cancellationToken);
        var stages = currentYear is null ? 0 : await store.CountAsync(store.Query<Stage>().Where(row => row.AcademicYearId == currentYear.Id && !row.IsArchived), cancellationToken);
        var sections = currentYear is null ? 0 : await store.CountAsync(store.Query<Section>().Where(row => !row.IsArchived && store.Query<Stage>().Any(stage => stage.Id == row.StageId && stage.AcademicYearId == currentYear.Id)), cancellationToken);
        var workingWeek = await store.FirstOrDefaultAsync(store.Query<WorkingWeek>().Where(row => row.Id == WorkingWeek.SingletonId), cancellationToken);
        var yearShifts = currentYear is null ? [] : await store.ListAsync(store.Query<Shift>().Where(row => row.AcademicYearId == currentYear.Id), cancellationToken);
        var hasPeriods = yearShifts.Any(row => row.LessonCount > 0);

        var counts = new List<DashboardCountDto> { new("academicYears", years), new("stages", stages), new("sections", sections) };
        var checklist = new List<ChecklistItemDto>
        {
            new("schoolProfile", profile?.IsFilled == true),
            new("academicYear", currentYear is not null && currentYear.CurrentTermId is not null),
            new("timetableStructure", shifts > 0 && hasPeriods && workingWeek?.Days.Count > 0),
            new("stagesSections", stages > 0 && sections > 0),
        };
        return new DashboardSummaryDto(counts, checklist);
    }
}
