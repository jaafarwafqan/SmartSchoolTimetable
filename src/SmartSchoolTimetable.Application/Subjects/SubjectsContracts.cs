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
    int Version,
    long? RequiredResourceId = null,
    int? DailyCap = null,
    IReadOnlyList<CapOverrideDto>? CapOverrides = null);

/// <param name="Scope">"stage" (every section of a grade) or "section".</param>
public sealed record CapOverrideDto(string Scope, long ScopeId, int Cap);

/// <summary>Sets (or clears, with a null cap) the daily limit of one subject for one grade or section.</summary>
public sealed record SetCapOverrideCommand(string? Scope, long ScopeId, int? Cap, int Version);

/// <summary>Sets (or clears, with null) the owner's daily limit of one subject: «أولويات الجدول › متقدم».</summary>
public sealed record SetDailyCapCommand(int? DailyCap, int Version);

/// <param name="RequiredResourceId">The one resource the subject needs (null: none). It must be an active resource,
/// unless it is the subject's current one.</param>
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
    int Version,
    long? RequiredResourceId = null);
