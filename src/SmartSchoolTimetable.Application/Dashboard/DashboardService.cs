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

        var counts = new List<DashboardCountDto> { new("academicYears", years) };
        var checklist = new List<ChecklistItemDto>
        {
            new("schoolProfile", profile?.IsFilled == true),
            new("academicYear", currentYear is not null && currentYear.CurrentTermId is not null),
        };
        return new DashboardSummaryDto(counts, checklist);
    }
}
