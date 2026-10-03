namespace SmartSchoolTimetable.Application;

public sealed record AuthOperationResult(
    bool Succeeded,
    string? ErrorCode = null,
    string? RecoveryCode = null,
    string? SessionId = null,
    TimeSpan? RetryAfter = null);

public sealed record LocalAuthStatus(bool SetupRequired, bool Authenticated, string? Username);

public interface ILocalAuthService
{
    Task<LocalAuthStatus> GetStatusAsync(string? sessionId, CancellationToken cancellationToken);
    Task<AuthOperationResult> SetupAsync(string username, string password, CancellationToken cancellationToken);
    Task<AuthOperationResult> LoginAsync(string username, string password, CancellationToken cancellationToken);
    Task<AuthOperationResult> RecoverAsync(string recoveryCode, string newPassword, CancellationToken cancellationToken);
    Task<AuthOperationResult> ChangePasswordAsync(
        string sessionId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);
    void Logout(string? sessionId);
}
