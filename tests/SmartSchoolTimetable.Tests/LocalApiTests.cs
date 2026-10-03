using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Api;
using SmartSchoolTimetable.Infrastructure;

using static SmartSchoolTimetable.Tests.ApiTestDefaults;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests;

public sealed class LocalApiTests
{

    [Fact]
    public async Task SetupCreatesSingleOwnerWithOneTimeCodeHashedCredentialsWalAndAudit()
    {
        await using var host = new TestHost();
        var bootstrap = await host.GetBootstrapAsync();
        Assert.True(bootstrap.SetupRequired);
        Assert.False(bootstrap.Authenticated);

        var setup = await host.PostAsync(
            "/api/v1/auth/setup",
            new
            {
                username = "owner",
                password = "A-Strong-Passphrase-401",
                confirmPassword = "A-Strong-Passphrase-401"
            },
            bootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.Created, setup.StatusCode);
        var response = await ReadAsync<SetupResponse>(setup);
        Assert.Matches("^[A-F0-9]{8}(-[A-F0-9]{8}){3}$", response.RecoveryCode);
        Assert.Equal(35, response.RecoveryCode.Length);
        Assert.Equal(128, LocalAuthService.RecoveryCodeEntropyBits);

        var repeatedSetup = await host.PostAsync(
            "/api/v1/auth/setup",
            new
            {
                username = "another",
                password = "A-Strong-Passphrase-401",
                confirmPassword = "A-Strong-Passphrase-401"
            },
            bootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.Conflict, repeatedSetup.StatusCode);

        var afterSetup = await host.GetBootstrapAsync();
        Assert.False(afterSetup.SetupRequired);
        Assert.DoesNotContain(response.RecoveryCode, JsonSerializer.Serialize(afterSetup, JsonOptions));

        Assert.Contains(setup.Headers.GetValues("Set-Cookie"), value =>
            value.Contains("HttpOnly", StringComparison.OrdinalIgnoreCase));
        var authenticatedBootstrap = await host.GetBootstrapAsync();
        Assert.True(authenticatedBootstrap.Authenticated);
        Assert.True(authenticatedBootstrap.RecoveryCodeAcknowledgementRequired);

        var dbOptions = new DbContextOptionsBuilder<LocalDbContext>()
            .UseSqlite($"Data Source={host.DatabasePath};Pooling=False")
            .Options;
        await using var db = new LocalDbContext(dbOptions);
        var storedAccount = await db.Owners.SingleAsync();
        Assert.False(Encoding.UTF8.GetBytes("A-Strong-Passphrase-401").SequenceEqual(storedAccount.PasswordHash));
        Assert.Equal(600_000, storedAccount.PasswordIterations);
        Assert.False(Encoding.UTF8.GetBytes(response.RecoveryCode).SequenceEqual(storedAccount.RecoveryCodeHash));
        Assert.Equal(16, storedAccount.PasswordSalt.Length);
        Assert.Equal(32, storedAccount.PasswordHash.Length);
        Assert.Equal(16, storedAccount.RecoverySalt.Length);
        Assert.Equal(32, storedAccount.RecoveryCodeHash.Length);
        Assert.Single(await db.Owners.ToListAsync());
        var auditEntry = await db.AuditEntries.SingleAsync();
        Assert.Equal("OwnerAccountCreated", auditEntry.EventType);
        Assert.DoesNotContain("A-Strong-Passphrase-401", auditEntry.Summary);
        Assert.DoesNotContain(response.RecoveryCode, auditEntry.Summary);
        Assert.Contains("20261003105756_InitialLocalSchema", await db.Database.GetAppliedMigrationsAsync());

        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("wal", (string?)await command.ExecuteScalarAsync());
    }

    [Fact]
    public void PasswordHasherUsesPbkdf2Sha256AtOrAboveTheRequiredIterationCount()
    {
        var hasher = new Pbkdf2CredentialHasher();
        const string password = "A-Strong-Passphrase-401";
        Assert.True(Pbkdf2CredentialHasher.Iterations >= 600_000);
        Assert.Equal(600_000, hasher.CurrentPasswordIterations);

        var (salt, hash) = hasher.HashPassword(password);
        var independentlyDerivedHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            600_000,
            HashAlgorithmName.SHA256,
            32);

        Assert.Equal(16, salt.Length);
        Assert.Equal(32, hash.Length);
        Assert.Equal(independentlyDerivedHash, hash);
        Assert.True(hasher.VerifyPassword(password, salt, hash, 600_000));
        Assert.False(hasher.VerifyPassword("A-different-passphrase", salt, hash, 600_000));
    }

    [Fact]
    public async Task EfCommandLoggingIsWarningByDefaultAndSensitiveDataLoggingIsDisabled()
    {
        var repositoryRoot = TestPaths.FindRepositoryRoot();
        using var defaultSettings = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "SmartSchoolTimetable.Api", "appsettings.json")));
        using var developmentSettings = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "SmartSchoolTimetable.Api", "appsettings.Development.json")));

        Assert.Equal(
            "Warning",
            defaultSettings.RootElement.GetProperty("Logging").GetProperty("LogLevel")
                .GetProperty("Microsoft.EntityFrameworkCore.Database.Command").GetString());
        Assert.Equal(
            "Information",
            developmentSettings.RootElement.GetProperty("Logging").GetProperty("LogLevel")
                .GetProperty("Microsoft.EntityFrameworkCore.Database.Command").GetString());

        await using var host = new TestHost();
        var options = host.Services.GetRequiredService<DbContextOptions<LocalDbContext>>();
        var coreOptions = Assert.Single(options.Extensions.OfType<CoreOptionsExtension>());
        Assert.False(coreOptions.IsSensitiveDataLoggingEnabled);
    }

    [Fact]
    public void EveryApiErrorCodeHasAnArabicDictionaryEntry()
    {
        var dictionary = File.ReadAllText(Path.Combine(
            TestPaths.FindRepositoryRoot(),
            "frontend",
            "src",
            "i18n",
            "ar",
            "errors.ts"));

        foreach (var code in ApiErrorCodes.All)
        {
            var match = Regex.Match(
                dictionary,
                $@"^\s{{2}}{Regex.Escape(code)}:\s*""([^""]+)""",
                RegexOptions.Multiline);
            Assert.True(match.Success, $"Arabic error dictionary is missing API error code {code}.");
            Assert.Matches(@"\p{IsArabic}", match.Groups[1].Value);
        }
    }

    [Fact]
    public void InactivityTimeoutSupportsNeverWithoutExpiringSessions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalHost:Port"] = "5080",
                ["Authentication:InactivityTimeoutMinutes"] = "Never"
            })
            .Build();
        var options = LocalApplicationOptions.FromConfiguration(configuration);
        Assert.Null(options.InactivityTimeout);
        Assert.Null(options.InactivityTimeoutMinutes);

        var clock = new TestTimeProvider(TestClockStart);
        var store = new LocalSessionStore(clock);
        var sessionId = store.Issue("owner");
        clock.Advance(TimeSpan.FromDays(365));
        Assert.True(store.TryValidateAndTouch(sessionId, options.InactivityTimeout, out _));
    }

    [Fact]
    public async Task FrameworkAndValidationFailuresUseUnifiedApiErrorContract()
    {
        await using var host = new TestHost();
        var bootstrap = await host.GetBootstrapAsync();

        var notFound = await host.Client.GetAsync("/api/v1/no-such-endpoint");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        await AssertApiErrorAsync(notFound, "NOT_FOUND");

        using var unsupportedMethod = new HttpRequestMessage(HttpMethod.Put, "/api/v1/bootstrap");
        unsupportedMethod.Headers.TryAddWithoutValidation(
            "Origin",
            LocalOrigin.ToString().TrimEnd('/'));
        unsupportedMethod.Headers.TryAddWithoutValidation(
            "X-Local-Launch-Token",
            bootstrap.LaunchToken);
        var methodFailure = await host.Client.SendAsync(unsupportedMethod);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, methodFailure.StatusCode);
        await AssertApiErrorAsync(methodFailure, "METHOD_NOT_ALLOWED");

        using var unsupportedMediaRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = new StringContent("{}", Encoding.UTF8, "text/plain")
        };
        unsupportedMediaRequest.Headers.TryAddWithoutValidation("Origin", LocalOrigin.ToString().TrimEnd('/'));
        unsupportedMediaRequest.Headers.TryAddWithoutValidation("X-Local-Launch-Token", bootstrap.LaunchToken);
        var unsupportedMedia = await host.Client.SendAsync(unsupportedMediaRequest);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupportedMedia.StatusCode);
        await AssertApiErrorAsync(unsupportedMedia, "UNSUPPORTED_MEDIA_TYPE");

        using var invalidSetupRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/setup")
        {
            Content = JsonContent.Create(new
            {
                username = "owner",
                password = "A-Strong-Passphrase-401",
                confirmPassword = "Does-Not-Match-Password"
            })
        };
        invalidSetupRequest.Headers.TryAddWithoutValidation("Origin", LocalOrigin.ToString().TrimEnd('/'));
        invalidSetupRequest.Headers.TryAddWithoutValidation("X-Local-Launch-Token", bootstrap.LaunchToken);
        var invalidSetup = await host.Client.SendAsync(invalidSetupRequest);
        Assert.Equal((HttpStatusCode)422, invalidSetup.StatusCode);
        var validationError = await AssertApiErrorAsync(invalidSetup, "VALIDATION_FAILED");
        Assert.Contains(validationError.Errors, issue =>
            issue.Field == "ConfirmPassword" && issue.Code == "PASSWORD_MISMATCH");

        var unhandled = await CreateUnhandledApiResponseAsync();
        Assert.Equal(StatusCodes.Status500InternalServerError, unhandled.StatusCode);
        using var internalErrorJson = JsonDocument.Parse(unhandled.Body);
        Assert.Equal("INTERNAL_ERROR", internalErrorJson.RootElement.GetProperty("code").GetString());
        Assert.Equal(unhandled.CorrelationId,
            internalErrorJson.RootElement.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("title", unhandled.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("detail", unhandled.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PasswordAndRecoveryCodeAreNeverWrittenToApplicationLogs()
    {
        await using var host = new TestHost();
        var (token, recoveryCode) = await SetupOwnerAsync(host);

        var recovery = await host.PostAsync(
            "/api/v1/auth/recovery",
            new { recoveryCode, newPassword = "A-New-Strong-Passphrase-802" },
            token);
        Assert.Equal(HttpStatusCode.OK, recovery.StatusCode);
        var replacementCode = (await ReadAsync<RecoveryResponse>(recovery)).RecoveryCode;

        Assert.DoesNotContain(host.LogProvider.Messages, message =>
            message.Contains("A-Strong-Passphrase-401", StringComparison.Ordinal) ||
            message.Contains("A-New-Strong-Passphrase-802", StringComparison.Ordinal) ||
            message.Contains(recoveryCode, StringComparison.Ordinal) ||
            message.Contains(replacementCode, StringComparison.Ordinal));
    }

    [Fact]
    public void LoginPasswordVisibilityToggleHasAccessibleLabelAndPressedState()
    {
        var passwordField = File.ReadAllText(Path.Combine(
            TestPaths.FindRepositoryRoot(),
            "frontend",
            "src",
            "components",
            "PasswordField.tsx"));

        Assert.Contains("aria-label={toggleLabel}", passwordField);
        Assert.Contains("aria-pressed={visible}", passwordField);
        Assert.Contains("title={toggleLabel}", passwordField);
        Assert.Contains("type={visible ? \"text\" : \"password\"}", passwordField);
        Assert.Contains("EyeOff", passwordField);
        Assert.Contains("Eye", passwordField);
    }

    [Fact]
    public async Task LoginCookieHasRequiredAttributesAndOmitsSecureOnLoopbackHttp()
    {
        Assert.Equal("http", LocalOrigin.Scheme);
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);

        var login = await host.PostAsync(
            "/api/v1/auth/login",
            new { username = "owner", password = "A-Strong-Passphrase-401" },
            token);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        var attributes = cookie.Split(';').Select(attribute => attribute.Trim()).ToArray();
        Assert.Contains(attributes, attribute =>
            string.Equals(attribute, "HttpOnly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attributes, attribute =>
            string.Equals(attribute, "SameSite=Strict", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attributes, attribute =>
            string.Equals(attribute, "Path=/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(attributes, attribute =>
            string.Equals(attribute, "Secure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecoveryCodeResetsPasswordOnceAndIssuesReplacement()
    {
        await using var host = new TestHost();
        var (token, originalCode) = await SetupOwnerAsync(host);

        var recovered = await host.PostAsync(
            "/api/v1/auth/recovery",
            new { recoveryCode = originalCode, newPassword = "A-New-Strong-Passphrase-802" },
            token);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        var replacement = (await ReadAsync<RecoveryResponse>(recovered)).RecoveryCode;
        Assert.NotEqual(originalCode, replacement);

        var reusedCode = await host.PostAsync(
            "/api/v1/auth/recovery",
            new { recoveryCode = originalCode, newPassword = "A-Third-Strong-Passphrase-803" },
            token);
        Assert.Equal(HttpStatusCode.Unauthorized, reusedCode.StatusCode);

        var oldPassword = await host.PostAsync(
            "/api/v1/auth/login",
            new { username = "owner", password = "A-Strong-Passphrase-401" },
            token);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);

        var newPassword = await host.PostAsync(
            "/api/v1/auth/login",
            new { username = "owner", password = "A-New-Strong-Passphrase-802" },
            token);
        Assert.Equal(HttpStatusCode.NoContent, newPassword.StatusCode);
        Assert.Contains(newPassword.Headers.GetValues("Set-Cookie"), value =>
            value.Contains("HttpOnly", StringComparison.OrdinalIgnoreCase) &&
            value.Contains("SameSite=Strict", StringComparison.OrdinalIgnoreCase));

        var dbOptions = new DbContextOptionsBuilder<LocalDbContext>()
            .UseSqlite($"Data Source={host.DatabasePath};Pooling=False")
            .Options;
        await using var db = new LocalDbContext(dbOptions);
        var auditEntry = await db.AuditEntries.OrderByDescending(entry => entry.Id).FirstAsync();
        Assert.Equal("PasswordChanged", auditEntry.EventType);
        Assert.DoesNotContain(originalCode, auditEntry.Summary);
    }

    [Fact]
    public async Task WrongPasswordsApplyFixedOneSecondDelayWithoutTemporaryLockout()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var invalid = await host.PostAsync(
                "/api/v1/auth/login",
                new { username = "owner", password = "Wrong-Password-Not-Valid" },
                token);
            Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        }

        Assert.Equal(Enumerable.Repeat(TimeSpan.FromSeconds(1), 6), host.LoginDelay.Delays);
    }

    [Fact]
    public async Task RecoveryCodeCanBeRegeneratedOnlyWithCurrentPasswordAndMustBeAcknowledged()
    {
        await using var host = new TestHost();
        var (token, oldCode) = await SetupOwnerAsync(host);
        await host.PostAsync("/api/v1/auth/logout", new { }, token);
        var signIn = await host.PostAsync(
            "/api/v1/auth/login",
            new { username = "owner", password = "A-Strong-Passphrase-401" },
            token);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        var prematureAcknowledgement = await host.PostAsync(
            "/api/v1/auth/recovery-code/acknowledge",
            new { },
            token);
        Assert.Equal(HttpStatusCode.Conflict, prematureAcknowledgement.StatusCode);
        Assert.Equal("RECOVERY_MISSING",
            (await ReadAsync<ApiErrorResponse>(prematureAcknowledgement)).Code);

        var wrongPassword = await host.PostAsync(
            "/api/v1/auth/recovery-code/regenerate",
            new { currentPassword = "Wrong-Password-Not-Valid" },
            token);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal("CURRENT_PASSWORD_INCORRECT", (await ReadAsync<ApiErrorResponse>(wrongPassword)).Code);

        var regenerated = await host.PostAsync(
            "/api/v1/auth/recovery-code/regenerate",
            new { currentPassword = "A-Strong-Passphrase-401" },
            token);
        Assert.Equal(HttpStatusCode.OK, regenerated.StatusCode);
        var newCode = (await ReadAsync<RecoveryResponse>(regenerated)).RecoveryCode;
        Assert.NotEqual(oldCode, newCode);
        Assert.True((await host.GetBootstrapAsync()).RecoveryCodeAcknowledgementRequired);

        var oldCodeRecovery = await host.PostAsync(
            "/api/v1/auth/recovery",
            new { recoveryCode = oldCode, newPassword = "A-New-Strong-Passphrase-802" },
            token);
        Assert.Equal(HttpStatusCode.Unauthorized, oldCodeRecovery.StatusCode);

        var acknowledged = await host.PostAsync(
            "/api/v1/auth/recovery-code/acknowledge",
            new { },
            token);
        Assert.Equal(HttpStatusCode.NoContent, acknowledged.StatusCode);
        Assert.False((await host.GetBootstrapAsync()).RecoveryCodeAcknowledgementRequired);
    }

    [Fact]
    public async Task ChangePasswordRequiresCurrentPasswordAndInvalidatesSession()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var login = await host.PostAsync(
            "/api/v1/auth/login",
            new { username = "owner", password = "A-Strong-Passphrase-401" },
            token);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        var wrongCurrentPassword = await host.PostAsync(
            "/api/v1/auth/change-password",
            new { currentPassword = "Not-The-Current-Password", newPassword = "A-Changed-Strong-Passphrase-805" },
            token);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongCurrentPassword.StatusCode);

        var changed = await host.PostAsync(
            "/api/v1/auth/change-password",
            new { currentPassword = "A-Strong-Passphrase-401", newPassword = "A-Changed-Strong-Passphrase-805" },
            token);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        var privateStatus = await host.Client.GetAsync("/api/v1/private/status");
        Assert.Equal(HttpStatusCode.Unauthorized, privateStatus.StatusCode);

        var dbOptions = new DbContextOptionsBuilder<LocalDbContext>()
            .UseSqlite($"Data Source={host.DatabasePath};Pooling=False")
            .Options;
        await using var db = new LocalDbContext(dbOptions);
        Assert.Contains(await db.AuditEntries.Select(entry => entry.EventType).ToListAsync(), type =>
            type == "PasswordChanged");
    }

    [Fact]
    public async Task LogoutRevokesAuthenticatedSessionImmediately()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var login = await host.PostAsync(
            "/api/v1/auth/login",
            new { username = "owner", password = "A-Strong-Passphrase-401" },
            token);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/v1/private/status")).StatusCode);

        var logout = await host.PostAsync("/api/v1/auth/logout", new { }, token);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/private/status")).StatusCode);
    }

    [Fact]
    public async Task InactivityTimeoutLocksAuthenticatedSessionAndProtectsPrivateRoutes()
    {
        await using var host = new TestHost();
        Assert.Same(host.Clock, host.Services.GetRequiredService<TimeProvider>());
        var (token, _) = await SetupOwnerAsync(host);
        Assert.Equal(5, (await host.GetBootstrapAsync()).InactivityTimeoutMinutes);

        var login = await host.PostAsync(
            "/api/v1/auth/login",
            new { username = "owner", password = "A-Strong-Passphrase-401" },
            token);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/v1/private/status")).StatusCode);

        host.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/private/status")).StatusCode);
        Assert.False((await host.GetBootstrapAsync()).Authenticated);
    }

    [Fact]
    public async Task MaliciousCrossOriginAndDnsRebindingRequestsAreRejected()
    {
        await using var host = new TestHost();
        var bootstrap = await host.GetBootstrapAsync();
        await using var secondLaunch = new TestHost();
        var secondLaunchBootstrap = await secondLaunch.GetBootstrapAsync();
        Assert.NotEqual(bootstrap.LaunchToken, secondLaunchBootstrap.LaunchToken);

        var noToken = await host.PostWithoutTokenAsync(
            "/api/v1/auth/setup",
            new
            {
                username = "owner",
                password = "A-Strong-Passphrase-401",
                confirmPassword = "A-Strong-Passphrase-401"
            });
        Assert.Equal(HttpStatusCode.Forbidden, noToken.StatusCode);

        using var noOriginRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/auth/setup")
        {
            Content = JsonContent.Create(new
            {
                username = "owner",
                password = "A-Strong-Passphrase-401",
                confirmPassword = "A-Strong-Passphrase-401"
            })
        };
        noOriginRequest.Headers.TryAddWithoutValidation(
            "X-Local-Launch-Token",
            bootstrap.LaunchToken);
        var noOrigin = await host.Client.SendAsync(noOriginRequest);
        Assert.Equal(HttpStatusCode.Forbidden, noOrigin.StatusCode);
        await AssertApiErrorAsync(noOrigin, "INVALID_ORIGIN");

        var staleToken = await host.PostAsync(
            "/api/v1/auth/setup",
            new
            {
                username = "owner",
                password = "A-Strong-Passphrase-401",
                confirmPassword = "A-Strong-Passphrase-401"
            },
            secondLaunchBootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.Forbidden, staleToken.StatusCode);

        var wrongOrigin = await host.PostAsync(
            "/api/v1/auth/setup",
            new
            {
                username = "owner",
                password = "A-Strong-Passphrase-401",
                confirmPassword = "A-Strong-Passphrase-401"
            },
            bootstrap.LaunchToken,
            "http://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, wrongOrigin.StatusCode);

        host.Client.DefaultRequestHeaders.Host = "localhost:5080";
        var wrongHost = await host.Client.GetAsync("/api/v1/bootstrap");
        Assert.Equal(HttpStatusCode.BadRequest, wrongHost.StatusCode);
        host.Client.DefaultRequestHeaders.Host = LocalOrigin.Authority;

        using var reboundHostRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/bootstrap");
        reboundHostRequest.Headers.Host = "127.0.0.1.attacker.example:5080";
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.SendAsync(reboundHostRequest)).StatusCode);

        var corsProbe = new HttpRequestMessage(HttpMethod.Get, "/api/v1/bootstrap");
        corsProbe.Headers.TryAddWithoutValidation("Origin", "http://evil.example");
        var corsResponse = await host.Client.SendAsync(corsProbe);
        Assert.Equal(HttpStatusCode.Forbidden, corsResponse.StatusCode);
        Assert.False(corsResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void ListenerGuardRejectsAnyAddressOtherThanCanonicalLoopback()
    {
        LocalListenerGuard.ValidateConfiguredEndpoint(IPAddress.Loopback, 5080);
        LocalListenerGuard.ValidateBoundAddresses(["http://127.0.0.1:5080"], 5080);
        Assert.Throws<InvalidOperationException>(() =>
            LocalListenerGuard.ValidateConfiguredEndpoint(IPAddress.Any, 5080));
        Assert.Throws<InvalidOperationException>(() =>
            LocalListenerGuard.ValidateBoundAddresses(["http://0.0.0.0:5080"], 5080));
        Assert.Throws<InvalidOperationException>(() =>
            LocalListenerGuard.ValidateBoundAddresses(["http://*:5080"], 5080));
        Assert.Throws<InvalidOperationException>(() =>
            LocalListenerGuard.ValidateBoundAddresses(["http://+:5080"], 5080));
        Assert.Throws<InvalidOperationException>(() =>
            LocalListenerGuard.ValidateBoundAddresses(["http://192.168.1.25:5080"], 5080));
        Assert.Throws<InvalidOperationException>(() =>
            LocalListenerGuard.ValidateBoundAddresses(["http://127.0.0.1:5080", "http://[::]:5080"], 5080));
    }

    [Fact]
    public async Task LiveKestrelStartupBindsOnlyToLoopback()
    {
        var repositoryRoot = TestPaths.FindRepositoryRoot();
        var buildConfiguration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif
        var apiDirectory = Path.Combine(repositoryRoot, "src", "SmartSchoolTimetable.Api");
        var apiAssembly = Path.Combine(apiDirectory, "bin", buildConfiguration, "net9.0", "SmartSchoolTimetable.Api.dll");
        Assert.True(File.Exists(apiAssembly), $"The API must be built before this test: {apiAssembly}");

        using var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();

        var directory = Path.Combine(Path.GetTempPath(), $"smart-school-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "startup.db");
        var startInfo = new ProcessStartInfo(FindDotnetHost())
        {
            WorkingDirectory = apiDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(apiAssembly);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        startInfo.Environment["Database__Path"] = databasePath;
        startInfo.Environment["LocalHost__Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);

        using var process = new Process { StartInfo = startInfo };
        Task<string>? outputTask = null;
        Task<string>? errorTask = null;
        string stdout = string.Empty;
        string stderr = string.Empty;
        var started = false;
        try
        {
            Assert.True(process.Start(), "Could not start the API process.");
            started = true;
            outputTask = process.StandardOutput.ReadToEndAsync();
            errorTask = process.StandardError.ReadToEndAsync();
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            HttpResponseMessage? response = null;
            for (var attempt = 0; attempt < 100 && !process.HasExited; attempt++)
            {
                try
                {
                    response = await client.GetAsync("/api/v1/bootstrap");
                    if (response.IsSuccessStatusCode)
                        break;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(100);
                }
            }

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(process.HasExited, "The API stopped after failing its production listener validation.");
            response.Dispose();
        }
        finally
        {
            if (started)
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                stdout = await outputTask!;
                stderr = await errorTask!;
            }
            Directory.Delete(directory, recursive: true);
        }

        var logs = stdout + stderr;
        Assert.Contains($"Now listening on: http://127.0.0.1:{port}", logs);
        Assert.DoesNotContain($"Now listening on: http://0.0.0.0:{port}", logs);
    }

    [Fact]
    public async Task ChangePasswordWithBogusOrExpiredSessionCookieReturns401Unauthenticated()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var changePasswordBody = new
        {
            currentPassword = "A-Strong-Passphrase-401",
            newPassword = "A-Changed-Strong-Passphrase-805"
        };

        using var bogusClient = host.CreateClientWithoutCookies();
        using var bogusRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(changePasswordBody)
        };
        bogusRequest.Headers.TryAddWithoutValidation("Origin", LocalOrigin.ToString().TrimEnd('/'));
        bogusRequest.Headers.TryAddWithoutValidation("X-Local-Launch-Token", token);
        bogusRequest.Headers.TryAddWithoutValidation("Cookie", $"{AuthEndpoints.SessionCookieName}=not-a-real-session");
        var bogus = await bogusClient.SendAsync(bogusRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, bogus.StatusCode);
        await AssertApiErrorAsync(bogus, "UNAUTHENTICATED");

        // The setup session is valid; advance past the 5-minute test inactivity timeout to expire it.
        host.Clock.Advance(TimeSpan.FromMinutes(6));
        var expired = await host.PostAsync("/api/v1/auth/change-password", changePasswordBody, token);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        await AssertApiErrorAsync(expired, "UNAUTHENTICATED");

        // The password was not changed by either rejected request.
        var login = await host.PostAsync(
            "/api/v1/auth/login",
            new { username = "owner", password = "A-Strong-Passphrase-401" },
            token);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
    }

    [Fact]
    public async Task PasswordMinimumIsEightCharacters()
    {
        Assert.Equal(8, CredentialRules.PasswordMinLength);
        await using var host = new TestHost();
        var bootstrap = await host.GetBootstrapAsync();

        var tooShort = await host.PostAsync(
            "/api/v1/auth/setup",
            new { username = "owner", password = "Seven-7", confirmPassword = "Seven-7" },
            bootstrap.LaunchToken);
        Assert.Equal((HttpStatusCode)422, tooShort.StatusCode);
        var error = await AssertApiErrorAsync(tooShort, "VALIDATION_FAILED");
        Assert.Contains(error.Errors, issue => issue.Field == "Password" && issue.Code == "PASSWORD_TOO_SHORT");

        var eight = await host.PostAsync(
            "/api/v1/auth/setup",
            new { username = "owner", password = "Eight-88", confirmPassword = "Eight-88" },
            bootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.Created, eight.StatusCode);

        var changeToSeven = await host.PostAsync(
            "/api/v1/auth/change-password",
            new { currentPassword = "Eight-88", newPassword = "Seven-7" },
            bootstrap.LaunchToken);
        Assert.Equal((HttpStatusCode)422, changeToSeven.StatusCode);

        var loginWithEight = await host.PostAsync(
            "/api/v1/auth/login",
            new { username = "owner", password = "Eight-88" },
            bootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.NoContent, loginWithEight.StatusCode);
    }

    [Fact]
    public async Task EveryEfCoreConnectionAppliesSynchronousFull()
    {
        await using var host = new TestHost();
        var options = host.Services.GetRequiredService<DbContextOptions<LocalDbContext>>();
        var coreOptions = Assert.Single(options.Extensions.OfType<CoreOptionsExtension>());
        Assert.Contains(SqlitePragmaInterceptor.Instance, coreOptions.Interceptors ?? []);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
            await db.Database.OpenConnectionAsync();
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA synchronous;";
            Assert.Equal(2L, Assert.IsType<long>(await command.ExecuteScalarAsync()));
        }

        // Prove the interceptor itself changes the setting, independent of SQLite's compiled default.
        await using var connection = new SqliteConnection($"Data Source={host.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        await using (var off = connection.CreateCommand())
        {
            off.CommandText = "PRAGMA synchronous=OFF;";
            await off.ExecuteNonQueryAsync();
        }
        await using (var offCheck = connection.CreateCommand())
        {
            offCheck.CommandText = "PRAGMA synchronous;";
            Assert.Equal(0L, Assert.IsType<long>(await offCheck.ExecuteScalarAsync()));
        }
        await SqlitePragmaInterceptor.ApplyAsync(connection, CancellationToken.None);
        await using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA synchronous;";
        Assert.Equal(2L, Assert.IsType<long>(await check.ExecuteScalarAsync()));
    }

    [Fact]
    public void ArchitectureDependenciesFlowInward()
    {
        var domainReferences = ReferencedAssemblyNames(typeof(SmartSchoolTimetable.Domain.OwnerAccount).Assembly);
        var applicationReferences = ReferencedAssemblyNames(typeof(ILocalAuthService).Assembly);
        var infrastructureReferences = ReferencedAssemblyNames(typeof(LocalDbContext).Assembly);

        Assert.DoesNotContain("SmartSchoolTimetable.Application", domainReferences);
        Assert.DoesNotContain("SmartSchoolTimetable.Infrastructure", domainReferences);
        Assert.DoesNotContain("SmartSchoolTimetable.Api", domainReferences);
        Assert.Contains("SmartSchoolTimetable.Domain", applicationReferences);
        Assert.DoesNotContain("SmartSchoolTimetable.Infrastructure", applicationReferences);
        Assert.DoesNotContain("SmartSchoolTimetable.Api", applicationReferences);
        Assert.Contains("SmartSchoolTimetable.Application", infrastructureReferences);
        Assert.Contains("SmartSchoolTimetable.Domain", infrastructureReferences);
        Assert.DoesNotContain("SmartSchoolTimetable.Api", infrastructureReferences);
    }

    [Fact]
    public void LocalDatabaseResetRequiresExactConfirmationAndRemovesOnlyDatabaseFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"smart-school-reset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "local.db");
        var sidecarPaths = new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm", $"{databasePath}-journal" };
        try
        {
            foreach (var path in sidecarPaths)
                File.WriteAllText(path, "test");

            using var declinedOutput = new StringWriter();
            Assert.False(LocalDatabaseReset.DeleteAfterConfirmation(
                databasePath,
                new StringReader("reset"),
                declinedOutput));
            Assert.All(sidecarPaths, path => Assert.True(File.Exists(path)));
            Assert.Contains(LocalDatabaseReset.CancelledMessage, declinedOutput.ToString());
            Assert.Matches(@"\p{IsArabic}", declinedOutput.ToString());
            Assert.DoesNotMatch("Reset cancelled|permanently deletes|to confirm", declinedOutput.ToString());

            using var confirmedOutput = new StringWriter();
            Assert.True(LocalDatabaseReset.DeleteAfterConfirmation(
                databasePath,
                new StringReader("RESET"),
                confirmedOutput));
            Assert.All(sidecarPaths, path => Assert.False(File.Exists(path)));
            Assert.Contains(LocalDatabaseReset.CompletedMessage, confirmedOutput.ToString());
            Assert.Contains(LocalDatabaseReset.WarningMessage, confirmedOutput.ToString());
            Assert.All(
                [LocalDatabaseReset.WarningMessage, LocalDatabaseReset.ConfirmationPrompt,
                 LocalDatabaseReset.CancelledMessage, LocalDatabaseReset.CompletedMessage],
                message => Assert.Matches(@"\p{IsArabic}", message));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AuthRoutesExposeNoAlternatePasswordRecoveryEndpoint()
    {
        await using var host = new TestHost();
        var routePatterns = host.Services.GetRequiredService<IEnumerable<EndpointDataSource>>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        Assert.Contains("/api/v1/auth/recovery", routePatterns);
        Assert.DoesNotContain(routePatterns, route =>
            route.Contains("forgot", StringComparison.OrdinalIgnoreCase) ||
            route.Contains("reset-password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LocalSessionStoreExpiresOnInactivityAndCanBeRevoked()
    {
        var clock = new TestTimeProvider(TestClockStart);
        var store = new LocalSessionStore(clock);
        var sessionId = store.Issue("owner");
        Assert.True(store.TryValidateAndTouch(sessionId, TimeSpan.FromMinutes(5), out var username));
        Assert.Equal("owner", username);

        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.True(store.TryValidateAndTouch(sessionId, TimeSpan.FromMinutes(5), out _));
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.False(store.TryValidateAndTouch(sessionId, TimeSpan.FromMinutes(5), out _));

        var secondSession = store.Issue("owner");
        store.Revoke(secondSession);
        Assert.False(store.TryValidateAndTouch(secondSession, TimeSpan.FromMinutes(5), out _));
    }

    private static async Task<(int StatusCode, string CorrelationId, string Body)> CreateUnhandledApiResponseAsync()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/test-error";
        context.Response.Body = new MemoryStream();
        var logger = NullLogger<UnifiedApiErrorMiddleware>.Instance;
        var middleware = new UnifiedApiErrorMiddleware(
            _ => throw new InvalidOperationException("Do not expose this internal detail."),
            logger);
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return (
            context.Response.StatusCode,
            context.Response.Headers["X-Correlation-ID"].ToString(),
            body);
    }

    private static HashSet<string> ReferencedAssemblyNames(System.Reflection.Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!).ToHashSet(StringComparer.Ordinal);

    private static string FindDotnetHost()
    {
        var configuredHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configuredHost) && File.Exists(configuredHost))
            return configuredHost;

        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        var executableName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            var fromRoot = Path.Combine(dotnetRoot, executableName);
            if (File.Exists(fromRoot))
                return fromRoot;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var standardInstall = Path.Combine(programFiles, "dotnet", executableName);
        return File.Exists(standardInstall) ? standardInstall : "dotnet";
    }
}
