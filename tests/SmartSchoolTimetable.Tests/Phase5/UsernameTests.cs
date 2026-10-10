using System.Net;
using System.Text.Json;
using SmartSchoolTimetable.Application;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>M2: changing the username with the current password; the session stays open, the old name stops working, history records it.</summary>
public sealed class UsernameTests
{
    private const string Password = "A-Strong-Passphrase-401";

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task ChangingTheNameKeepsTheSessionAndMovesTheLoginToTheNewName()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);

        var changed = await host.PostAsync("/api/v1/auth/change-username", new { currentPassword = Password, newUsername = "  مدير.المدرسة  " }, token);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        // The open session shows the new name at once and is still signed in.
        var status = await JsonAsync(await host.Client.GetAsync("/api/v1/bootstrap"));
        Assert.True(status.GetProperty("authenticated").GetBoolean());
        Assert.Equal("مدير.المدرسة", status.GetProperty("username").GetString());
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/v1/preferences/")).StatusCode);

        // After signing out only the new name logs in (the old one is refused with the same code as a wrong password).
        Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsync("/api/v1/auth/logout", new { }, token)).StatusCode);
        await AssertApiErrorAsync(await host.PostAsync("/api/v1/auth/login", new { username = "owner", password = Password }, token), ErrorCodes.InvalidCredentials);
        Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsync("/api/v1/auth/login", new { username = "مدير.المدرسة", password = Password }, token)).StatusCode);

        var history = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=account"));
        Assert.Contains(history.GetProperty("items").EnumerateArray(), entry => entry.GetProperty("eventType").GetString() == "UsernameChanged");
        Assert.DoesNotContain("مدير.المدرسة", await (await host.Client.GetAsync("/api/v1/audit/?category=account")).Content.ReadAsStringAsync()); // no name or secret in the history
    }

    [Fact]
    public async Task AWrongPasswordAnInvalidNameOrTheSameNameChangesNothing()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);

        await AssertApiErrorAsync(await host.PostAsync("/api/v1/auth/change-username", new { currentPassword = "not-the-password", newUsername = "someone" }, token), ErrorCodes.CurrentPasswordIncorrect);
        Assert.Equal("owner", (await JsonAsync(await host.Client.GetAsync("/api/v1/bootstrap"))).GetProperty("username").GetString());

        var tooShort = await AssertApiErrorAsync(await host.PostAsync("/api/v1/auth/change-username", new { currentPassword = Password, newUsername = "ab" }, token), ErrorCodes.ValidationFailed);
        Assert.Equal(("NewUsername", ErrorCodes.UsernameTooShort), (tooShort.Errors.Single().Field, tooShort.Errors.Single().Code));
        var tooLong = await AssertApiErrorAsync(await host.PostAsync("/api/v1/auth/change-username", new { currentPassword = Password, newUsername = new string('x', 65) }, token), ErrorCodes.ValidationFailed);
        Assert.Equal(ErrorCodes.UsernameTooLong, tooLong.Errors.Single().Code);
        await AssertApiErrorAsync(await host.PostAsync("/api/v1/auth/change-username", new { currentPassword = "", newUsername = "valid-name" }, token), ErrorCodes.ValidationFailed);

        // The same name: success, nothing recorded.
        Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsync("/api/v1/auth/change-username", new { currentPassword = Password, newUsername = "owner" }, token)).StatusCode);
        var history = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=account"));
        Assert.DoesNotContain(history.GetProperty("items").EnumerateArray(), entry => entry.GetProperty("eventType").GetString() == "UsernameChanged");

        // Signed out: no change either.
        Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsync("/api/v1/auth/logout", new { }, token)).StatusCode);
        await AssertApiErrorAsync(await host.PostAsync("/api/v1/auth/change-username", new { currentPassword = Password, newUsername = "other" }, token), ErrorCodes.Unauthenticated);
    }
}
