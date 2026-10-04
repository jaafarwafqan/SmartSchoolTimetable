namespace SmartSchoolTimetable.Application.Stages;

public sealed record StageDto(long Id, long AcademicYearId, string Name, int DisplayOrder, bool IsArchived, DateTimeOffset? ArchivedAt, int Version);

public sealed record SaveStageCommand(string? Name, int DisplayOrder, int Version);

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
