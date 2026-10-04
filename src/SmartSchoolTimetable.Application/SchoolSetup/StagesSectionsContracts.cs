namespace SmartSchoolTimetable.Application.SchoolSetup;

public sealed record StageDto(long Id, long AcademicYearId, string Name, int DisplayOrder, bool IsArchived, DateTimeOffset? ArchivedAt, int Version);
public sealed record SaveStageCommand(string? Name, int DisplayOrder, int Version);
public sealed record SectionDto(long Id, long StageId, string Label, long ShiftId, int? StudentCount, int WeeklyCapacity, bool IsArchived, DateTimeOffset? ArchivedAt, int Version);
public sealed record SaveSectionCommand(string? Label, long ShiftId, int? StudentCount, int Version);
public sealed record ArchiveCommand(int Version);
