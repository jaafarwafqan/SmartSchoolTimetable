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
    public bool RecoveryCodeAcknowledged { get; private set; }
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

    public void ChangePassword(byte[] salt, byte[] hash, int iterations, DateTimeOffset now)
    {
        PasswordSalt = salt;
        PasswordHash = hash;
        PasswordIterations = iterations;
        UpdatedAt = now;
    }
}
