namespace SmartSchoolTimetable.Application.SchoolSetup;

public sealed record SchoolProfileDto(
    string Name,
    string SchoolType,
    string StudyType,
    string? PrincipalName,
    string? ScheduleOfficerName,
    string TimeZone,
    string NumeralSystem,
    string CalendarDisplay,
    bool HasLogo,
    bool HasStamp,
    int Version,
    SchoolProfileOptions Options);

public sealed record SchoolProfileOptions(
    IReadOnlyList<string> SchoolTypes,
    IReadOnlyList<string> StudyTypes,
    IReadOnlyList<string> NumeralSystems,
    IReadOnlyList<string> CalendarDisplays,
    IReadOnlyList<string> TimeZones);

public sealed record UpdateSchoolProfileCommand(
    string? Name,
    string? SchoolType,
    string? StudyType,
    string? PrincipalName,
    string? ScheduleOfficerName,
    string? TimeZone,
    string? NumeralSystem,
    string? CalendarDisplay,
    int Version);

public sealed record AssetContent(Stream Content, string ContentType, string FileName);

/// <summary>Everything the app shell needs: school name, current year/term and display preferences.</summary>
public sealed record SchoolContextDto(
    string SchoolName,
    string NumeralSystem,
    string CalendarDisplay,
    string TimeZone,
    CurrentPeriodDto? CurrentYear,
    CurrentPeriodDto? CurrentTerm);

public sealed record CurrentPeriodDto(long Id, string Name, string StartDate, string EndDate);
