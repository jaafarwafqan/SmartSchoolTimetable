namespace SmartSchoolTimetable.Application.Curriculum;

/// <param name="Status">under, equal or over.</param>
/// <param name="Difference">Capacity minus planned lessons (positive: lessons missing; negative: too many).</param>
public sealed record ShiftTotalDto(long ShiftId, string ShiftName, int Sections, int WeeklyCapacity, string Status, int Difference);

public sealed record CurriculumStageDto(long Id, string Name, int PlannedLessons, IReadOnlyList<ShiftTotalDto> Totals);

/// <param name="Duplicates">Further entries with the same subject and label in this stage (shown as a warning).</param>
public sealed record CurriculumCellDto(long StageId, long? EntryId, int? WeeklyLessons, int? Version, int Duplicates);

/// <param name="Label">Null for the subject's main row; a repeated entry has its own label (e.g. "قواعد").</param>
public sealed record CurriculumRowDto(long SubjectId, string SubjectName, int ColorIndex, string? Label, IReadOnlyList<CurriculumCellDto> Cells);

public sealed record CurriculumTableDto(IReadOnlyList<CurriculumStageDto> Stages, IReadOnlyList<CurriculumRowDto> Rows);

/// <param name="WeeklyLessons">Null clears the cell (the entry is deleted; nothing references entries before Phase 3).</param>
/// <param name="EntryId">The entry shown in the cell, with its <paramref name="Version"/>; null to create a new entry.</param>
public sealed record SetCurriculumCellCommand(long StageId, long SubjectId, string? Label, int? WeeklyLessons, long? EntryId, int? Version);

public sealed record CurriculumEntryDto(long Id, long StageId, long SubjectId, int WeeklyLessons, string? Label, bool NeedsDoublePeriod, string? Notes, bool IsArchived, int Version);

public sealed record SaveCurriculumEntryCommand(int WeeklyLessons, string? Label, bool NeedsDoublePeriod, string? Notes, int Version);

public sealed record CopyCurriculumCommand(long FromStageId, IReadOnlyList<long>? ToStageIds);

public sealed record SetLessonsAcrossCommand(long SubjectId, string? Label, int WeeklyLessons, IReadOnlyList<long>? StageIds);

/// <param name="Action">create, update, exists (skipped), unchanged, ambiguous (several entries match) or notApplicable.</param>
public sealed record CurriculumPlanLineDto(long StageId, string StageName, long SubjectId, string SubjectName, string? Label, int WeeklyLessons, string Action);

public sealed record CurriculumPlanDto(IReadOnlyList<CurriculumPlanLineDto> Lines, int Changes);
