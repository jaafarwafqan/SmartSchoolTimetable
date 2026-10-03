using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.SchoolSetup;

/// <summary>App-shell context: school name, current year and term, and display preferences.</summary>
public sealed class SchoolContextService(IDataStore store)
{
    public async Task<SchoolContextDto> GetAsync(CancellationToken cancellationToken)
    {
        var profile = await store.FirstOrDefaultAsync(store.Query<SchoolProfile>(), cancellationToken)
            ?? throw new InvalidOperationException("The school profile row is created at database initialization.");
        var year = await CurrentYearAsync(store, cancellationToken);
        var term = year?.CurrentTerm;
        return new SchoolContextDto(
            profile.Name,
            ApiText.ToValue(profile.NumeralSystem),
            ApiText.ToValue(profile.CalendarDisplay),
            profile.TimeZoneId,
            year is null ? null : new CurrentPeriodDto(year.Id, year.Label, InputParsing.Format(year.StartDate), InputParsing.Format(year.EndDate)),
            term is null ? null : new CurrentPeriodDto(term.Id, term.Name, InputParsing.Format(term.StartDate), InputParsing.Format(term.EndDate)));
    }

    public static Task<AcademicYear?> CurrentYearAsync(IDataStore store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.FirstOrDefaultAsync(store.Query<AcademicYear>().Where(year => year.IsCurrent), cancellationToken);
    }
}
