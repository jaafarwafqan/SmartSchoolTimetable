using SmartSchoolTimetable.Application.SchoolSetup;

namespace SmartSchoolTimetable.Application.Teachers;

public sealed record TeacherDto(
    long Id,
    string FullName,
    string ShortName,
    IReadOnlyList<int> OffDays,
    IReadOnlyList<BlockedPeriodDto> BlockedPeriods,
    bool FullyReleased,
    string? ReleaseReason,
    string? ReleaseFrom,
    string? ReleaseTo,
    int? MaxLessonsPerDay,
    int? MaxLessonsPerWeek,
    string? Notes,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    int Version,
    IReadOnlyList<long>? SpecializationIds = null);

public sealed record SaveTeacherCommand(
    string? FullName,
    string? ShortName,
    IReadOnlyList<int>? OffDays,
    IReadOnlyList<BlockedPeriodDto>? BlockedPeriods,
    bool FullyReleased,
    string? ReleaseReason,
    string? ReleaseFrom,
    string? ReleaseTo,
    int? MaxLessonsPerDay,
    int? MaxLessonsPerWeek,
    string? Notes,
    int Version,
    IReadOnlyList<long>? SpecializationIds = null);

/// <summary>«إضافة المادة لتخصصاته» (Phase 3 §2.1).</summary>
public sealed record AddSpecializationCommand(int Version);

/// <param name="Names">Pasted full names, one per line; blank lines are ignored.</param>
public sealed record BulkTeachersCommand(IReadOnlyList<string>? Names);

/// <param name="Status">ready, tooLong, duplicateInList, exists or noShortName.</param>
public sealed record BulkPreviewLineDto(int Line, string FullName, string? ShortName, string Status);

public sealed record BulkPreviewDto(IReadOnlyList<BulkPreviewLineDto> Lines, int ReadyCount);

public sealed record BulkCreatedDto(int Created);
