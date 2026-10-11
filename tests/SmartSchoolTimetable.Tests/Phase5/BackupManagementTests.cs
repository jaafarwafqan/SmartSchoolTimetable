using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Backup;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>
/// M2 backups on temporary databases only: consistent copies with an integrity check and version metadata, damaged and truncated files that are
/// listed but refused, a restore that fails part-way and is rolled back, the automatic backup with keep-last-N (only its own files are pruned).
/// </summary>
public sealed class BackupManagementTests
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task CreateSubjectAsync(TestHost host, string token, string name)
    {
        var response = await host.PostAsync("/api/v1/subjects/", new
        {
            name, colorIndex = 1, priority = 0, distributionEnabled = true, spreadAcrossDays = false, heavy = false,
            requiresDoublePeriod = false, blockedPeriods = Array.Empty<object>(), notes = (string?)null, version = 0,
        }, token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<string[]> SubjectNamesAsync(TestHost host) =>
        (await JsonAsync(await host.Client.GetAsync("/api/v1/subjects/"))).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()!).ToArray();

    private static async Task<JsonElement[]> FilesAsync(TestHost host, string folder) =>
        (await JsonAsync(await host.Client.GetAsync($"/api/v1/backup/files?folder={Uri.EscapeDataString(folder)}"))).GetProperty("files").EnumerateArray().ToArray();

    private static string FolderOf(TestHost host, string name) => Path.Combine(Path.GetDirectoryName(host.DatabasePath)!, name);

    [Fact]
    public async Task ABackupIsAConsistentCopyWithAnIntegrityCheckAndTheVersionsThatMadeIt()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var folder = FolderOf(host, "m2-backups");
        await CreateSubjectAsync(host, token, "الرياضيات");
        var created = await JsonAsync(await host.PostAsync("/api/v1/backup/", new { folder }, token));
        Assert.True(File.Exists(created.GetProperty("filePath").GetString()));

        var file = Assert.Single(await FilesAsync(host, folder));
        Assert.Equal(("ok", true, "manual"), (file.GetProperty("integrity").GetString(), file.GetProperty("restorable").GetBoolean(), file.GetProperty("kind").GetString()));
        Assert.Equal(AppInfo.Version, file.GetProperty("appVersion").GetString());
        var listing = await JsonAsync(await host.Client.GetAsync($"/api/v1/backup/files?folder={Uri.EscapeDataString(folder)}"));
        Assert.True(file.GetProperty("schemaVersion").GetInt32() > 0);
        Assert.Equal(listing.GetProperty("currentSchemaVersion").GetInt32(), file.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task DamagedAndTruncatedFilesAreListedAsNotRestorableAndRefusedWithoutTouchingTheData()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var folder = FolderOf(host, "m2-corrupt");
        await CreateSubjectAsync(host, token, "الرياضيات");
        var good = (await JsonAsync(await host.PostAsync("/api/v1/backup/", new { folder }, token))).GetProperty("filePath").GetString()!;

        // Truncated: the first third of a real backup. Damaged: bytes in the middle of the file overwritten.
        var bytes = await File.ReadAllBytesAsync(good);
        var truncated = Path.Combine(folder, "truncated.db");
        await File.WriteAllBytesAsync(truncated, bytes[..(bytes.Length / 3)]);
        var damaged = Path.Combine(folder, "damaged.db");
        var broken = (byte[])bytes.Clone();
        for (var offset = bytes.Length / 2; offset < bytes.Length / 2 + 4096 && offset < broken.Length; offset++)
            broken[offset] = 0xFF;
        await File.WriteAllBytesAsync(damaged, broken);

        var files = (await FilesAsync(host, folder)).ToDictionary(item => item.GetProperty("name").GetString()!);
        Assert.True(files[Path.GetFileName(good)].GetProperty("restorable").GetBoolean());
        Assert.False(files["truncated.db"].GetProperty("restorable").GetBoolean());
        Assert.False(files["damaged.db"].GetProperty("restorable").GetBoolean());
        Assert.NotEqual("ok", files["damaged.db"].GetProperty("integrity").GetString());

        await CreateSubjectAsync(host, token, "الفيزياء");
        foreach (var path in new[] { truncated, damaged })
        {
            await AssertApiErrorAsync(await host.PostAsync("/api/v1/backup/restore", new { filePath = path, confirm = true, confirmReplace = true }, token), ErrorCodes.RestoreFileInvalid);
        }
        Assert.Contains("الفيزياء", await SubjectNamesAsync(host)); // nothing was replaced, and the files are still there
        Assert.True(File.Exists(truncated) && File.Exists(damaged));
    }

    [Fact]
    public async Task ARestoreThatFailsPartWayIsRolledBackFromTheAutomaticBackup()
    {
        var restores = new List<string>();
        await using var host = new TestHost(configureServices: services =>
        {
            services.RemoveAll<IDatabaseBackup>();
            services.AddScoped<IDatabaseBackup>(provider =>
                new FailingFirstRestore(ActivatorUtilities.CreateInstance<SmartSchoolTimetable.Infrastructure.Persistence.SqliteDatabaseBackup>(provider), restores));
        });
        var (token, _) = await SetupOwnerAsync(host);
        var folder = FolderOf(host, "m2-rollback");
        await CreateSubjectAsync(host, token, "الرياضيات");
        var backupFile = (await JsonAsync(await host.PostAsync("/api/v1/backup/", new { folder }, token))).GetProperty("filePath").GetString()!;
        await CreateSubjectAsync(host, token, "الفيزياء");

        await AssertApiErrorAsync(await host.PostAsync("/api/v1/backup/restore", new { filePath = backupFile, confirm = true, confirmReplace = true }, token), ErrorCodes.RestoreFailed);
        // The chosen file was tried first, then the automatic backup of the current data put everything back; the owner is still signed in.
        Assert.Equal(2, restores.Count);
        Assert.Equal(backupFile, restores[0]);
        Assert.StartsWith("pre-restore-", Path.GetFileName(restores[1]), StringComparison.Ordinal);
        Assert.Contains("الفيزياء", await SubjectNamesAsync(host));
    }

    [Fact]
    public async Task TheSettingsAreVersionedAndValidated()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var settings = await JsonAsync(await host.Client.GetAsync("/api/v1/backup/settings"));
        Assert.Equal((true, 14), (settings.GetProperty("autoBackupEnabled").GetBoolean(), settings.GetProperty("keepLast").GetInt32()));
        var v = settings.GetProperty("version").GetInt32();
        Assert.Equal(JsonValueKind.Null, settings.GetProperty("folder").ValueKind);
        Assert.Equal(settings.GetProperty("suggestedFolder").GetString(), settings.GetProperty("effectiveFolder").GetString());

        var folder = FolderOf(host, "chosen");
        var saved = await JsonAsync(await host.PutAsync("/api/v1/backup/settings", new { folder, autoBackupEnabled = false, keepLast = 3, version = v }, token));
        Assert.Equal((folder, false, 3, v + 1), (saved.GetProperty("folder").GetString(), saved.GetProperty("autoBackupEnabled").GetBoolean(), saved.GetProperty("keepLast").GetInt32(), saved.GetProperty("version").GetInt32()));
        Assert.Equal(folder, saved.GetProperty("effectiveFolder").GetString());

        await AssertApiErrorAsync(await host.PutAsync("/api/v1/backup/settings", new { folder, autoBackupEnabled = true, keepLast = 3, version = v }, token), ErrorCodes.Conflict);
        var invalid = await AssertApiErrorAsync(await host.PutAsync("/api/v1/backup/settings", new { folder = "relative\\path", autoBackupEnabled = true, keepLast = 5, version = v + 1 }, token), ErrorCodes.ValidationFailed);
        Assert.Equal(["Folder", "KeepLast"], invalid.Errors.Select(issue => issue.Field).Order());
    }

    [Fact]
    public async Task TheAutomaticBackupRunsWhenDueKeepsTheLastNAndPrunesOnlyItsOwnFiles()
    {
        await using var host = new TestHost();
        // Before the owner account exists there is nothing to back up.
        Assert.False((await RunAutoAsync(host)).Created);
        var (token, _) = await SetupOwnerAsync(host);
        var folder = FolderOf(host, "m2-auto");
        var v = (await JsonAsync(await host.Client.GetAsync("/api/v1/backup/settings"))).GetProperty("version").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await host.PutAsync("/api/v1/backup/settings", new { folder, autoBackupEnabled = true, keepLast = 3, version = v }, token)).StatusCode);

        // A manual backup and a look-alike file in the same folder: never pruned.
        var manual = (await JsonAsync(await host.PostAsync("/api/v1/backup/", new { folder }, token))).GetProperty("filePath").GetString()!;
        var notes = Path.Combine(folder, "auto-notes.txt");
        await File.WriteAllTextAsync(notes, "ملاحظات");

        var first = await RunAutoAsync(host);
        Assert.True(first.Created);
        Assert.StartsWith("auto-backup-", Path.GetFileName(first.File!), StringComparison.Ordinal);
        Assert.False((await RunAutoAsync(host)).Created); // made just now: not due

        for (var run = 0; run < 5; run++)
        {
            host.Clock.Advance(TimeSpan.FromHours(21));
            Assert.True((await RunAutoAsync(host)).Created);
        }
        // The clock moved days ahead: the session timed out, so sign in again for the reads and writes below.
        var login = await host.PostAsync("/api/v1/auth/login", new { username = "owner", password = "A-Strong-Passphrase-401" }, (await host.GetBootstrapAsync()).LaunchToken);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var autos = Directory.GetFiles(folder, "auto-backup-*.db");
        Assert.Equal(3, autos.Length); // 6 made, the 3 oldest removed
        Assert.True(File.Exists(manual) && File.Exists(notes));
        var raw = await host.Client.GetAsync($"/api/v1/backup/files?folder={Uri.EscapeDataString(folder)}"); Assert.True(raw.IsSuccessStatusCode, await raw.Content.ReadAsStringAsync());
        Assert.All(await FilesAsync(host, folder), file => Assert.True(file.GetProperty("restorable").GetBoolean() || file.GetProperty("name").GetString() == "auto-notes.txt"));

        // Keep-all (0) removes nothing; switched off does nothing.
        Assert.Equal(HttpStatusCode.OK, (await host.PutAsync("/api/v1/backup/settings", new { folder, autoBackupEnabled = true, keepLast = 0, version = await VersionAsync(host) }, token)).StatusCode);
        host.Clock.Advance(TimeSpan.FromHours(21));
        Assert.Equal(0, (await RunAutoAsync(host)).Pruned);
        Assert.Equal(4, Directory.GetFiles(folder, "auto-backup-*.db").Length);
        Assert.Equal(HttpStatusCode.OK, (await host.PutAsync("/api/v1/backup/settings", new { folder, autoBackupEnabled = false, keepLast = 0, version = await VersionAsync(host) }, token)).StatusCode);
        host.Clock.Advance(TimeSpan.FromHours(21));
        Assert.False((await RunAutoAsync(host)).Created);

        _ = await VersionAsync(host); // signs in again
        var history = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=backup"));
        var events = history.GetProperty("items").EnumerateArray().Select(entry => entry.GetProperty("eventType").GetString()).ToArray();
        Assert.Contains("AutoBackupCreated", events);
        Assert.Contains("AutoBackupPruned", events);
    }

    private static async Task<int> VersionAsync(TestHost host)
    {
        // The test clock jumps hours ahead, which times the session out: sign in again first.
        await host.PostAsync("/api/v1/auth/login", new { username = "owner", password = "A-Strong-Passphrase-401" }, (await host.GetBootstrapAsync()).LaunchToken);
        return (await JsonAsync(await host.Client.GetAsync("/api/v1/backup/settings"))).GetProperty("version").GetInt32();
    }

    private static async Task<AutoBackupOutcome> RunAutoAsync(TestHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BackupService>().RunAutoBackupAsync(CancellationToken.None);
    }

    /// <summary>Fails the first restore (the chosen file, part-way), then works: the second call is the rollback from the automatic backup.</summary>
    private sealed class FailingFirstRestore(IDatabaseBackup inner, List<string> restores) : IDatabaseBackup
    {
        public string AutomaticBackupFolder => inner.AutomaticBackupFolder;
        public IReadOnlyList<string> KnownMigrations => inner.KnownMigrations;
        public Task CreateAsync(string targetFile, CancellationToken token) => inner.CreateAsync(targetFile, token);
        public Task<BackupInspection> InspectAsync(string file, CancellationToken token) => inner.InspectAsync(file, token);

        public async Task RestoreAsync(string file, CancellationToken token)
        {
            restores.Add(file);
            if (restores.Count == 1)
                throw new IOException("The copy stopped part-way (simulated).");
            await inner.RestoreAsync(file, token);
        }
    }
}
