namespace SmartSchoolTimetable.Domain;

public sealed class OwnerAccount
{
    /// <summary>Inactivity auto-lock choices (minutes) the owner may select; "never" is represented by <c>null</c>.</summary>
    public static readonly IReadOnlyList<int> InactivityTimeoutChoicesMinutes = [5, 15, 30, 60];

    private OwnerAccount()
    {
    }

    public long Id { get; private set; }
    public string OwnerSlot { get; private set; } = "owner";
    public string Username { get; private set; } = string.Empty;
    public string NormalizedUsername { get; private set; } = string.Empty;
    public byte[] PasswordSalt { get; private set; } = [];
    public byte[] PasswordHash { get; private set; } = [];
    public int PasswordIterations { get; private set; }
    public byte[] RecoverySalt { get; private set; } = [];
    public byte[] RecoveryCodeHash { get; private set; } = [];
    public bool RecoveryCodeAcknowledged { get; private set; }

    /// <summary>False until the owner picks a value; the configured application default applies meanwhile.</summary>
    public bool HasCustomInactivityTimeout { get; private set; }

    /// <summary>The chosen timeout in minutes; <c>null</c> together with a custom setting means "never lock".</summary>
    public int? InactivityTimeoutMinutes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static OwnerAccount Create(
        string username,
        string normalizedUsername,
        byte[] passwordSalt,
        byte[] passwordHash,
        int passwordIterations,
        byte[] recoverySalt,
        byte[] recoveryCodeHash,
        DateTimeOffset now) =>
        new()
        {
            OwnerSlot = "owner",
            Username = username,
            NormalizedUsername = normalizedUsername,
            PasswordSalt = passwordSalt,
            PasswordHash = passwordHash,
            PasswordIterations = passwordIterations,
            RecoverySalt = recoverySalt,
            RecoveryCodeHash = recoveryCodeHash,
            RecoveryCodeAcknowledged = false,
            CreatedAt = now,
            UpdatedAt = now
        };

    /// <summary>M2: the owner's new username (the password and recovery code are unchanged).</summary>
    public void ChangeUsername(string username, string normalizedUsername, DateTimeOffset now)
    {
        Username = username;
        NormalizedUsername = normalizedUsername;
        UpdatedAt = now;
    }

    public void AcknowledgeRecoveryCode(DateTimeOffset now)
    {
        RecoveryCodeAcknowledged = true;
        UpdatedAt = now;
    }

    public void ReplaceRecoveryCode(byte[] salt, byte[] hash, DateTimeOffset now)
    {
        RecoverySalt = salt;
        RecoveryCodeHash = hash;
        RecoveryCodeAcknowledged = false;
        UpdatedAt = now;
    }

    public void SetInactivityTimeout(int? minutes, DateTimeOffset now)
    {
        if (minutes is { } value && !InactivityTimeoutChoicesMinutes.Contains(value))
            throw new ArgumentOutOfRangeException(nameof(minutes), value, "Unsupported inactivity timeout.");
        HasCustomInactivityTimeout = true;
        InactivityTimeoutMinutes = minutes;
        UpdatedAt = now;
    }

    public TimeSpan? EffectiveInactivityTimeout(TimeSpan? configuredDefault) =>
        !HasCustomInactivityTimeout
            ? configuredDefault
            : InactivityTimeoutMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null;

    public void ChangePassword(byte[] salt, byte[] hash, int iterations, DateTimeOffset now)
    {
        PasswordSalt = salt;
        PasswordHash = hash;
        PasswordIterations = iterations;
        UpdatedAt = now;
    }
}
