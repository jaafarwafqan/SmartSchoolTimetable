namespace SmartSchoolTimetable.Api;

public sealed record BootstrapResponse(
    bool SetupRequired,
    bool Authenticated,
    string? Username,
    bool RecoveryCodeAcknowledgementRequired,
    string LaunchToken,
    int? InactivityTimeoutMinutes);

public sealed record SetupRequest(string? Username, string? Password, string? ConfirmPassword);
public sealed record SetupResponse(string RecoveryCode);
public sealed record LoginRequest(string? Username, string? Password);
public sealed record RecoveryRequest(string? RecoveryCode, string? NewPassword);
public sealed record RecoveryCodeRequest(string? CurrentPassword);
public sealed record RecoveryResponse(string RecoveryCode);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public sealed record PrivateStatusResponse(string Status);
public sealed record ValidationIssue(string Field, string Code);
public sealed record ApiErrorResponse(string Code, string CorrelationId, IReadOnlyList<ValidationIssue> Errors);
