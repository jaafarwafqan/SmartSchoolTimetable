namespace SmartSchoolTimetable.Application;

/// <param name="InactivityTimeout">Effective timeout for the issued or refreshed session (null means it never expires).</param>
public sealed record AuthOperationResult(
    bool Succeeded,
    string? ErrorCode = null,
    string? RecoveryCode = null,
    string? SessionId = null,
    TimeSpan? InactivityTimeout = null);

/// <param name="InactivityTimeout">Effective inactivity timeout: the owner preference, else the configured default.</param>
public sealed record LocalAuthStatus(
    bool SetupRequired,
    bool Authenticated,
    string? Username,
    bool RecoveryCodeAcknowledgementRequired,
    TimeSpan? InactivityTimeout);

public interface ILocalAuthService
{
    Task<LocalAuthStatus> GetStatusAsync(string? sessionId, CancellationToken cancellationToken);
    Task<AuthOperationResult> SetupAsync(string username, string password, CancellationToken cancellationToken);
    Task<AuthOperationResult> LoginAsync(string username, string password, CancellationToken cancellationToken);
    Task<AuthOperationResult> RecoverAsync(string recoveryCode, string newPassword, CancellationToken cancellationToken);
    Task<AuthOperationResult> RegenerateRecoveryCodeAsync(
        string sessionId,
        string currentPassword,
        CancellationToken cancellationToken);
    Task<AuthOperationResult> AcknowledgeRecoveryCodeAsync(string sessionId, CancellationToken cancellationToken);
    Task<AuthOperationResult> ChangePasswordAsync(
        string sessionId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);
    /// <summary>M2: changes the username after checking the current password; the session stays signed in.</summary>
    Task<AuthOperationResult> ChangeUsernameAsync(
        string sessionId,
        string currentPassword,
        string newUsername,
        CancellationToken cancellationToken);
    /// <param name="minutes">One of the allowed choices, or null for "never".</param>
    Task<AuthOperationResult> SetInactivityTimeoutAsync(
        string sessionId,
        int? minutes,
        CancellationToken cancellationToken);
    void Logout(string? sessionId);
}
