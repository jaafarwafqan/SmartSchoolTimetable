using System.Net;
using System.Text.Json;
using SmartSchoolTimetable.Application;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// Backup and restore (Phase 4 M6) on a temporary database only: a backup is a new consistent file; a restore needs
/// both confirmations and a valid, compatible file, takes an automatic backup first, brings the old data back and
/// signs the owner out. No file is deleted or overwritten.
/// </summary>
public sealed class BackupApiTests
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
        (await JsonAsync(await host.Client.GetAsync("/api/v1/subjects/"))).GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()!).ToArray();

    [Fact]
    public async Task ABackupCanBeRestoredOnlyWithBothConfirmationsAndAValidFile()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var folder = Path.Combine(Path.GetDirectoryName(host.DatabasePath)!, "owner-backups");
        await CreateSubjectAsync(host, token, "الرياضيات");

        await AssertApiErrorAsync(await host.PostAsync("/api/v1/backup/", new { folder = "relative\\path" }, token), ErrorCodes.ValidationFailed);
        var created = await host.PostAsync("/api/v1/backup/", new { folder }, token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var backupFile = (await JsonAsync(created)).GetProperty("filePath").GetString()!;
        Assert.True(File.Exists(backupFile));
        Assert.StartsWith(folder, backupFile, StringComparison.OrdinalIgnoreCase);

        await CreateSubjectAsync(host, token, "الفيزياء");
        Assert.Contains("الفيزياء", await SubjectNamesAsync(host));

        await AssertApiErrorAsync(await host.PostAsync("/api/v1/backup/restore", new { filePath = backupFile, confirm = true, confirmReplace = false }, token),
            ErrorCodes.RestoreConfirmationRequired);
        var notADatabase = Path.Combine(folder, "notes.txt");
        await File.WriteAllTextAsync(notADatabase, "ليست قاعدة بيانات");
        await AssertApiErrorAsync(await host.PostAsync("/api/v1/backup/restore", new { filePath = notADatabase, confirm = true, confirmReplace = true }, token),
            ErrorCodes.RestoreFileInvalid);
        Assert.Contains("الفيزياء", await SubjectNamesAsync(host));

        var restored = await host.PostAsync("/api/v1/backup/restore", new { filePath = backupFile, confirm = true, confirmReplace = true }, token);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        var automatic = (await JsonAsync(restored)).GetProperty("automaticBackupPath").GetString()!;
        Assert.True(File.Exists(automatic));
        Assert.True(File.Exists(backupFile));

        // Signed out after the data was replaced; signing in again shows the data of the backup.
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/subjects/")).StatusCode);
        var login = await host.PostAsync("/api/v1/auth/login", new { username = "owner", password = "A-Strong-Passphrase-401" }, token);
        Assert.True(login.IsSuccessStatusCode, login.StatusCode.ToString());
        var names = await SubjectNamesAsync(host);
        Assert.Contains("الرياضيات", names);
        Assert.DoesNotContain("الفيزياء", names);
    }
}
