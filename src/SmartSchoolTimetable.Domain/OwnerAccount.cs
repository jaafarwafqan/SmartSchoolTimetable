namespace SmartSchoolTimetable.Domain;

public sealed class OwnerAccount
{
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
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? NextLoginAllowedAt { get; private set; }
    public DateTimeOffset? LockoutUntil { get; private set; }
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
            CreatedAt = now,
            UpdatedAt = now
        };

    public void RecordFailedLogin(DateTimeOffset now)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= 5)
        {
            LockoutUntil = now.AddMinutes(15);
            NextLoginAllowedAt = LockoutUntil;
        }
        else
        {
            var delaySeconds = Math.Min(1 << (FailedLoginCount - 1), 30);
            NextLoginAllowedAt = now.AddSeconds(delaySeconds);
        }

        UpdatedAt = now;
    }

    public void RecordSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        NextLoginAllowedAt = null;
        LockoutUntil = null;
        UpdatedAt = now;
    }

    public void ChangePassword(byte[] salt, byte[] hash, int iterations, DateTimeOffset now)
    {
        PasswordSalt = salt;
        PasswordHash = hash;
        PasswordIterations = iterations;
        FailedLoginCount = 0;
        NextLoginAllowedAt = null;
        LockoutUntil = null;
        UpdatedAt = now;
    }

    public void ReplaceRecoveryCode(byte[] salt, byte[] hash, DateTimeOffset now)
    {
        RecoverySalt = salt;
        RecoveryCodeHash = hash;
        UpdatedAt = now;
    }
}
