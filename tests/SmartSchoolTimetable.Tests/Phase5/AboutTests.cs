using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartSchoolTimetable.Api;
using SmartSchoolTimetable.Api.Endpoints;
using SmartSchoolTimetable.Application;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>«حول البرنامج» (M2): the facts shown, the guarded open-data-folder action, and a support log that never carries a user name or path.</summary>
public sealed class AboutTests
{
    private sealed class RecordingOpener : IFolderOpener
    {
        public List<string> Opened { get; } = [];

        public bool Open(string folder)
        {
            Opened.Add(folder);
            return true;
        }
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task AboutShowsTheVersionBuildDateSchemaLicenceDataFolderAndThirdPartyLicences()
    {
        await using var host = new TestHost();
        await SetupOwnerAsync(host);
        var about = await JsonAsync(await host.Client.GetAsync("/api/v1/about/"));

        Assert.Equal(AppInfo.Version, about.GetProperty("version").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", about.GetProperty("buildDate").GetString());
        Assert.True(about.GetProperty("schemaVersion").GetInt32() > 0);
        Assert.Contains("جميع الحقوق محفوظة", about.GetProperty("license").GetString(), StringComparison.Ordinal);
        Assert.Equal(Path.GetDirectoryName(host.DatabasePath), about.GetProperty("dataFolder").GetString());
        Assert.Contains(about.GetProperty("thirdParty").EnumerateArray(), item => item.GetProperty("name").GetString() == "Google OR-Tools");
    }

    [Fact]
    public async Task AboutNeedsASignedInOwner()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/about/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/about/support-log")).StatusCode);
    }

    [Fact]
    public async Task OpeningTheDataFolderOnlyEverOpensTheApplicationsOwnFolderAndNeedsTheLaunchToken()
    {
        var opener = new RecordingOpener();
        await using var host = new TestHost(configureServices: services =>
        {
            services.RemoveAll<IFolderOpener>();
            services.AddSingleton<IFolderOpener>(opener);
        });
        var (token, _) = await SetupOwnerAsync(host);

        // No path can be supplied: a body with a path is ignored, the folder is always the one the database lives in.
        var response = await host.PostAsync("/api/v1/about/open-data-folder", new { path = @"C:\Windows" }, token);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([Path.GetDirectoryName(host.DatabasePath)!], opener.Opened);

        var noToken = await host.PostAsync("/api/v1/about/open-data-folder", new { }, "not-the-launch-token");
        Assert.NotEqual(HttpStatusCode.NoContent, noToken.StatusCode);
        Assert.Single(opener.Opened);
    }

    [Fact]
    public async Task TheSupportLogHasTheFactsAndNoUserNamesOrPaths()
    {
        await using var host = new TestHost();
        await SetupOwnerAsync(host);
        var response = await host.Client.GetAsync("/api/v1/about/support-log");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("support-log-", response.Content.Headers.ContentDisposition?.FileName, StringComparison.Ordinal);
        var text = await response.Content.ReadAsStringAsync();

        Assert.Contains($"Application version: {AppInfo.Version}", text, StringComparison.Ordinal);
        Assert.Contains("Schema version:", text, StringComparison.Ordinal);
        Assert.Contains("Row counts per table", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Environment.UserName, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Users\" + Environment.UserName, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("A-Strong-Passphrase", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactionRemovesTheProfilePathTheUserNameAndTheMachineName()
    {
        var redacted = PathRedactor.Redact(@"Failed at C:\Users\Jaafar Wafqan\AppData\Local\X\a.db on HOME-PC by jaafar2", "jaafar2", "HOME-PC");
        Assert.DoesNotContain("Wafqan", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("HOME-PC", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("jaafar2", redacted, StringComparison.Ordinal);
        Assert.Contains(@"C:\Users\<user>", redacted, StringComparison.Ordinal);
        Assert.Equal(@"C:\Users\<user>/x", PathRedactor.Redact("c:/users/bob/x", "nobody-here", "nobody-pc"));
    }
}
