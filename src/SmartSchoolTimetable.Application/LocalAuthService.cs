using System.Security.Cryptography;
using System.Text;
using SmartSchoolTimetable.Domain;

namespace SmartSchoolTimetable.Application;

public sealed class LocalAuthService(
    IOwnerRepository ownerRepository,
    ICredentialHasher credentialHasher,
    ILocalSessionStore sessionStore,
    ILoginDelay loginDelay,
    TimeProvider timeProvider,
    TimeSpan inactivityTimeout,
    SemaphoreSlim operationGate) : ILocalAuthService
{
    public const int RecoveryCodeEntropyBits = 128;

    public async Task<LocalAuthStatus> GetStatusAsync(string? sessionId, CancellationToken cancellationToken)
    {
        var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
        if (owner is null)
            return new LocalAuthStatus(true, false, null);

        var username = string.Empty;
        var authenticated = sessionId is not null &&
            sessionStore.TryValidateAndTouch(sessionId, inactivityTimeout, out username);
        return authenticated
            ? new LocalAuthStatus(false, true, username)
            : new LocalAuthStatus(false, false, null);
    }

    public async Task<AuthOperationResult> SetupAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeUsername(username);
        var validationError = ValidateCredentials(normalizedUsername, password);
        if (validationError is not null)
            return new AuthOperationResult(false, validationError);

        await operationGate.WaitAsync(cancellationToken);
        try
        {
            if (await ownerRepository.GetOwnerAsync(cancellationToken) is not null)
                return new AuthOperationResult(false, "setup_already_complete");

            var now = timeProvider.GetUtcNow();
            var (passwordSalt, passwordHash) = credentialHasher.HashPassword(password);
            var recoveryCode = GenerateRecoveryCode();
            var (recoverySalt, recoveryHash) = credentialHasher.HashRecoveryCode(NormalizeRecoveryCode(recoveryCode));
            var owner = OwnerAccount.Create(
                username.Trim(),
                normalizedUsername,
                passwordSalt,
                passwordHash,
                credentialHasher.CurrentPasswordIterations,
                recoverySalt,
                recoveryHash,
                now);

            await ownerRepository.CreateOwnerAsync(
                owner,
                LocalAuditEntry.Create(now, "OwnerAccountCreated", "owner-account", "Initial owner setup completed."),
                cancellationToken);
            return new AuthOperationResult(true, RecoveryCode: recoveryCode);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<AuthOperationResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken);
        try
        {
            var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
            if (owner is null)
                return new AuthOperationResult(false, "setup_required");

            var now = timeProvider.GetUtcNow();
            if (owner.LockoutUntil is { } lockoutUntil && lockoutUntil > now)
                return new AuthOperationResult(false, "temporarily_locked", RetryAfter: lockoutUntil - now);

            if (owner.NextLoginAllowedAt is { } allowedAt && allowedAt > now)
                await loginDelay.WaitAsync(allowedAt - now, cancellationToken);

            var normalizedUsername = NormalizeUsername(username);
            var passwordValid = credentialHasher.VerifyPassword(
                password,
                owner.PasswordSalt,
                owner.PasswordHash,
                owner.PasswordIterations);
            var usernameValid = FixedTimeEquals(normalizedUsername, owner.NormalizedUsername);

            if (!passwordValid || !usernameValid)
            {
                now = timeProvider.GetUtcNow();
                owner.RecordFailedLogin(now);
                await ownerRepository.SaveOwnerAsync(owner, null, cancellationToken);
                if (owner.LockoutUntil is { } until && until > now)
                    return new AuthOperationResult(false, "temporarily_locked", RetryAfter: until - now);

                var delay = owner.NextLoginAllowedAt is { } next ? next - now : TimeSpan.Zero;
                await loginDelay.WaitAsync(delay, cancellationToken);
                return new AuthOperationResult(false, "invalid_credentials");
            }

            now = timeProvider.GetUtcNow();
            owner.RecordSuccessfulLogin(now);
            await ownerRepository.SaveOwnerAsync(owner, null, cancellationToken);
            var sessionId = sessionStore.Issue(owner.Username);
            return new AuthOperationResult(true, SessionId: sessionId);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<AuthOperationResult> RecoverAsync(
        string recoveryCode,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var validationError = ValidatePassword(newPassword);
        if (validationError is not null)
            return new AuthOperationResult(false, validationError);

        await operationGate.WaitAsync(cancellationToken);
        try
        {
            var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
            if (owner is null)
                return new AuthOperationResult(false, "setup_required");

            if (!credentialHasher.VerifyRecoveryCode(
                    NormalizeRecoveryCode(recoveryCode),
                    owner.RecoverySalt,
                    owner.RecoveryCodeHash))
                return new AuthOperationResult(false, "invalid_recovery_code");

            var now = timeProvider.GetUtcNow();
            var (passwordSalt, passwordHash) = credentialHasher.HashPassword(newPassword);
            var replacementCode = GenerateRecoveryCode();
            var (recoverySalt, recoveryHash) = credentialHasher.HashRecoveryCode(NormalizeRecoveryCode(replacementCode));
            owner.ChangePassword(passwordSalt, passwordHash, credentialHasher.CurrentPasswordIterations, now);
            owner.ReplaceRecoveryCode(recoverySalt, recoveryHash, now);
            sessionStore.RevokeAll();

            await ownerRepository.SaveOwnerAsync(
                owner,
                LocalAuditEntry.Create(now, "PasswordChanged", "owner-account", "Password reset using recovery code."),
                cancellationToken);
            return new AuthOperationResult(true, RecoveryCode: replacementCode);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<AuthOperationResult> ChangePasswordAsync(
        string sessionId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var validationError = ValidatePassword(newPassword);
        if (validationError is not null)
            return new AuthOperationResult(false, validationError);

        if (!sessionStore.TryValidateAndTouch(sessionId, inactivityTimeout, out _))
            return new AuthOperationResult(false, "unauthenticated");

        await operationGate.WaitAsync(cancellationToken);
        try
        {
            var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
            if (owner is null)
                return new AuthOperationResult(false, "setup_required");

            if (!credentialHasher.VerifyPassword(
                    currentPassword,
                    owner.PasswordSalt,
                    owner.PasswordHash,
                    owner.PasswordIterations))
                return new AuthOperationResult(false, "current_password_incorrect");

            var now = timeProvider.GetUtcNow();
            var (salt, hash) = credentialHasher.HashPassword(newPassword);
            owner.ChangePassword(salt, hash, credentialHasher.CurrentPasswordIterations, now);
            sessionStore.RevokeAll();

            await ownerRepository.SaveOwnerAsync(
                owner,
                LocalAuditEntry.Create(now, "PasswordChanged", "owner-account", "Owner password changed."),
                cancellationToken);
            return new AuthOperationResult(true);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public void Logout(string? sessionId)
    {
        if (sessionId is not null)
            sessionStore.Revoke(sessionId);
    }

    private static string NormalizeUsername(string value) => value.Trim().ToUpperInvariant();

    private static string NormalizeRecoveryCode(string value) =>
        new(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string? ValidateCredentials(string username, string password)
    {
        if (username.Length is < 3 or > 64)
            return "invalid_username";
        if (username.Any(char.IsControl))
            return "invalid_username";
        return ValidatePassword(password);
    }

    private static string? ValidatePassword(string password) =>
        password.Length is < 12 or > 1024 ? "invalid_password" : null;

    private static string GenerateRecoveryCode()
    {
        var value = Convert.ToHexString(RandomNumberGenerator.GetBytes(RecoveryCodeEntropyBits / 8));
        return string.Join('-', Enumerable.Range(0, 4).Select(index => value.Substring(index * 8, 8)));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
            CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
