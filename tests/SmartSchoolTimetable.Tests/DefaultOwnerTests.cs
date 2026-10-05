using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SmartSchoolTimetable.Application;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests;

/// <summary>The default owner account (ADR 0025): created once on an empty database, never overwritten, password changeable.</summary>
public sealed class DefaultOwnerTests
{
    [Fact]
    public async Task AFreshDatabaseOpensAtLoginWithTheDefaultAccount()
    {
        await using var host = new TestHost(defaultOwner: true);
        var bootstrap = await host.GetBootstrapAsync();
        Assert.False(bootstrap.SetupRequired);
        Assert.False(bootstrap.Authenticated);
        Assert.Equal(HttpStatusCode.Conflict, (await host.PostAsync("/api/v1/auth/setup",
            new { username = "other", password = "A-Strong-Passphrase-401", confirmPassword = "A-Strong-Passphrase-401" }, bootstrap.LaunchToken)).StatusCode);

        var login = await host.PostAsync("/api/v1/auth/login", new { username = "admin", password = "Admin@12345" }, bootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var signedIn = await host.GetBootstrapAsync();
        Assert.True(signedIn.Authenticated);
        Assert.False(signedIn.RecoveryCodeAcknowledgementRequired); // the hidden first code is never asked for

        var changed = await host.PostAsync("/api/v1/auth/change-password",
            new { currentPassword = "Admin@12345", newPassword = "A-Changed-Strong-Passphrase-805" }, bootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        // A later start never recreates or resets the account.
        await using (var scope = host.Services.CreateAsyncScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<ILocalAuthService>().EnsureDefaultOwnerAsync("admin", "Admin@12345", default));
        var fresh = await host.GetBootstrapAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.PostAsync("/api/v1/auth/login", new { username = "admin", password = "Admin@12345" }, fresh.LaunchToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsync("/api/v1/auth/login", new { username = "admin", password = "A-Changed-Strong-Passphrase-805" }, fresh.LaunchToken)).StatusCode);
    }

    [Fact]
    public async Task AnInvalidConfiguredAccountIsRefused()
    {
        await using var host = new TestHost();
        await using var scope = host.Services.CreateAsyncScope();
        var auth = scope.ServiceProvider.GetRequiredService<ILocalAuthService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => auth.EnsureDefaultOwnerAsync("admin", "short", default));
        Assert.True((await host.GetBootstrapAsync()).SetupRequired);
    }
}
