namespace SmartSchoolTimetable.Application.SchoolSetup;

public sealed record LessonPeriodDto(int Position, string Kind, string StartTime, string EndTime, bool StartBell, bool EndBell);
/// <param name="Kind">morning, evening (created by the shift mode) or other.</param>
/// <param name="DayLessons">Lessons taught on each working day (per-day counts, ADR 0020).</param>
/// <param name="WeeklyLessons">Sum of <paramref name="DayLessons"/>: the weekly capacity of a section on this shift.</param>
public sealed record ShiftDto(
    long Id,
    long AcademicYearId,
    string Name,
    int DisplayOrder,
    string Kind,
    int LessonCount,
    IReadOnlyList<LessonPeriodDto> Periods,
    IReadOnlyList<DayLessonsDto> DayLessons,
    int WeeklyLessons,
    int Version);

public sealed record SetDayLessonsCommand(IReadOnlyList<DayLessonsDto>? DayLessons, int Version);
public sealed record SaveShiftCommand(string? Name, int DisplayOrder, int Version);
public sealed record PeriodInput(string? Kind, string? StartTime, string? EndTime, bool StartBell = true, bool EndBell = true);
public sealed record ReplacePeriodsCommand(IReadOnlyList<PeriodInput>? Periods, int Version);
public sealed record BreakSlotDto(int AfterLesson, int Minutes);

/// <param name="Breaks">Several breaks (presets); when null, the single <paramref name="BreakAfterLesson"/> form applies.</param>
public sealed record GeneratePeriodsCommand(string? FirstStartTime, int LessonMinutes, int LessonCount, int BreakMinutes, int? BreakAfterLesson, IReadOnlyList<BreakSlotDto>? Breaks = null);
public sealed record GeneratedPeriodsDto(IReadOnlyList<LessonPeriodDto> Periods);
public sealed record WorkingWeekDto(IReadOnlyList<int> Days, int WeekStartDay, int Version);
public sealed record UpdateWorkingWeekCommand(IReadOnlyList<int>? Days, int WeekStartDay, int Version);
public sealed record BellSettingsDto(string Tone, bool BreakBell, int Version);
public sealed record UpdateBellSettingsCommand(string? Tone, bool BreakBell, int Version);
