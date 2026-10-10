namespace SmartSchoolTimetable.Application.Scheduling;

/// <summary>
/// Stable codes of the pre-solve findings (not HTTP error codes); the frontend renders each in Arabic from its numbers.
/// This file is their single definition site: everywhere else the backend uses the constants (ErrorContractTests).
/// </summary>
public static class FindingCodes
{
    public const string NothingToSchedule = "NOTHING_TO_SCHEDULE";
    public const string UnassignedLines = "UNASSIGNED_LINES";
    public const string SectionOverCapacity = "SECTION_OVER_CAPACITY";
    public const string SectionUnderCapacity = "SECTION_UNDER_CAPACITY";
    public const string TeacherOverload = "TEACHER_OVERLOAD";
    public const string TeacherReleased = "TEACHER_RELEASED_ASSIGNED";
    public const string TeacherArchived = "TEACHER_ARCHIVED_ASSIGNED";
    public const string TeacherPartialRelease = "TEACHER_PARTIAL_RELEASE";
    public const string SubjectSlotsShort = "SUBJECT_SLOTS_SHORT";
    public const string AssignmentInfeasible = "ASSIGNMENT_INFEASIBLE";
    public const string ResourceOverCapacity = "RESOURCE_OVER_CAPACITY";
    public const string ResourceArchived = "RESOURCE_ARCHIVED";
    public const string DoublePeriodImpossible = "DOUBLE_PERIOD_IMPOSSIBLE";
    public const string DoublePeriodTight = "DOUBLE_PERIOD_TIGHT";
    public const string OrphanBlockedPeriods = "ORPHAN_BLOCKED_PERIODS";
    public const string ShiftWithoutPeriods = "SHIFT_WITHOUT_PERIODS";
    public const string DistributionDisabled = "DISTRIBUTION_DISABLED_IN_CURRICULUM";
    public const string StageWithoutCurriculum = "STAGE_WITHOUT_CURRICULUM";
    public const string TeacherShiftOverlap = "TEACHER_SHIFT_OVERLAP";

    /// <summary>Every finding code, in one registry (checked against the frontend dictionary by FindingCodeContractTests).</summary>
    public static readonly IReadOnlyList<string> All =
    [
        NothingToSchedule,
        UnassignedLines,
        SectionOverCapacity,
        SectionUnderCapacity,
        TeacherOverload,
        TeacherReleased,
        TeacherArchived,
        TeacherPartialRelease,
        SubjectSlotsShort,
        AssignmentInfeasible,
        ResourceOverCapacity,
        ResourceArchived,
        DoublePeriodImpossible,
        DoublePeriodTight,
        OrphanBlockedPeriods,
        ShiftWithoutPeriods,
        DistributionDisabled,
        StageWithoutCurriculum,
        TeacherShiftOverlap,
    ];
}
