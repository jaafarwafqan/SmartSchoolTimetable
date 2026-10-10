using System.Net;
using System.Text.Json;
using SmartSchoolTimetable.Application;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>MF10: the server-side folder picker (subfolders only) and the list of backups to restore from, on a temporary database only.</summary>
public sealed class BackupBrowseTests
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task TheFolderPickerListsSubfoldersOnlyAndTheStartingPlaces()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/backup/folders")).StatusCode);
        await SetupOwnerAsync(host);
        var root = Path.Combine(Path.GetDirectoryName(host.DatabasePath)!, "picker");
        Directory.CreateDirectory(Path.Combine(root, "Zeta"));
        Directory.CreateDirectory(Path.Combine(root, "alpha"));
        await File.WriteAllTextAsync(Path.Combine(root, "notes.txt"), "ملف لا يظهر");

        var start = await JsonAsync(await host.Client.GetAsync("/api/v1/backup/folders"));
        Assert.Equal(JsonValueKind.Null, start.GetProperty("path").ValueKind);
        Assert.Empty(start.GetProperty("folders").EnumerateArray());
        Assert.Contains(start.GetProperty("places").EnumerateArray(), place => place.GetProperty("kind").GetString() == "drive");

        var listing = await JsonAsync(await host.Client.GetAsync($"/api/v1/backup/folders?path={Uri.EscapeDataString(root)}"));
        Assert.Equal(["alpha", "Zeta"], listing.GetProperty("folders").EnumerateArray().Select(item => item.GetProperty("name").GetString()!).ToArray());
        Assert.Equal(Path.GetDirectoryName(root), listing.GetProperty("parent").GetString());
        Assert.Empty(listing.GetProperty("places").EnumerateArray());

        await AssertApiErrorAsync(await host.Client.GetAsync("/api/v1/backup/folders?path=relative"), ErrorCodes.ValidationFailed);
        await AssertApiErrorAsync(await host.Client.GetAsync($"/api/v1/backup/folders?path={Uri.EscapeDataString(Path.Combine(root, "missing"))}"), ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task TheBackupListShowsRestorableAndUnusableDatabaseFilesNewestFirst()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/backup/files")).StatusCode);
        var (token, _) = await SetupOwnerAsync(host);
        var folder = Path.Combine(Path.GetDirectoryName(host.DatabasePath)!, "list-backups");

        var empty = await JsonAsync(await host.Client.GetAsync($"/api/v1/backup/files?folder={Uri.EscapeDataString(folder)}"));
        Assert.Empty(empty.GetProperty("files").EnumerateArray()); // a folder that does not exist yet is simply empty

        var created = await JsonAsync(await host.PostAsync("/api/v1/backup/", new { folder }, token));
        var backupFile = created.GetProperty("filePath").GetString()!;
        await File.WriteAllTextAsync(Path.Combine(folder, "broken.db"), "ليست قاعدة بيانات");
        await File.WriteAllTextAsync(Path.Combine(folder, "notes.txt"), "لا يظهر");
        File.SetLastWriteTimeUtc(Path.Combine(folder, "broken.db"), DateTime.UtcNow.AddMinutes(-30));

        var listing = await JsonAsync(await host.Client.GetAsync($"/api/v1/backup/files?folder={Uri.EscapeDataString(folder)}"));
        var files = listing.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(2, files.Length);
        Assert.Equal(backupFile, files[0].GetProperty("path").GetString());
        Assert.Equal(("manual", "folder", true), (files[0].GetProperty("kind").GetString(), files[0].GetProperty("source").GetString(), files[0].GetProperty("restorable").GetBoolean()));
        Assert.Equal(("broken.db", "other", false), (files[1].GetProperty("name").GetString(), files[1].GetProperty("kind").GetString(), files[1].GetProperty("restorable").GetBoolean()));
        Assert.True(files[0].GetProperty("sizeBytes").GetInt64() > 0);

        await AssertApiErrorAsync(await host.Client.GetAsync("/api/v1/backup/files?folder=relative"), ErrorCodes.ValidationFailed);
    }
}
