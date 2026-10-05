namespace SmartSchoolTimetable.Application;

/// <summary>
/// The single source of every stable error code the local API can emit. Code must reference these
/// constants rather than string literals; tests enforce that each one has an HTTP status and an Arabic message.
/// </summary>
public static class ErrorCodes
{
    public const string Conflict = "CONFLICT";
    public const string CurrentPasswordIncorrect = "CURRENT_PASSWORD_INCORRECT";
    public const string InternalError = "INTERNAL_ERROR";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string InvalidHost = "INVALID_HOST";
    public const string InvalidLaunchToken = "INVALID_LAUNCH_TOKEN";
    public const string InvalidOrigin = "INVALID_ORIGIN";
    public const string InvalidPassword = "INVALID_PASSWORD";
    public const string InvalidRecoveryCode = "INVALID_RECOVERY_CODE";
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string InvalidUsername = "INVALID_USERNAME";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string NotFound = "NOT_FOUND";
    public const string RecoveryMissing = "RECOVERY_MISSING";
    public const string RequestForbidden = "REQUEST_FORBIDDEN";
    public const string SetupAlreadyComplete = "SETUP_ALREADY_COMPLETE";
    public const string SetupRequired = "SETUP_REQUIRED";
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string ValidationFailed = "VALIDATION_FAILED";

    // Field-level validation codes returned inside the `errors` array.
    public const string Required = "REQUIRED";
    public const string UsernameTooShort = "USERNAME_TOO_SHORT";
    public const string UsernameTooLong = "USERNAME_TOO_LONG";
    public const string PasswordTooShort = "PASSWORD_TOO_SHORT";
    public const string PasswordTooLong = "PASSWORD_TOO_LONG";
    public const string PasswordMismatch = "PASSWORD_MISMATCH";
    public const string InvalidInactivityTimeout = "INVALID_INACTIVITY_TIMEOUT";

    // Phase 2: school data. Top-level codes.
    public const string RecordInUse = "RECORD_IN_USE";
    public const string CurrentYearRequired = "CURRENT_YEAR_REQUIRED";
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";

    // Phase 2: field-level validation codes.
    public const string ValueTooLong = "VALUE_TOO_LONG";
    public const string ValueOutOfRange = "VALUE_OUT_OF_RANGE";
    public const string InvalidOption = "INVALID_OPTION";
    public const string InvalidDate = "INVALID_DATE";
    public const string InvalidTime = "INVALID_TIME";
    public const string InvalidDateRange = "INVALID_DATE_RANGE";
    public const string DuplicateName = "DUPLICATE_NAME";
    public const string TermOutsideYear = "TERM_OUTSIDE_YEAR";
    public const string TermsOverlap = "TERMS_OVERLAP";
    public const string InvalidTimeRange = "INVALID_TIME_RANGE";
    public const string PeriodsOverlap = "PERIODS_OVERLAP";
    public const string PeriodsNotAscending = "PERIODS_NOT_ASCENDING";
    public const string NoLessonPeriods = "NO_LESSON_PERIODS";
    public const string TooManyPeriods = "TOO_MANY_PERIODS";
    public const string NoWorkingDays = "NO_WORKING_DAYS";
    public const string BlockedPeriodInvalid = "BLOCKED_PERIOD_INVALID";
    public const string MaxPerDayExceedsPeriods = "MAX_PER_DAY_EXCEEDS_PERIODS";
    public const string MaxPerWeekExceedsCapacity = "MAX_PER_WEEK_EXCEEDS_CAPACITY";
    public const string ShiftNotInYear = "SHIFT_NOT_IN_YEAR";
    public const string AssetTooLarge = "ASSET_TOO_LARGE";
    public const string AssetTypeNotAllowed = "ASSET_TYPE_NOT_ALLOWED";
    public const string AssetTypeMismatch = "ASSET_TYPE_MISMATCH";
    public const string YearStructureInUse = "YEAR_STRUCTURE_IN_USE";
    public const string StageArchived = "STAGE_ARCHIVED";
    public const string NoCurrentYear = "NO_CURRENT_YEAR";
    public const string ShiftModeInUse = "SHIFT_MODE_IN_USE";
    public const string CurriculumInUse = "CURRICULUM_IN_USE";
    public const string StageLessonsAboveShift = "STAGE_LESSONS_ABOVE_SHIFT";
    public const string DailyTotalAboveShift = "DAILY_TOTAL_ABOVE_SHIFT";
    public const string ResourceInUse = "RESOURCE_IN_USE";
    public const string WorkloadInUse = "WORKLOAD_IN_USE";
}
