using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Generation;

namespace SmartSchoolTimetable.Application.Backup;

/// <summary>What a candidate backup file contains (read-only inspection).</summary>
/// <param name="IsDatabase">The file is a readable SQLite database of this application (it has a migration history).</param>
/// <param name="Migrations">Migrations applied to it.</param>
/// <param name="HasOwner">It holds the owner account (a restore must never lead to a first-run screen).</param>
public sealed record BackupInspection(bool IsDatabase, IReadOnlyList<string> Migrations, bool HasOwner);

/// <summary>The local SQLite database file operations behind «النسخ الاحتياطي» (Infrastructure, ADR 0042).</summary>
public interface IDatabaseBackup
{
    /// <summary>Folder next to the database for automatic backups taken before a restore.</summary>
    string AutomaticBackupFolder { get; }

    /// <summary>Migrations this build knows, oldest first.</summary>
    IReadOnlyList<string> KnownMigrations { get; }

    /// <summary>Writes a consistent copy of the live database to a NEW file (never overwrites).</summary>
    Task CreateAsync(string targetFile, CancellationToken token);

    Task<BackupInspection> InspectAsync(string file, CancellationToken token);

    /// <summary>Copies the backup into the live database (online backup API) and brings it to the current schema.</summary>
    Task RestoreAsync(string file, CancellationToken token);
}

public sealed record CreateBackupCommand(string? Folder);

/// <param name="Confirm">First confirmation: «أريد الاستعادة».</param>
/// <param name="ConfirmReplace">Second confirmation: «أفهم أن البيانات الحالية ستُستبدل».</param>
public sealed record RestoreBackupCommand(string? FilePath, bool Confirm, bool ConfirmReplace);

public sealed record BackupResultDto(string FilePath, long SizeBytes, DateTimeOffset CreatedAt);

public sealed record RestoreResultDto(string RestoredFrom, string AutomaticBackupPath);

public sealed record BackupDefaultsDto(string SuggestedFolder);

/// <summary>
/// «النسخ الاحتياطي والاستعادة» (Phase 4 M6). A backup is a new file in a folder the owner chooses; a restore checks
/// the file, takes an automatic backup of the current data first, replaces the data, and signs the owner out. No
/// backup file is ever deleted or overwritten.
/// </summary>
public sealed class BackupService(IDatabaseBackup backup, IDataStore store, ILocalSessionStore sessions, TimeProvider clock)
{
    private const string FolderField = "folder";
    private const string FileField = "filePath";

    public static BackupDefaultsDto Defaults() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SmartSchoolTimetable", "Backups"));

    public async Task<OperationResult<BackupResultDto>> CreateAsync(CreateBackupCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!IsUsablePath(command.Folder))
            return OperationResult.Invalid<BackupResultDto>(FolderField, ErrorCodes.BackupPathInvalid);
        var now = clock.GetUtcNow();
        try
        {
            Directory.CreateDirectory(command.Folder!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return OperationResult.Invalid<BackupResultDto>(FolderField, ErrorCodes.BackupPathInvalid);
        }
        var file = Path.Combine(command.Folder!, $"timetable-backup-{now.ToLocalTime():yyyyMMdd-HHmmss}.db");
        if (File.Exists(file))
            return OperationResult.Failure<BackupResultDto>(ErrorCodes.BackupFileExists);
        try
        {
            await backup.CreateAsync(file, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return OperationResult.Failure<BackupResultDto>(ErrorCodes.BackupFailed);
        }
        AuditTrail.Record(store, clock, "BackupCreated", "database", "A database backup was created.");
        await store.SaveChangesAsync(token);
        return OperationResult.Success(new BackupResultDto(file, new FileInfo(file).Length, now));
    }

    public async Task<OperationResult<RestoreResultDto>> RestoreAsync(RestoreBackupCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Confirm || !command.ConfirmReplace)
            return OperationResult.Failure<RestoreResultDto>(ErrorCodes.RestoreConfirmationRequired);
        if (!IsUsablePath(command.FilePath) || !File.Exists(command.FilePath))
            return OperationResult.Invalid<RestoreResultDto>(FileField, ErrorCodes.BackupPathInvalid);
        var inspection = await backup.InspectAsync(command.FilePath!, token);
        if (!inspection.IsDatabase || !inspection.HasOwner)
            return OperationResult.Failure<RestoreResultDto>(ErrorCodes.RestoreFileInvalid);
        // Compatible: every migration in the file is one this build knows (an older file is upgraded after the copy).
        if (inspection.Migrations.Count == 0 || inspection.Migrations.Any(migration => !backup.KnownMigrations.Contains(migration)))
            return OperationResult.Failure<RestoreResultDto>(ErrorCodes.RestoreIncompatible);

        // Never while a generation is active: it would write into the data being replaced.
        var active = GenerationRun.ActiveStatuses.ToArray();
        if (await store.AnyAsync(store.Read<GenerationRun>().Where(run => active.Contains(run.Status)), token))
            return OperationResult.Failure<RestoreResultDto>(ErrorCodes.GenerationActive);
        Directory.CreateDirectory(backup.AutomaticBackupFolder);
        var automatic = Path.Combine(backup.AutomaticBackupFolder, $"pre-restore-{clock.GetUtcNow().ToLocalTime():yyyyMMdd-HHmmss}.db");
        if (File.Exists(automatic))
            return OperationResult.Failure<RestoreResultDto>(ErrorCodes.BackupFileExists);
        await backup.CreateAsync(automatic, token);
        await backup.RestoreAsync(command.FilePath!, token);
        sessions.RevokeAll();
        return OperationResult.Success(new RestoreResultDto(command.FilePath!, automatic));
    }

    private static bool IsUsablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return false;
        try
        {
            return Path.IsPathFullyQualified(path);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
