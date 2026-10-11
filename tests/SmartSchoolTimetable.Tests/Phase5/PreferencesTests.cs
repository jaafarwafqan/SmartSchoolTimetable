using System.Net;
using System.Text.Json;
using SmartSchoolTimetable.Application;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>M2: the owner's stored preferences (theme, default semester, default print options): defaults, persistence, versioning, validation.</summary>
public sealed class PreferencesTests
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static object Body(int version, string theme = "system", int? semester = null, string sectionPaper = "a4", bool fit = true) => new
    {
        theme, defaultSemester = semester, section = new { paper = sectionPaper, orientation = "landscape" }, teacher = new { paper = "a4", orientation = "portrait" },
        school = new { paper = "a3", orientation = "landscape" }, printFit = fit, version,
    };

    [Fact]
    public async Task AFreshDatabaseHasTheOwnersDefaultsAndRequiresTheOwner()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/preferences/")).StatusCode);
        await SetupOwnerAsync(host);
        var preferences = await JsonAsync(await host.Client.GetAsync("/api/v1/preferences/"));
        Assert.Equal("system", preferences.GetProperty("theme").GetString());
        Assert.Equal(JsonValueKind.Null, preferences.GetProperty("defaultSemester").ValueKind);
        Assert.Equal(("a4", "landscape"), (preferences.GetProperty("section").GetProperty("paper").GetString(), preferences.GetProperty("section").GetProperty("orientation").GetString()));
        Assert.Equal(("a4", "portrait"), (preferences.GetProperty("teacher").GetProperty("paper").GetString(), preferences.GetProperty("teacher").GetProperty("orientation").GetString()));
        Assert.Equal(("a3", "landscape"), (preferences.GetProperty("school").GetProperty("paper").GetString(), preferences.GetProperty("school").GetProperty("orientation").GetString()));
        Assert.True(preferences.GetProperty("printFit").GetBoolean());
        Assert.Equal(1, preferences.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task ChangesPersistWithANewVersionAndAnUnchangedSaveDoesNotBumpIt()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var saved = await JsonAsync(await host.PutAsync("/api/v1/preferences/", Body(1, "dark", 2, "a3", false), token));
        Assert.Equal(("dark", 2, "a3", false, 2),
            (saved.GetProperty("theme").GetString(), saved.GetProperty("defaultSemester").GetInt32(), saved.GetProperty("section").GetProperty("paper").GetString(),
                saved.GetProperty("printFit").GetBoolean(), saved.GetProperty("version").GetInt32()));
        var reread = await JsonAsync(await host.Client.GetAsync("/api/v1/preferences/"));
        Assert.Equal("dark", reread.GetProperty("theme").GetString());

        // The same values again: still version 2 and no second history entry.
        var again = await JsonAsync(await host.PutAsync("/api/v1/preferences/", Body(2, "dark", 2, "a3", false), token));
        Assert.Equal(2, again.GetProperty("version").GetInt32());
        var history = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=settings"));
        Assert.Single(history.GetProperty("items").EnumerateArray(), entry => entry.GetProperty("eventType").GetString() == "PreferencesUpdated");
    }

    [Fact]
    public async Task StaleVersionsAndInvalidValuesAreRejected()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        Assert.Equal(HttpStatusCode.OK, (await host.PutAsync("/api/v1/preferences/", Body(1, "light"), token)).StatusCode);
        await AssertApiErrorAsync(await host.PutAsync("/api/v1/preferences/", Body(1, "dark"), token), ErrorCodes.Conflict);

        var invalid = await host.PutAsync("/api/v1/preferences/", new
        {
            theme = "sepia", defaultSemester = 3, section = new { paper = "a5", orientation = "landscape" }, teacher = new { paper = "a4", orientation = "sideways" },
            school = new { paper = "a3", orientation = "landscape" }, printFit = true, version = 2,
        }, token);
        var fields = (await AssertApiErrorAsync(invalid, ErrorCodes.ValidationFailed)).Errors.Select(issue => issue.Field).Order().ToArray();
        Assert.Equal(["DefaultSemester", "SectionPaper", "TeacherOrientation", "Theme"], fields);
        Assert.Equal("light", (await JsonAsync(await host.Client.GetAsync("/api/v1/preferences/"))).GetProperty("theme").GetString()); // nothing was half-saved
    }
}
