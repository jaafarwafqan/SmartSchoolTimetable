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
    TimeSpan? inactivityTimeout,
    SemaphoreSlim operationGate) : ILocalAuthService
{
    public const int RecoveryCodeEntropyBits = 128;

    public async Task<LocalAuthStatus> GetStatusAsync(string? sessionId, CancellationToken cancellationToken)
    {
        var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
        if (owner is null)
            return new LocalAuthStatus(true, false, null, false);

        var username = string.Empty;
        var authenticated = sessionId is not null &&
            sessionStore.TryValidateAndTouch(sessionId, inactivityTimeout, out username);
        return authenticated
            ? new LocalAuthStatus(false, true, username, !owner.RecoveryCodeAcknowledged)
            : new LocalAuthStatus(false, false, null, !owner.RecoveryCodeAcknowledged);
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
                return new AuthOperationResult(false, "SETUP_ALREADY_COMPLETE");

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
            var sessionId = sessionStore.Issue(owner.Username, recoveryCodeIssued: true);
            return new AuthOperationResult(true, RecoveryCode: recoveryCode, SessionId: sessionId);
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
                return new AuthOperationResult(false, "SETUP_REQUIRED");

            var normalizedUsername = NormalizeUsername(username);
            var passwordValid = credentialHasher.VerifyPassword(
                password,
                owner.PasswordSalt,
                owner.PasswordHash,
                owner.PasswordIterations);
            var usernameValid = FixedTimeEquals(normalizedUsername, owner.NormalizedUsername);

            if (!passwordValid || !usernameValid)
            {
                await loginDelay.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
                return new AuthOperationResult(false, "INVALID_CREDENTIALS");
            }

            var sessionId = sessionStore.Issue(owner.Username);
            return new AuthOperationResult(true, SessionId: sessionId);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<AuthOperationResult> RegenerateRecoveryCodeAsync(
        string sessionId,
        string currentPassword,
        CancellationToken cancellationToken)
    {
        if (!sessionStore.TryValidateAndTouch(sessionId, inactivityTimeout, out _))
            return new AuthOperationResult(false, "UNAUTHENTICATED");

        await operationGate.WaitAsync(cancellationToken);
        try
        {
            var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
            if (owner is null)
                return new AuthOperationResult(false, "SETUP_REQUIRED");
            if (!credentialHasher.VerifyPassword(
                    currentPassword,
                    owner.PasswordSalt,
                    owner.PasswordHash,
                    owner.PasswordIterations))
                return new AuthOperationResult(false, "CURRENT_PASSWORD_INCORRECT");

            var recoveryCode = GenerateRecoveryCode();
            var (salt, hash) = credentialHasher.HashRecoveryCode(NormalizeRecoveryCode(recoveryCode));
            var now = timeProvider.GetUtcNow();
            owner.ReplaceRecoveryCode(salt, hash, now);
            await ownerRepository.SaveOwnerAsync(
                owner,
                LocalAuditEntry.Create(now, "RecoveryCodeRegenerated", "owner-account", "Recovery code regenerated."),
                cancellationToken);
            sessionStore.MarkRecoveryCodeIssued(sessionId);
            return new AuthOperationResult(true, RecoveryCode: recoveryCode);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<AuthOperationResult> AcknowledgeRecoveryCodeAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (!sessionStore.TryValidateAndTouch(sessionId, inactivityTimeout, out _))
            return new AuthOperationResult(false, "UNAUTHENTICATED");
        if (!sessionStore.HasRecoveryCodeIssued(sessionId))
            return new AuthOperationResult(false, "RECOVERY_MISSING");

        await operationGate.WaitAsync(cancellationToken);
        try
        {
            var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
            if (owner is null)
                return new AuthOperationResult(false, "SETUP_REQUIRED");

            var now = timeProvider.GetUtcNow();
            owner.AcknowledgeRecoveryCode(now);
            await ownerRepository.SaveOwnerAsync(owner, null, cancellationToken);
            return new AuthOperationResult(true);
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
                return new AuthOperationResult(false, "SETUP_REQUIRED");

            if (!credentialHasher.VerifyRecoveryCode(
                    NormalizeRecoveryCode(recoveryCode),
                    owner.RecoverySalt,
                    owner.RecoveryCodeHash))
                return new AuthOperationResult(false, "INVALID_RECOVERY_CODE");

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
            return new AuthOperationResult(
                true,
                RecoveryCode: replacementCode,
                SessionId: sessionStore.Issue(owner.Username, recoveryCodeIssued: true));
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
                return new AuthOperationResult(false, "SETUP_REQUIRED");

            if (!credentialHasher.VerifyPassword(
                    currentPassword,
                    owner.PasswordSalt,
                    owner.PasswordHash,
                    owner.PasswordIterations))
                return new AuthOperationResult(false, "CURRENT_PASSWORD_INCORRECT");

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
            return "INVALID_USERNAME";
        if (username.Any(char.IsControl))
            return "INVALID_USERNAME";
        return ValidatePassword(password);
    }

    private static string? ValidatePassword(string password) =>
        password.Length is < 12 or > 1024 ? "INVALID_PASSWORD" : null;

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
