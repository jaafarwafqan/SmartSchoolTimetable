using System.Net;
using Microsoft.EntityFrameworkCore;
using SmartSchoolTimetable.Api;
using SmartSchoolTimetable.Domain;
using Microsoft.Extensions.DependencyInjection;
using SmartSchoolTimetable.Infrastructure;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests;

public sealed class InactivityTimeoutSettingTests
{
    private const string SettingPath = "/api/v1/settings/inactivity-timeout";

    [Fact]
    public async Task OwnerChoicePersistsAndAppliesAtRuntimeWithoutRestart()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);

        // The test host configures a 5-minute default.
        var initial = await host.GetBootstrapAsync();
        Assert.Equal(5, initial.InactivityTimeoutMinutes);
        Assert.Equal(OwnerAccount.InactivityTimeoutChoicesMinutes, initial.InactivityTimeoutChoices);

        var saved = await host.PutAsync(SettingPath, new { inactivityTimeout = "15" }, token);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(15, (await ReadAsync<InactivityTimeoutResponse>(saved)).InactivityTimeoutMinutes);
        Assert.Contains(saved.Headers.GetValues("Set-Cookie"), cookie =>
            cookie.Contains("max-age=900", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(15, (await host.GetBootstrapAsync()).InactivityTimeoutMinutes);

        // 6 minutes would have expired the 5-minute default; the new 15-minute value applies immediately.
        host.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/v1/private/status")).StatusCode);
        host.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/private/status")).StatusCode);

        var login = await host.PostAsync(
            "/api/v1/auth/login",
            new { username = "owner", password = "A-Strong-Passphrase-401" },
            token);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var never = await host.PutAsync(SettingPath, new { inactivityTimeout = "never" }, token);
        Assert.Equal(HttpStatusCode.OK, never.StatusCode);
        Assert.Null((await ReadAsync<InactivityTimeoutResponse>(never)).InactivityTimeoutMinutes);
        host.Clock.Advance(TimeSpan.FromDays(365));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/v1/private/status")).StatusCode);
        Assert.Null((await host.GetBootstrapAsync()).InactivityTimeoutMinutes);

        var dbOptions = new DbContextOptionsBuilder<LocalDbContext>()
            .UseSqlite($"Data Source={host.DatabasePath};Pooling=False")
            .Options;
        await using var db = new LocalDbContext(dbOptions);
        var owner = await db.Owners.SingleAsync();
        Assert.True(owner.HasCustomInactivityTimeout);
        Assert.Null(owner.InactivityTimeoutMinutes);
        Assert.Contains(await db.AuditEntries.Select(entry => entry.EventType).ToListAsync(), type =>
            type == "InactivityTimeoutChanged");
        Assert.Contains(
            "20261003175642_AddOwnerInactivityTimeoutPreference",
            await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task InvalidMissingOrUnauthenticatedChangesAreRejected()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);

        var invalid = await host.PutAsync(SettingPath, new { inactivityTimeout = "7" }, token);
        Assert.Equal((HttpStatusCode)422, invalid.StatusCode);
        var invalidError = await AssertApiErrorAsync(invalid, "VALIDATION_FAILED");
        Assert.Contains(invalidError.Errors, issue =>
            issue.Field == "InactivityTimeout" && issue.Code == "INVALID_INACTIVITY_TIMEOUT");

        var missing = await host.PutAsync(SettingPath, new { }, token);
        var missingError = await AssertApiErrorAsync(missing, "VALIDATION_FAILED");
        Assert.Contains(missingError.Errors, issue => issue.Field == "InactivityTimeout" && issue.Code == "REQUIRED");

        await host.PostAsync("/api/v1/auth/logout", new { }, token);
        var unauthenticated = await host.PutAsync(SettingPath, new { inactivityTimeout = "30" }, token);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        await AssertApiErrorAsync(unauthenticated, "UNAUTHENTICATED");

        var withoutToken = await host.SendJsonAsync(HttpMethod.Put, SettingPath, new { inactivityTimeout = "30" }, "stale");
        Assert.Equal(HttpStatusCode.Forbidden, withoutToken.StatusCode);
    }

    [Fact]
    public void DomainAcceptsOnlyTheOfferedChoicesAndFallsBackToTheConfiguredDefault()
    {
        var owner = OwnerAccount.Create("owner", "OWNER", new byte[16], new byte[32], 600_000, new byte[16], new byte[32], DateTimeOffset.UnixEpoch);
        var configuredDefault = TimeSpan.FromMinutes(45);
        Assert.Equal(configuredDefault, owner.EffectiveInactivityTimeout(configuredDefault));

        owner.SetInactivityTimeout(60, DateTimeOffset.UnixEpoch);
        Assert.Equal(TimeSpan.FromMinutes(60), owner.EffectiveInactivityTimeout(configuredDefault));
        owner.SetInactivityTimeout(null, DateTimeOffset.UnixEpoch);
        Assert.Null(owner.EffectiveInactivityTimeout(configuredDefault));
        Assert.Throws<ArgumentOutOfRangeException>(() => owner.SetInactivityTimeout(7, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task EfModelHasNoPendingChangesVersusTheLatestMigration()
    {
        await using var host = new TestHost();
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
        Assert.False(
            db.Database.HasPendingModelChanges(),
            "The EF Core model differs from the latest migration. Run: dotnet ef migrations add <Name> -p src/SmartSchoolTimetable.Infrastructure -s src/SmartSchoolTimetable.Infrastructure");
    }
}
