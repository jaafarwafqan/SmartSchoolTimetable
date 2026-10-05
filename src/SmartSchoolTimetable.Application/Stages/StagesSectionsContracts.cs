using SmartSchoolTimetable.Application.SchoolSetup;

namespace SmartSchoolTimetable.Application.Stages;

/// <param name="DayLessons">The stage's own lessons per day (ADR 0027); a working day not listed uses the shift's count.</param>
public sealed record StageDto(long Id, long AcademicYearId, string Name, int DisplayOrder, string? TemplateKey, IReadOnlyList<DayLessonsDto> DayLessons, bool IsArchived, DateTimeOffset? ArchivedAt, int Version);

/// <param name="DayLessons">Lessons per working day; an empty list returns every day to the shift's count.</param>
public sealed record SetStageDayLessonsCommand(IReadOnlyList<DayLessonsDto>? DayLessons, int Version);

/// <param name="TemplateKey">Set by templates and the wizard (e.g. "preparatory-4-scientific"); null for typed stages.</param>
public sealed record SaveStageCommand(string? Name, int DisplayOrder, int Version, string? TemplateKey = null);

/// <param name="ShiftId">Shift of the sections added; ignored when sections are removed.</param>
/// <param name="LabelStyle">arabic (أ، ب …), numbers (1، 2 …) or latin (A, B …).</param>
public sealed record SetSectionCountCommand(int Count, long ShiftId, string? LabelStyle);

/// <summary>A stage card: the stage and its sections (with shift and weekly capacity) for the stages screen.</summary>
public sealed record StageCardDto(StageDto Stage, IReadOnlyList<SectionDto> Sections);

/// <param name="WeeklyCapacity">Working days × lessons per day of the section's shift (computed, never stored).</param>
public sealed record SectionDto(
    long Id,
    long StageId,
    string Label,
    long ShiftId,
    int? StudentCount,
    int WeeklyCapacity,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    int Version);

public sealed record SaveSectionCommand(string? Label, long ShiftId, int? StudentCount, int Version);

public sealed record ArchiveCommand(int Version);
