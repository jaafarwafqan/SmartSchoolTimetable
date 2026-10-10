using System.Security.Cryptography;
using System.Text;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain;

namespace SmartSchoolTimetable.Application;

public sealed class LocalAuthService(
    IOwnerRepository ownerRepository,
    ICredentialHasher credentialHasher,
    ILocalSessionStore sessionStore,
    ILoginDelay loginDelay,
    TimeProvider timeProvider,
    TimeSpan? defaultInactivityTimeout,
    SemaphoreSlim operationGate) : ILocalAuthService
{
    public const int RecoveryCodeEntropyBits = 128;
    public static readonly TimeSpan FailedLoginDelay = TimeSpan.FromSeconds(1);

    public async Task<LocalAuthStatus> GetStatusAsync(string? sessionId, CancellationToken cancellationToken)
    {
        var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
        if (owner is null)
            return new LocalAuthStatus(true, false, null, false, defaultInactivityTimeout);

        var timeout = EffectiveTimeout(owner);
        var username = string.Empty;
        var authenticated = sessionId is not null &&
            sessionStore.TryValidateAndTouch(sessionId, timeout, out username);
        return authenticated
            ? new LocalAuthStatus(false, true, username, !owner.RecoveryCodeAcknowledged, timeout)
            : new LocalAuthStatus(false, false, null, !owner.RecoveryCodeAcknowledged, timeout);
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
                return new AuthOperationResult(false, ErrorCodes.SetupAlreadyComplete);

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
                AuditTrail.Entry(now, AuditEvents.OwnerAccountCreated, "owner-account", "Initial owner setup completed."),
                cancellationToken);
            var sessionId = sessionStore.Issue(owner.Username, recoveryCodeIssued: true);
            return new AuthOperationResult(
                true,
                RecoveryCode: recoveryCode,
                SessionId: sessionId,
                InactivityTimeout: EffectiveTimeout(owner));
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
        AuthOperationResult result;
        await operationGate.WaitAsync(cancellationToken);
        try
        {
            result = await VerifyLoginAsync(username, password, cancellationToken);
        }
        finally
        {
            operationGate.Release();
        }

        // The fixed delay runs after the gate is released so a failed attempt never blocks other operations.
        if (result.ErrorCode == ErrorCodes.InvalidCredentials)
            await loginDelay.WaitAsync(FailedLoginDelay, cancellationToken);
        return result;
    }

    private async Task<AuthOperationResult> VerifyLoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
        if (owner is null)
            return new AuthOperationResult(false, ErrorCodes.SetupRequired);

        var normalizedUsername = NormalizeUsername(username);
        var passwordValid = credentialHasher.VerifyPassword(
            password,
            owner.PasswordSalt,
            owner.PasswordHash,
            owner.PasswordIterations);
        var usernameValid = FixedTimeEquals(normalizedUsername, owner.NormalizedUsername);
        if (!passwordValid || !usernameValid)
            return new AuthOperationResult(false, ErrorCodes.InvalidCredentials);

        return new AuthOperationResult(
            true,
            SessionId: sessionStore.Issue(owner.Username),
            InactivityTimeout: EffectiveTimeout(owner));
    }

    public async Task<AuthOperationResult> RegenerateRecoveryCodeAsync(
        string sessionId,
        string currentPassword,
        CancellationToken cancellationToken)
    {
        if (!await IsSessionValidAsync(sessionId, cancellationToken))
            return new AuthOperationResult(false, ErrorCodes.Unauthenticated);

        await operationGate.WaitAsync(cancellationToken);
        try
        {
            var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
            if (owner is null)
                return new AuthOperationResult(false, ErrorCodes.SetupRequired);
            if (!credentialHasher.VerifyPassword(
                    currentPassword,
                    owner.PasswordSalt,
                    owner.PasswordHash,
                    owner.PasswordIterations))
                return new AuthOperationResult(false, ErrorCodes.CurrentPasswordIncorrect);

            var recoveryCode = GenerateRecoveryCode();
            var (salt, hash) = credentialHasher.HashRecoveryCode(NormalizeRecoveryCode(recoveryCode));
            var now = timeProvider.GetUtcNow();
            owner.ReplaceRecoveryCode(salt, hash, now);
            await ownerRepository.SaveOwnerAsync(
                owner,
                AuditTrail.Entry(now, AuditEvents.RecoveryCodeRegenerated, "owner-account", "Recovery code regenerated."),
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
        if (!await IsSessionValidAsync(sessionId, cancellationToken))
            return new AuthOperationResult(false, ErrorCodes.Unauthenticated);
        if (!sessionStore.HasRecoveryCodeIssued(sessionId))
            return new AuthOperationResult(false, ErrorCodes.RecoveryMissing);

        await operationGate.WaitAsync(cancellationToken);
        try
        {
            var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
            if (owner is null)
                return new AuthOperationResult(false, ErrorCodes.SetupRequired);

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
                return new AuthOperationResult(false, ErrorCodes.SetupRequired);

            if (!credentialHasher.VerifyRecoveryCode(
                    NormalizeRecoveryCode(recoveryCode),
                    owner.RecoverySalt,
                    owner.RecoveryCodeHash))
                return new AuthOperationResult(false, ErrorCodes.InvalidRecoveryCode);

            var now = timeProvider.GetUtcNow();
            var (passwordSalt, passwordHash) = credentialHasher.HashPassword(newPassword);
            var replacementCode = GenerateRecoveryCode();
            var (recoverySalt, recoveryHash) = credentialHasher.HashRecoveryCode(NormalizeRecoveryCode(replacementCode));
            owner.ChangePassword(passwordSalt, passwordHash, credentialHasher.CurrentPasswordIterations, now);
            owner.ReplaceRecoveryCode(recoverySalt, recoveryHash, now);
            sessionStore.RevokeAll();

            await ownerRepository.SaveOwnerAsync(
                owner,
                AuditTrail.Entry(now, AuditEvents.PasswordChanged, "owner-account", "Password reset using recovery code."),
                cancellationToken);
            return new AuthOperationResult(
                true,
                RecoveryCode: replacementCode,
                SessionId: sessionStore.Issue(owner.Username, recoveryCodeIssued: true),
                InactivityTimeout: EffectiveTimeout(owner));
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

        if (!await IsSessionValidAsync(sessionId, cancellationToken))
            return new AuthOperationResult(false, ErrorCodes.Unauthenticated);

        await operationGate.WaitAsync(cancellationToken);
        try
        {
            var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
            if (owner is null)
                return new AuthOperationResult(false, ErrorCodes.SetupRequired);

            if (!credentialHasher.VerifyPassword(
                    currentPassword,
                    owner.PasswordSalt,
                    owner.PasswordHash,
                    owner.PasswordIterations))
                return new AuthOperationResult(false, ErrorCodes.CurrentPasswordIncorrect);

            var now = timeProvider.GetUtcNow();
            var (salt, hash) = credentialHasher.HashPassword(newPassword);
            owner.ChangePassword(salt, hash, credentialHasher.CurrentPasswordIterations, now);
            sessionStore.RevokeAll();

            await ownerRepository.SaveOwnerAsync(
                owner,
                AuditTrail.Entry(now, AuditEvents.PasswordChanged, "owner-account", "Owner password changed."),
                cancellationToken);
            return new AuthOperationResult(true);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<AuthOperationResult> SetInactivityTimeoutAsync(
        string sessionId,
        int? minutes,
        CancellationToken cancellationToken)
    {
        if (minutes is { } value && !InactivityTimeoutChoices.Minutes.Contains(value))
            return new AuthOperationResult(false, ErrorCodes.InvalidInactivityTimeout);
        if (!await IsSessionValidAsync(sessionId, cancellationToken))
            return new AuthOperationResult(false, ErrorCodes.Unauthenticated);

        await operationGate.WaitAsync(cancellationToken);
        try
        {
            var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
            if (owner is null)
                return new AuthOperationResult(false, ErrorCodes.SetupRequired);

            var now = timeProvider.GetUtcNow();
            owner.SetInactivityTimeout(minutes, now);
            await ownerRepository.SaveOwnerAsync(
                owner,
                AuditTrail.Entry(now, AuditEvents.InactivityTimeoutChanged, "owner-account", "Inactivity auto-lock changed."),
                cancellationToken);
            return new AuthOperationResult(true, SessionId: sessionId, InactivityTimeout: EffectiveTimeout(owner));
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

    private TimeSpan? EffectiveTimeout(OwnerAccount? owner) =>
        owner is null ? defaultInactivityTimeout : owner.EffectiveInactivityTimeout(defaultInactivityTimeout);

    /// <summary>Validates and touches the session with the current effective timeout, so changes apply immediately.</summary>
    private async Task<bool> IsSessionValidAsync(string sessionId, CancellationToken cancellationToken)
    {
        var owner = await ownerRepository.GetOwnerAsync(cancellationToken);
        return sessionStore.TryValidateAndTouch(sessionId, EffectiveTimeout(owner), out _);
    }

    private static string NormalizeUsername(string value) => value.Trim().ToUpperInvariant();

    private static string NormalizeRecoveryCode(string value) =>
        new(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string? ValidateCredentials(string normalizedUsername, string password) =>
        CredentialRules.IsUsernameValid(normalizedUsername) ? ValidatePassword(password) : ErrorCodes.InvalidUsername;

    private static string? ValidatePassword(string password) =>
        CredentialRules.IsPasswordLengthValid(password) ? null : ErrorCodes.InvalidPassword;

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
