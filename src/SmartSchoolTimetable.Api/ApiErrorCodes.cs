using SmartSchoolTimetable.Application;

namespace SmartSchoolTimetable.Api;

/// <summary>Maps every <see cref="ErrorCodes"/> value to the HTTP status the API returns for it.</summary>
public static class ApiErrorCodes
{
    public static readonly IReadOnlyDictionary<string, int> StatusByCode =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ErrorCodes.InvalidHost] = StatusCodes.Status400BadRequest,
            [ErrorCodes.InvalidRequest] = StatusCodes.Status400BadRequest,

            [ErrorCodes.InvalidCredentials] = StatusCodes.Status401Unauthorized,
            [ErrorCodes.InvalidRecoveryCode] = StatusCodes.Status401Unauthorized,
            [ErrorCodes.CurrentPasswordIncorrect] = StatusCodes.Status401Unauthorized,
            [ErrorCodes.Unauthenticated] = StatusCodes.Status401Unauthorized,

            [ErrorCodes.InvalidOrigin] = StatusCodes.Status403Forbidden,
            [ErrorCodes.InvalidLaunchToken] = StatusCodes.Status403Forbidden,
            [ErrorCodes.RequestForbidden] = StatusCodes.Status403Forbidden,
            [ErrorCodes.SetupRequired] = StatusCodes.Status403Forbidden,

            [ErrorCodes.NotFound] = StatusCodes.Status404NotFound,
            [ErrorCodes.MethodNotAllowed] = StatusCodes.Status405MethodNotAllowed,

            [ErrorCodes.RecoveryMissing] = StatusCodes.Status409Conflict,
            [ErrorCodes.SetupAlreadyComplete] = StatusCodes.Status409Conflict,
            [ErrorCodes.Conflict] = StatusCodes.Status409Conflict,

            [ErrorCodes.UnsupportedMediaType] = StatusCodes.Status415UnsupportedMediaType,

            [ErrorCodes.ValidationFailed] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.InvalidPassword] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.InvalidUsername] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.Required] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.UsernameTooShort] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.UsernameTooLong] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.PasswordTooShort] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.PasswordTooLong] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.PasswordMismatch] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.InvalidInactivityTimeout] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.ValueTooLong] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.ValueOutOfRange] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.InvalidOption] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.InvalidDate] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.InvalidTime] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.InvalidDateRange] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.DuplicateName] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.TermOutsideYear] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.TermsOverlap] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.InvalidTimeRange] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.PeriodsOverlap] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.PeriodsNotAscending] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.NoLessonPeriods] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.TooManyPeriods] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.NoWorkingDays] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.BlockedPeriodInvalid] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.MaxPerDayExceedsPeriods] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.MaxPerWeekExceedsCapacity] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.ShiftNotInYear] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.AssetTooLarge] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.AssetTypeNotAllowed] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.AssetTypeMismatch] = StatusCodes.Status422UnprocessableEntity,
            [ErrorCodes.YearStructureInUse] = StatusCodes.Status409Conflict,
            [ErrorCodes.StageArchived] = StatusCodes.Status409Conflict,
            [ErrorCodes.NoCurrentYear] = StatusCodes.Status409Conflict,
            [ErrorCodes.ShiftModeInUse] = StatusCodes.Status409Conflict,

            [ErrorCodes.RecordInUse] = StatusCodes.Status409Conflict,
            [ErrorCodes.CurrentYearRequired] = StatusCodes.Status409Conflict,
            [ErrorCodes.PayloadTooLarge] = StatusCodes.Status413PayloadTooLarge,

            [ErrorCodes.TooManyRequests] = StatusCodes.Status429TooManyRequests,
            [ErrorCodes.InternalError] = StatusCodes.Status500InternalServerError
        };

    public static IReadOnlyCollection<string> All { get; } = [.. StatusByCode.Keys];

    /// <summary>Returns the mapped HTTP status, or <c>null</c> when the code is not registered.</summary>
    public static int? StatusFor(string code) =>
        StatusByCode.TryGetValue(code, out var status) ? status : null;

    public static string CodeForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.InvalidRequest,
        StatusCodes.Status401Unauthorized => ErrorCodes.Unauthenticated,
        StatusCodes.Status403Forbidden => ErrorCodes.RequestForbidden,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status405MethodNotAllowed => ErrorCodes.MethodNotAllowed,
        StatusCodes.Status409Conflict => ErrorCodes.Conflict,
        StatusCodes.Status413PayloadTooLarge => ErrorCodes.PayloadTooLarge,
        StatusCodes.Status415UnsupportedMediaType => ErrorCodes.UnsupportedMediaType,
        StatusCodes.Status422UnprocessableEntity => ErrorCodes.ValidationFailed,
        StatusCodes.Status429TooManyRequests => ErrorCodes.TooManyRequests,
        _ => ErrorCodes.InternalError
    };
}
