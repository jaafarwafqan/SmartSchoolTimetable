using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Generation;
using SmartSchoolTimetable.Domain.Settings;

namespace SmartSchoolTimetable.Application.Backup;

/// <summary>What a candidate backup file contains (read-only inspection).</summary>
/// <param name="IsDatabase">The file is a readable SQLite database of this application (it has a migration history).</param>
/// <param name="Migrations">Migrations applied to it.</param>
/// <param name="HasOwner">It holds the owner account (a restore must never lead to a first-run screen).</param>
/// <param name="IntegrityOk">SQLite's <c>PRAGMA integrity_check</c> on the copy returned "ok".</param>
/// <param name="AppVersion">The application version that last ran on the database the copy came from (null for older copies).</param>
public sealed record BackupInspection(bool IsDatabase, IReadOnlyList<string> Migrations, bool HasOwner, bool IntegrityOk = true, string? AppVersion = null);

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
/// The folder suggested for backups until the owner chooses one: «Documents\SmartSchoolTimetable\Backups» when the application
/// uses its normal database location, otherwise a folder next to the database in use (so a test or portable copy never writes
/// into the user's Documents).
/// </summary>
public sealed record BackupDefaults(string SuggestedFolder);

/// <summary>A subfolder in the folder picker (MF10).</summary>
public sealed record FolderEntryDto(string Name, string Path);

/// <param name="Kind">"documents", "desktop" or "drive": the starting places shown before any folder is opened.</param>
public sealed record FolderPlaceDto(string Kind, string Name, string Path);

/// <summary>
/// The folder picker's view of one folder (MF10): its subfolders only (never files), where to go up to, or, with no
/// path, the starting places (documents, desktop, drives).
/// </summary>
/// <param name="Path">The listed folder, or null at the starting places.</param>
/// <param name="Parent">The folder above, or null at a drive root (up goes back to the starting places).</param>
public sealed record FolderListingDto(string? Path, string? Parent, IReadOnlyList<FolderEntryDto> Folders, IReadOnlyList<FolderPlaceDto> Places);

/// <param name="Kind">"manual" (made with «إنشاء نسخة»), "auto" (the scheduled one), "preRestore", "preConversion", "preUpgrade" or "other".</param>
/// <param name="Source">"folder" (the chosen folder) or "automatic" (the folder next to the database).</param>
/// <param name="Restorable">A readable database of this app that passed the integrity check, with an owner account and only migrations this build knows.</param>
/// <param name="Integrity">"ok", "failed" (the integrity check found damage) or "unreadable" (not a database of this app).</param>
/// <param name="AppVersion">The application version that last ran on the database the copy was made from (null for older copies).</param>
/// <param name="SchemaVersion">How many migrations the copy has applied (compare with <see cref="BackupListingDto.CurrentSchemaVersion"/>).</param>
public sealed record BackupFileDto(string Name, string Path, long SizeBytes, DateTimeOffset ModifiedAt, string Kind, string Source, bool Restorable,
    string Integrity = "ok", string? AppVersion = null, int? SchemaVersion = null);

public sealed record BackupListingDto(string Folder, string AutomaticFolder, IReadOnlyList<BackupFileDto> Files, int CurrentSchemaVersion = 0);

/// <param name="Folder">The folder the owner chose, or null while the suggested one is used.</param>
/// <param name="KeepLast">How many automatic backups to keep (0 keeps them all).</param>
public sealed record BackupSettingsDto(string? Folder, string EffectiveFolder, string SuggestedFolder, bool AutoBackupEnabled, int KeepLast,
    IReadOnlyList<int> KeepLastChoices, DateTimeOffset? LastAutoBackupAt, int Version);

public sealed record SaveBackupSettingsCommand(string? Folder, bool AutoBackupEnabled, int KeepLast, int Version);

/// <summary>What one automatic-backup check did: nothing (disabled, not due, no owner yet) or a new file and how many old ones were pruned.</summary>
public sealed record AutoBackupOutcome(bool Created, string? File, int Pruned);

/// <summary>
/// «النسخ الاحتياطي والاستعادة» (Phase 4 M6). A backup is a new file in a folder the owner chooses; a restore checks
/// the file, takes an automatic backup of the current data first, replaces the data, and signs the owner out. No
/// backup file is ever deleted or overwritten.
/// </summary>
public sealed class BackupService(IDatabaseBackup backup, IDataStore store, ILocalSessionStore sessions, TimeProvider clock, BackupDefaults defaults)
{
    private const string FolderField = "folder";
    private const string FileField = "filePath";

    public BackupDefaultsDto Defaults() => new(defaults.SuggestedFolder);

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
        AuditTrail.Record(store, clock, AuditEvents.BackupCreated, "database", "A database backup was created.");
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
        if (!inspection.IsDatabase || !inspection.HasOwner || !inspection.IntegrityOk)
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
        try
        {
            await backup.CreateAsync(automatic, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return OperationResult.Failure<RestoreResultDto>(ErrorCodes.BackupFailed); // nothing was touched
        }
        try
        {
            await backup.RestoreAsync(command.FilePath!, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // The copy failed part-way: put the data back from the automatic backup taken a moment ago, so a failed restore loses nothing.
            await backup.RestoreAsync(automatic, CancellationToken.None);
            return OperationResult.Failure<RestoreResultDto>(ErrorCodes.RestoreFailed);
        }
        // The data was replaced, so the entry goes into the restored history (the old rows are not in it).
        AuditTrail.Record(store, clock, AuditEvents.BackupRestored, "database", "A database backup was restored.");
        await store.SaveChangesAsync(token);
        sessions.RevokeAll();
        return OperationResult.Success(new RestoreResultDto(command.FilePath!, automatic));
    }

    /// <summary>MF10: the subfolders of a folder for the picker; with no path, the starting places. Files are never listed.</summary>
    public static OperationResult<FolderListingDto> BrowseFolders(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            var places = new List<FolderPlaceDto>();
            AddPlace(places, "documents", Environment.SpecialFolder.MyDocuments);
            AddPlace(places, "desktop", Environment.SpecialFolder.DesktopDirectory);
            try
            {
                places.AddRange(DriveInfo.GetDrives().Where(drive => drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network)
                    .Select(drive => new FolderPlaceDto("drive", drive.Name, drive.RootDirectory.FullName)));
            }
            catch (IOException)
            {
                // A drive that vanishes while listing is simply not offered.
            }
            return OperationResult.Success(new FolderListingDto(null, null, [], places));
        }
        if (!IsUsablePath(path) || !Directory.Exists(path))
            return OperationResult.Invalid<FolderListingDto>("path", ErrorCodes.BackupPathInvalid);
        var folders = new List<FolderEntryDto>();
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(path).Select(item => new DirectoryInfo(item))
                         .Where(item => !item.Attributes.HasFlag(FileAttributes.Hidden) && !item.Attributes.HasFlag(FileAttributes.System))
                         .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).Take(MaxFolders))
                folders.Add(new FolderEntryDto(directory.Name, directory.FullName));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return OperationResult.Invalid<FolderListingDto>("path", ErrorCodes.BackupPathInvalid);
        }
        return OperationResult.Success(new FolderListingDto(Path.GetFullPath(path), Directory.GetParent(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.FullName, folders, []));
    }

    /// <summary>
    /// MF10: the backups the owner can restore: the database files of the chosen folder plus the automatic ones kept next to
    /// the database, newest first. A file that is not usable is still listed (with <c>Restorable = false</c>) so the owner sees it.
    /// </summary>
    public async Task<OperationResult<BackupListingDto>> ListFilesAsync(string? folder, CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(folder) && !IsUsablePath(folder))
            return OperationResult.Invalid<BackupListingDto>(FolderField, ErrorCodes.BackupPathInvalid);
        var files = new List<BackupFileDto>();
        if (!string.IsNullOrWhiteSpace(folder))
            files.AddRange(await ReadFolderAsync(folder, "folder", token));
        if (!string.Equals(Path.TrimEndingDirectorySeparator(folder ?? string.Empty), Path.TrimEndingDirectorySeparator(backup.AutomaticBackupFolder), StringComparison.OrdinalIgnoreCase))
            files.AddRange(await ReadFolderAsync(backup.AutomaticBackupFolder, "automatic", token));
        return OperationResult.Success(new BackupListingDto(folder ?? string.Empty, backup.AutomaticBackupFolder, files.OrderByDescending(file => file.ModifiedAt).ToList(), backup.KnownMigrations.Count));
    }

    /// <summary>The scheduled backups; only files with this prefix are ever pruned (never a manual, pre-restore, pre-conversion or pre-upgrade copy).</summary>
    public const string AutoPrefix = "auto-backup-";

    /// <summary>An automatic backup is due once a day.</summary>
    private static readonly TimeSpan AutoInterval = TimeSpan.FromHours(20);

    public async Task<BackupSettingsDto> GetSettingsAsync(CancellationToken token) => ToDto(await LoadSettingsAsync(token));

    public async Task<OperationResult<BackupSettingsDto>> UpdateSettingsAsync(SaveBackupSettingsCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = new InputErrors();
        var folder = string.IsNullOrWhiteSpace(command.Folder) ? null : command.Folder.Trim();
        if (folder is not null && !IsUsablePath(folder))
            input.Add("Folder", ErrorCodes.BackupPathInvalid);
        if (!BackupSettings.KeepLastChoices.Contains(command.KeepLast))
            input.Add("KeepLast", ErrorCodes.InvalidOption);
        if (input.Any)
            return input.ToResult<BackupSettingsDto>();
        var settings = await LoadSettingsAsync(token);
        if (!settings.IsVersion(command.Version))
            return OperationResult.Failure<BackupSettingsDto>(ErrorCodes.Conflict);
        var changed = false;
        try
        {
            changed = settings.Update(folder, command.AutoBackupEnabled, command.KeepLast);
        }
        catch (DomainValidationException exception)
        {
            return OperationResult.FromDomain<BackupSettingsDto>(exception);
        }
        if (changed)
            AuditTrail.Record(store, clock, AuditEvents.BackupSettingsUpdated, "backup-settings", "Backup settings updated.");
        return await store.SaveAsync(() => ToDto(settings), "Folder", token);
    }

    /// <summary>
    /// The automatic backup (on start and then daily): a consistent copy into the chosen folder, then the oldest automatic copies beyond the
    /// number to keep are removed. Does nothing when switched off, when one was made in the last 20 hours, or before the owner account exists.
    /// A failure is reported to the caller (the background worker logs it) and never stops the application.
    /// </summary>
    public async Task<AutoBackupOutcome> RunAutoBackupAsync(CancellationToken token)
    {
        var settings = await LoadSettingsAsync(token);
        var now = clock.GetUtcNow();
        if (!settings.AutoBackupEnabled || (settings.LastAutoBackupAt is { } last && now - last < AutoInterval))
            return new AutoBackupOutcome(false, null, 0);
        if (!await store.AnyAsync(store.Read<OwnerAccount>(), token))
            return new AutoBackupOutcome(false, null, 0);

        var folder = settings.Folder ?? defaults.SuggestedFolder;
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"{AutoPrefix}{now.ToLocalTime():yyyyMMdd-HHmmss}.db");
        if (File.Exists(file))
            return new AutoBackupOutcome(false, null, 0);
        await backup.CreateAsync(file, token);
        settings.RecordAutoBackup(now);
        AuditTrail.Record(store, clock, AuditEvents.AutoBackupCreated, "database", "An automatic database backup was created.");
        var pruned = settings.KeepLast > 0 ? PruneAutomatic(folder, settings.KeepLast) : 0;
        if (pruned > 0)
            AuditTrail.Record(store, clock, AuditEvents.AutoBackupPruned, "database", $"{pruned} old automatic backups removed.", new { count = pruned });
        await store.SaveChangesAsync(token);
        return new AutoBackupOutcome(true, file, pruned);
    }

    /// <summary>Removes the oldest <c>auto-backup-*.db</c> files of the folder beyond <paramref name="keep"/>; nothing else is touched.</summary>
    private static int PruneAutomatic(string folder, int keep)
    {
        var removed = 0;
        try
        {
            var automatic = new DirectoryInfo(folder).EnumerateFiles(AutoPrefix + "*.db").OrderByDescending(file => file.LastWriteTimeUtc).ThenByDescending(file => file.Name, StringComparer.Ordinal).ToList();
            foreach (var old in automatic.Skip(keep))
            {
                old.Delete();
                removed++;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A locked or protected file stays; the next run tries again.
        }
        return removed;
    }

    private async Task<BackupSettings> LoadSettingsAsync(CancellationToken token)
    {
        if (await store.FirstOrDefaultAsync(store.Query<BackupSettings>(), token) is { } settings)
            return settings;
        settings = BackupSettings.CreateDefault();
        store.Add(settings);
        await store.SaveChangesAsync(token);
        return settings;
    }

    private BackupSettingsDto ToDto(BackupSettings settings) => new(settings.Folder, settings.Folder ?? defaults.SuggestedFolder, defaults.SuggestedFolder,
        settings.AutoBackupEnabled, settings.KeepLast, BackupSettings.KeepLastChoices, settings.LastAutoBackupAt, settings.Version);

    private const int MaxFolders = 500;
    private const int MaxBackupFiles = 200;

    private static void AddPlace(List<FolderPlaceDto> places, string kind, Environment.SpecialFolder folder)
    {
        var path = Environment.GetFolderPath(folder);
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            places.Add(new FolderPlaceDto(kind, Path.GetFileName(path), path));
    }

    private async Task<List<BackupFileDto>> ReadFolderAsync(string folder, string source, CancellationToken token)
    {
        var result = new List<BackupFileDto>();
        if (!Directory.Exists(folder))
            return result;
        List<FileInfo> candidates;
        try
        {
            candidates = new DirectoryInfo(folder).EnumerateFiles("*.db").OrderByDescending(file => file.LastWriteTimeUtc).Take(MaxBackupFiles).ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return result;
        }
        foreach (var file in candidates)
        {
            var restorable = false;
            var integrity = "unreadable";
            string? appVersion = null;
            int? schemaVersion = null;
            try
            {
                var inspection = await backup.InspectAsync(file.FullName, token);
                if (inspection.IsDatabase)
                {
                    integrity = inspection.IntegrityOk ? "ok" : "failed";
                    appVersion = inspection.AppVersion;
                    schemaVersion = inspection.Migrations.Count;
                }
                restorable = inspection.IsDatabase && inspection.IntegrityOk && inspection.HasOwner && inspection.Migrations.Count > 0
                    && inspection.Migrations.All(migration => backup.KnownMigrations.Contains(migration));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Unreadable (locked, no permission): listed as not restorable.
            }
            result.Add(new BackupFileDto(file.Name, file.FullName, file.Length, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero), KindOf(file.Name), source, restorable,
                integrity, appVersion, schemaVersion));
        }
        return result;
    }

    private static string KindOf(string name) =>
        name.StartsWith("timetable-backup-", StringComparison.OrdinalIgnoreCase) ? "manual"
        : name.StartsWith(AutoPrefix, StringComparison.OrdinalIgnoreCase) ? "auto"
        : name.StartsWith("pre-upgrade-", StringComparison.OrdinalIgnoreCase) ? "preUpgrade"
        : name.StartsWith("pre-restore-", StringComparison.OrdinalIgnoreCase) ? "preRestore"
        : name.StartsWith("pre-conversion-", StringComparison.OrdinalIgnoreCase) ? "preConversion"
        : "other";

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
