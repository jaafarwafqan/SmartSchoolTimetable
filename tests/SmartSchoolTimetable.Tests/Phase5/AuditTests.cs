using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain;
using SmartSchoolTimetable.Infrastructure;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>The audit event codes are a contract like the error codes: registered once, categorised, and written in Arabic by the frontend.</summary>
public sealed partial class AuditEventContractTests
{
    private static string[] Constants() => typeof(AuditEvents).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    private static string Dictionary() => File.ReadAllText(TestPaths.FrontendFile("src", "i18n", "ar", "audit.ts"));

    private static string Block(string dictionary, string anchor)
    {
        var block = dictionary[dictionary.IndexOf(anchor, StringComparison.Ordinal)..];
        return block[..block.IndexOf("\n  },", StringComparison.Ordinal)];
    }

    [Fact]
    public void EveryEventIsRegisteredInACategory()
    {
        var constants = Constants();
        Assert.Equal(constants.Order(StringComparer.Ordinal), AuditEvents.All.Order(StringComparer.Ordinal));
        Assert.Equal(constants.Length, constants.Distinct(StringComparer.Ordinal).Count());
        Assert.All(AuditEvents.Categories.Values, category => Assert.Contains(category, AuditCategories.All));
        Assert.All(AuditCategories.All, category => Assert.Contains(category, AuditEvents.Categories.Values.Append(AuditCategories.Import)));
    }

    [Fact]
    public void EveryEventAndCategoryHasAnArabicSentenceInTheFrontendDictionary()
    {
        var dictionary = Dictionary();
        var events = Block(dictionary, "  events: {");
        foreach (var code in Constants())
        {
            var entry = Regex.Match(events, $@"^\s{{4}}{code}:\s*(.+)$", RegexOptions.Multiline);
            Assert.True(entry.Success, $"{code} has no Arabic sentence.");
            var from = entry.Index;
            Assert.Matches(@"[؀-ۿ]", events[from..Math.Min(events.Length, from + 400)]);
        }
        Assert.Equal(Constants().Order(StringComparer.Ordinal), EventKey().Matches(events).Select(match => match.Groups[1].Value).Order(StringComparer.Ordinal));
        var categories = Block(dictionary, "  categories: {");
        Assert.Equal(AuditCategories.All.Order(StringComparer.Ordinal), CategoryKey().Matches(categories).Select(match => match.Groups[1].Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NoEventNameIsWrittenAsALiteralWhereAnEntryIsRecorded()
    {
        var offenders = TestPaths.BackendSourceFiles()
            .Where(file => !file.EndsWith("AuditEvents.cs", StringComparison.Ordinal))
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, index)))
            .Where(item => AuditLiteral().IsMatch(item.line))
            .Select(item => $"{Path.GetFileName(item.file)}:{item.index + 1}")
            .ToArray();
        Assert.Empty(offenders);
        // Entries are created in one place only.
        var creators = TestPaths.BackendSourceFiles()
            .Where(file => File.ReadAllText(file).Contains("LocalAuditEntry.Create(", StringComparison.Ordinal))
            .Select(file => Path.GetFileName(file))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["AuditTrail.cs"], creators);
    }

    [GeneratedRegex(@"^\s{4}([A-Z][A-Za-z]+):", RegexOptions.Multiline)]
    private static partial Regex EventKey();

    [GeneratedRegex(@"^\s{4}([a-z]+):", RegexOptions.Multiline)]
    private static partial Regex CategoryKey();

    [GeneratedRegex(@"AuditTrail\.(Record\(store, clock|Entry\(now), ""|eventType: ""[A-Z]")]
    private static partial Regex AuditLiteral();
}

/// <summary>The history endpoint: owner only, filtered, paged, newest first; the entries carry codes and parameters, not prose.</summary>
public sealed class AuditApiTests
{
    /// <summary>Reads a successful JSON response; a failure shows the status and body instead of a missing property.</summary>
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task CreateSubjectAsync(TestHost host, string token, string name) =>
        Assert.Equal(HttpStatusCode.Created, (await host.PostAsync("/api/v1/subjects/", new
        {
            name, colorIndex = 1, priority = 0, distributionEnabled = true, spreadAcrossDays = false, heavy = false,
            requiresDoublePeriod = false, blockedPeriods = Array.Empty<object>(), notes = (string?)null, version = 0,
        }, token)).StatusCode);

    [Fact]
    public async Task TheHistoryIsOwnerOnlyAndEveryAccountAndSettingsChangeIsInIt()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/audit/")).StatusCode);
        var (token, _) = await SetupOwnerAsync(host);
        Assert.Equal(HttpStatusCode.OK, (await host.PutAsync("/api/v1/settings/inactivity-timeout", new { inactivityTimeout = "15" }, token)).StatusCode);
        var change = await host.PostAsync("/api/v1/auth/change-password",
            new { currentPassword = "A-Strong-Passphrase-401", newPassword = "Another-Strong-Passphrase-402", confirmPassword = "Another-Strong-Passphrase-402" }, token);
        Assert.True(change.IsSuccessStatusCode, change.StatusCode.ToString());
        await host.PostAsync("/api/v1/auth/login", new { username = "owner", password = "Another-Strong-Passphrase-402" }, token);

        var account = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=account"));
        Assert.Equal(["PasswordChanged", "OwnerAccountCreated"], account.GetProperty("items").EnumerateArray().Select(entry => entry.GetProperty("eventType").GetString()));
        var settings = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=settings"));
        Assert.Equal(["InactivityTimeoutChanged"], settings.GetProperty("items").EnumerateArray().Select(entry => entry.GetProperty("eventType").GetString()));
        Assert.False(settings.GetProperty("items")[0].TryGetProperty("summary", out _));
    }

    [Fact]
    public async Task EntriesAreFilteredByCategoryAndEventTypePagedAndNewestFirst()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        foreach (var name in new[] { "الرياضيات", "الفيزياء", "الكيمياء", "الأحياء" })
        {
            await CreateSubjectAsync(host, token, name);
            host.Clock.Advance(TimeSpan.FromMinutes(1));
        }
        var folder = Path.Combine(Path.GetDirectoryName(host.DatabasePath)!, "owner-backups");
        Assert.Equal(HttpStatusCode.Created, (await host.PostAsync("/api/v1/backup/", new { folder }, token)).StatusCode);

        var school = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=school&pageSize=3"));
        Assert.Equal((4, 1, 3, 3), (school.GetProperty("total").GetInt32(), school.GetProperty("page").GetInt32(), school.GetProperty("pageSize").GetInt32(), school.GetProperty("items").GetArrayLength()));
        var second = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=school&pageSize=3&page=2"));
        Assert.Equal(1, second.GetProperty("items").GetArrayLength());
        var times = school.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray()).Select(entry => entry.GetProperty("occurredAt").GetDateTimeOffset()).ToArray();
        Assert.Equal(times.OrderByDescending(time => time), times);

        var backups = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?eventType=BackupCreated"));
        Assert.Equal(1, backups.GetProperty("total").GetInt32());
        Assert.Equal("backup", backups.GetProperty("items")[0].GetProperty("category").GetString());
        var everything = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?pageSize=100"));
        Assert.Equal(1 + 4 + 1, everything.GetProperty("total").GetInt32());
        Assert.Equal(100, (await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?pageSize=500"))).GetProperty("pageSize").GetInt32());

        await AssertApiErrorAsync(await host.Client.GetAsync("/api/v1/audit/?category=nonsense"), ErrorCodes.ValidationFailed);
        await AssertApiErrorAsync(await host.Client.GetAsync("/api/v1/audit/?eventType=NotAnEvent"), ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task EntriesFromOlderBuildsWithoutParametersOrWithAnUnknownEventStillAppearUnderSchool()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        await CreateSubjectAsync(host, token, "التاريخ");
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
            db.AuditEntries.Add(LocalAuditEntry.Create(DateTimeOffset.UtcNow, "SomethingFromAFutureBuild", "x", "Legacy text."));
            await db.SaveChangesAsync();
        }
        var school = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=school"));
        var legacy = school.GetProperty("items").EnumerateArray().Single(entry => entry.GetProperty("eventType").GetString() == "SomethingFromAFutureBuild");
        Assert.Equal(JsonValueKind.Null, legacy.GetProperty("params").ValueKind);
        Assert.Equal("school", legacy.GetProperty("category").GetString());
        Assert.False(legacy.TryGetProperty("summary", out _));
        Assert.DoesNotContain("Legacy text", school.GetRawText(), StringComparison.Ordinal);
    }
}
