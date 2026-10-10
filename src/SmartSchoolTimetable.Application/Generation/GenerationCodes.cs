namespace SmartSchoolTimetable.Application.Generation;

/// <summary>
/// Stable codes of hard-constraint violations (docs/SOLVER.md). The manual editor shows them in Arabic; this file is
/// their single definition site (GenerationCodeContractTests).
/// </summary>
public static class ViolationCodes
{
    public const string UnknownLesson = "UNKNOWN_LESSON";
    public const string WrongLessonCount = "WRONG_LESSON_COUNT";
    public const string SectionConflict = "SECTION_CONFLICT";
    public const string OutsideSectionDay = "OUTSIDE_SECTION_DAY";
    public const string SectionGap = "SECTION_GAP";
    public const string TeacherConflict = "TEACHER_CONFLICT";
    public const string TeacherUnavailable = "TEACHER_UNAVAILABLE";
    public const string SubjectBlocked = "SUBJECT_BLOCKED";
    public const string TeacherDayLimit = "TEACHER_DAY_LIMIT";
    public const string TeacherWeekLimit = "TEACHER_WEEK_LIMIT";
    public const string ResourceCapacity = "RESOURCE_CAPACITY";
    public const string SubjectDailyCap = "SUBJECT_DAILY_CAP";
    public const string DoublePeriodBroken = "DOUBLE_PERIOD_BROKEN";

    public static readonly IReadOnlyList<string> All =
    [
        UnknownLesson, WrongLessonCount, SectionConflict, OutsideSectionDay, SectionGap, TeacherConflict, TeacherUnavailable,
        SubjectBlocked, TeacherDayLimit, TeacherWeekLimit, ResourceCapacity, SubjectDailyCap, DoublePeriodBroken,
    ];
}

/// <summary>Stable codes of solver diagnostics (rendered in Arabic by the frontend; GenerationCodeContractTests).</summary>
public static class DiagnosticCodes
{
    public const string TeacherAvailability = "CORE_TEACHER_AVAILABILITY";
    public const string TeacherLimits = "CORE_TEACHER_LIMITS";
    public const string SectionPacking = "CORE_SECTION_PACKING";
    public const string StageDays = "CORE_STAGE_DAYS";
    public const string SubjectBlocked = "CORE_SUBJECT_BLOCKED";
    public const string ResourceCapacity = "CORE_RESOURCE_CAPACITY";
    public const string SubjectDailyCap = "CORE_SUBJECT_DAILY_CAP";
    public const string DoublePeriods = "CORE_DOUBLE_PERIODS";
    public const string LockedLessons = "CORE_LOCKED_LESSONS";
    public const string Fundamental = "CORE_FUNDAMENTAL";
    public const string TimeoutNoSolution = "TIMEOUT_NO_SOLUTION";

    public static readonly IReadOnlyList<string> All =
    [
        TeacherAvailability, TeacherLimits, SectionPacking, StageDays, SubjectBlocked, ResourceCapacity, SubjectDailyCap, DoublePeriods,
        LockedLessons, Fundamental, TimeoutNoSolution,
    ];
}
