namespace SmartSchoolTimetable.Application.Workload;

/// <summary>A curriculum line of the stage: one matrix column.</summary>
public sealed record WorkloadLineDto(long EntryId, long SubjectId, string SubjectName, int ColorIndex, string? Label, int WeeklyLessons);

/// <param name="AssignmentId">Null when the line is not assigned in this section.</param>
/// <param name="OutsideSpecialization">The teacher does not list the subject among their specializations (warning only).</param>
public sealed record WorkloadCellDto(long EntryId, long? AssignmentId, long? TeacherId, int? Version, bool OutsideSpecialization);

/// <param name="AssignedLines">Lines with a teacher, out of <paramref name="TotalLines"/> («x من y»).</param>
public sealed record WorkloadSectionRowDto(long SectionId, string Label, string ShiftName, int AssignedLines, int TotalLines, int AssignedLessons, int TotalLessons, IReadOnlyList<WorkloadCellDto> Cells);

public sealed record WorkloadStageDto(long StageId, string StageName, IReadOnlyList<WorkloadLineDto> Lines, IReadOnlyList<WorkloadSectionRowDto> Sections);

/// <param name="AssignedCells">Section × line cells with a teacher, out of <paramref name="TotalCells"/>.</param>
public sealed record WorkloadStageSummaryDto(long StageId, string StageName, int AssignedCells, int TotalCells);

/// <param name="Stage">The chosen stage's matrix (the first stage when none was chosen), or null without stages.</param>
public sealed record WorkloadMatrixDto(IReadOnlyList<WorkloadStageSummaryDto> Stages, WorkloadStageDto? Stage);

/// <summary>One assignment in a teacher's load list.</summary>
public sealed record TeacherAssignmentDto(long AssignmentId, long SectionId, string StageName, string SectionLabel, long EntryId, string SubjectName, string? Label, int WeeklyLessons, bool OutsideSpecialization);

/// <param name="Limit">The weekly lessons the teacher can take: the smaller of max per week and available slots.</param>
/// <param name="Status">within («ضمن الحد»), near («قريب»: 90% of the limit or more) or over («تجاوز»).</param>
public sealed record TeacherLoadDto(
    long TeacherId,
    string FullName,
    string ShortName,
    IReadOnlyList<long> SpecializationIds,
    int AssignedLessons,
    int? MaxPerWeek,
    int Available,
    int Limit,
    string Status,
    bool Released,
    IReadOnlyList<TeacherAssignmentDto> Assignments,
    int Version);

/// <param name="TeacherId">Null clears the cell (the assignment is archived).</param>
/// <param name="AssignmentId">The assignment shown in the cell with its <paramref name="Version"/>; null for an empty cell.</param>
public sealed record SetWorkloadCellCommand(long SectionId, long EntryId, long? TeacherId, long? AssignmentId, int? Version);

/// <summary>«تعيين معلم لمادة في كل شعب مرحلة»: one line of a stage to one teacher in every active section.</summary>
public sealed record AssignAcrossStageCommand(long TeacherId, long EntryId, bool Overwrite);

/// <summary>«تعيين معلم الصف لمواد شعبة»: several lines of one section (all lines when <paramref name="EntryIds"/> is empty).</summary>
public sealed record ClassTeacherCommand(long TeacherId, long SectionId, IReadOnlyList<long>? EntryIds, bool Overwrite);

/// <summary>«نقل أنصبة معلم إلى معلم آخر»: every active assignment of the year moves to the other teacher.</summary>
public sealed record TransferWorkloadCommand(long FromTeacherId, long ToTeacherId);

/// <summary>«إزالة أنصبة معلم»: every active assignment of the year is archived.</summary>
public sealed record RemoveWorkloadCommand(long TeacherId);

/// <param name="Action">create, replace, skip (assigned to another teacher, not overwritten), unchanged, transfer or remove.</param>
public sealed record WorkloadPlanLineDto(long SectionId, string StageName, string SectionLabel, long EntryId, string SubjectName, string? Label, int WeeklyLessons, string? CurrentTeacher, string? NewTeacher, string Action);

public sealed record TeacherLoadChangeDto(long TeacherId, string FullName, int Before, int After, int Limit);

/// <param name="Changes">Lines that would change (create, replace, transfer, remove).</param>
public sealed record WorkloadPlanDto(IReadOnlyList<WorkloadPlanLineDto> Lines, int Changes, IReadOnlyList<TeacherLoadChangeDto> Loads);
