namespace SmartSchoolTimetable.Application.SchoolSetup;

public sealed record TermDto(long Id, string Name, string StartDate, string EndDate, bool IsCurrent);

public sealed record AcademicYearDto(
    long Id,
    string Label,
    string StartDate,
    string EndDate,
    bool IsCurrent,
    IReadOnlyList<TermDto> Terms,
    int Version,
    int HolidaysAdded = 0);

/// <param name="CopyStructureFromYearId">Optional: copy shifts, periods, stages and sections (never calendar days).</param>
public sealed record SaveAcademicYearCommand(
    string? Label,
    string? StartDate,
    string? EndDate,
    int Version,
    long? CopyStructureFromYearId = null);

public sealed record SaveTermCommand(string? Name, string? StartDate, string? EndDate, int Version);

public sealed record VersionCommand(int Version);
