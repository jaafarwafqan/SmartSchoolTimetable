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
}
