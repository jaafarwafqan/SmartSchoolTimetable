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

/// <summary>Stable codes of findings that only exist against current data (MF11); the rest reuse <see cref="ViolationCodes"/>.</summary>
public static class CurrentFindingCodes
{
    /// <summary>The curriculum line of the section is now taught by another teacher than the one in the saved timetable.</summary>
    public const string TeacherReassigned = "TEACHER_REASSIGNED";

    /// <summary>The (section, curriculum line) of a saved lesson no longer has any teacher assignment (or no longer exists).</summary>
    public const string AssignmentRemoved = "ASSIGNMENT_REMOVED";

    public static readonly IReadOnlyList<string> All = [TeacherReassigned, AssignmentRemoved];
}

/// <summary>Stable codes of the school-data changes shown under «ما الذي تغيّر منذ التوليد» (MF11). The screen turns each into one Arabic sentence.</summary>
public static class InputChangeCodes
{
    // Teacher assignments
    public const string AssignmentTeacherChanged = "ASSIGNMENT_TEACHER_CHANGED";
    public const string AssignmentAdded = "ASSIGNMENT_ADDED";
    public const string AssignmentRemoved = "ASSIGNMENT_REMOVED";

    // Availability
    public const string TeacherOffDaysChanged = "TEACHER_OFF_DAYS_CHANGED";
    public const string TeacherBlockedChanged = "TEACHER_BLOCKED_CHANGED";
    public const string TeacherReleasedChanged = "TEACHER_RELEASED_CHANGED";
    public const string TeacherArchivedChanged = "TEACHER_ARCHIVED_CHANGED";
    public const string TeacherSpecializationsChanged = "TEACHER_SPECIALIZATIONS_CHANGED";

    // Loads
    public const string TeacherDayLimitChanged = "TEACHER_DAY_LIMIT_CHANGED";
    public const string TeacherWeekLimitChanged = "TEACHER_WEEK_LIMIT_CHANGED";

    // Curriculum hours
    public const string LessonsPerWeekChanged = "LESSONS_PER_WEEK_CHANGED";
    public const string LineAdded = "LINE_ADDED";
    public const string LineRemoved = "LINE_REMOVED";
    public const string LineDoubleChanged = "LINE_DOUBLE_CHANGED";

    // Timing and sections
    public const string WorkingDaysChanged = "WORKING_DAYS_CHANGED";
    public const string ShiftTimingChanged = "SHIFT_TIMING_CHANGED";
    public const string SectionAdded = "SECTION_ADDED";
    public const string SectionRemoved = "SECTION_REMOVED";
    public const string SectionDaysChanged = "SECTION_DAYS_CHANGED";

    // Subjects, resources, priorities
    public const string SubjectRulesChanged = "SUBJECT_RULES_CHANGED";
    public const string ResourceChanged = "RESOURCE_CHANGED";
    public const string PrioritiesChanged = "PRIORITIES_CHANGED";

    /// <summary>The hash differs but no listed field does (a safety net so the screen never says "changed" with nothing to show).</summary>
    public const string OtherChange = "OTHER_CHANGE";

    public static readonly IReadOnlyList<string> All =
    [
        AssignmentTeacherChanged, AssignmentAdded, AssignmentRemoved, TeacherOffDaysChanged, TeacherBlockedChanged, TeacherReleasedChanged,
        TeacherArchivedChanged, TeacherSpecializationsChanged, TeacherDayLimitChanged, TeacherWeekLimitChanged, LessonsPerWeekChanged, LineAdded,
        LineRemoved, LineDoubleChanged, WorkingDaysChanged, ShiftTimingChanged, SectionAdded, SectionRemoved, SectionDaysChanged,
        SubjectRulesChanged, ResourceChanged, PrioritiesChanged, OtherChange,
    ];
}
