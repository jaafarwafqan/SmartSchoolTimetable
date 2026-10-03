namespace SmartSchoolTimetable.Application.SchoolSetup;

public sealed record LessonPeriodDto(int Position, string Kind, string StartTime, string EndTime, bool StartBell, bool EndBell);
public sealed record ShiftDto(long Id, long AcademicYearId, string Name, int DisplayOrder, int LessonCount, IReadOnlyList<LessonPeriodDto> Periods, int Version);
public sealed record SaveShiftCommand(string? Name, int DisplayOrder, int Version);
public sealed record PeriodInput(string? Kind, string? StartTime, string? EndTime, bool StartBell = true, bool EndBell = true);
public sealed record ReplacePeriodsCommand(IReadOnlyList<PeriodInput>? Periods, int Version);
public sealed record GeneratePeriodsCommand(string? FirstStartTime, int LessonMinutes, int LessonCount, int BreakMinutes, int? BreakAfterLesson);
public sealed record GeneratedPeriodsDto(IReadOnlyList<LessonPeriodDto> Periods);
public sealed record WorkingWeekDto(IReadOnlyList<int> Days, int WeekStartDay, int Version);
public sealed record UpdateWorkingWeekCommand(IReadOnlyList<int>? Days, int WeekStartDay, int Version);
public sealed record BellSettingsDto(string Tone, bool BreakBell, int Version);
public sealed record UpdateBellSettingsCommand(string? Tone, bool BreakBell, int Version);
