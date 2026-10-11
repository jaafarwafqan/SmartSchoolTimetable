using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.Settings;

/// <summary>
/// Backup configuration (exactly one row, Id = 1, versioned): the folder the owner chose, whether a backup is taken
/// automatically (on start and then daily), and how many automatic backups to keep. Only files the application made
/// itself as automatic backups are ever pruned, and never manual ones.
/// </summary>
public sealed class BackupSettings : VersionedEntity
{
    public const long SingletonId = 1;
    public const int FolderMaxLength = 500;
    public const int DefaultKeepLast = 14;

    /// <summary>How many automatic backups may be kept; 0 means keep them all.</summary>
    public static readonly IReadOnlyList<int> KeepLastChoices = [0, 3, 7, 14, 30];

    private BackupSettings()
    {
    }

    /// <summary>The chosen folder; null while the owner has not chosen one (the default folder is used).</summary>
    public string? Folder { get; private set; }

    public bool AutoBackupEnabled { get; private set; } = true;
    public int KeepLast { get; private set; } = DefaultKeepLast;
    public DateTimeOffset? LastAutoBackupAt { get; private set; }

    /// <summary>The application version that last ran on this database (stored so a backup says which version made it).</summary>
    public string? LastAppVersion { get; private set; }

    public static BackupSettings CreateDefault() => new() { Id = SingletonId };

    /// <summary>Returns false (no new version) when nothing changed.</summary>
    public bool Update(string? folder, bool autoBackupEnabled, int keepLast)
    {
        var trimmed = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim();
        new DomainErrors()
            .When(trimmed is { Length: > FolderMaxLength }, nameof(Folder), DomainErrorCode.TooLong)
            .When(!KeepLastChoices.Contains(keepLast), nameof(KeepLast), DomainErrorCode.InvalidOption)
            .ThrowIfAny();
        if (Folder == trimmed && AutoBackupEnabled == autoBackupEnabled && KeepLast == keepLast)
            return false;
        Folder = trimmed;
        AutoBackupEnabled = autoBackupEnabled;
        KeepLast = keepLast;
        Touch();
        return true;
    }

    public void RecordAutoBackup(DateTimeOffset now)
    {
        LastAutoBackupAt = now;
        Touch();
    }

    /// <summary>Records the running version at start-up. Returns true when it changed.</summary>
    public bool RecordAppVersion(string version)
    {
        if (string.Equals(LastAppVersion, version, StringComparison.Ordinal))
            return false;
        LastAppVersion = version.Length > 40 ? version[..40] : version;
        Touch();
        return true;
    }
}
