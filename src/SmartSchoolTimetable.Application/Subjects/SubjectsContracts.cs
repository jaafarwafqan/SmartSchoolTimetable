using SmartSchoolTimetable.Application.SchoolSetup;

namespace SmartSchoolTimetable.Application.Subjects;

/// <param name="ColorIndex">1..10: the subject palette token <c>subject-{n}</c> (no free colours).</param>
public sealed record SubjectDto(
    long Id,
    string Name,
    int ColorIndex,
    int Priority,
    bool DistributionEnabled,
    bool SpreadAcrossDays,
    bool Heavy,
    bool RequiresDoublePeriod,
    IReadOnlyList<BlockedPeriodDto> BlockedPeriods,
    string? Notes,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    int Version);

public sealed record SaveSubjectCommand(
    string? Name,
    int ColorIndex,
    int Priority,
    bool DistributionEnabled,
    bool SpreadAcrossDays,
    bool Heavy,
    bool RequiresDoublePeriod,
    IReadOnlyList<BlockedPeriodDto>? BlockedPeriods,
    string? Notes,
    int Version);
