using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Api;
using SmartSchoolTimetable.Infrastructure;

namespace SmartSchoolTimetable.Tests;

public sealed class LocalApiTests
{
    private static readonly Uri LocalOrigin = new("http://127.0.0.1:5080");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SetupCreatesSingleOwnerWithOneTimeCodeHashedCredentialsWalAndAudit()
    {
        await using var host = new TestHost();
        var bootstrap = await host.GetBootstrapAsync();
        Assert.True(bootstrap.SetupRequired);
        Assert.False(bootstrap.Authenticated);

        var setup = await host.PostAsync(
            "/api/v1/auth/setup",
            new { username = "owner", password = "A-Strong-Passphrase-401" },
            bootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.Created, setup.StatusCode);
        var response = await ReadAsync<SetupResponse>(setup);
        Assert.Matches("^[A-F0-9]{8}(-[A-F0-9]{8}){3}$", response.RecoveryCode);

        var repeatedSetup = await host.PostAsync(
            "/api/v1/auth/setup",
            new { username = "another", password = "A-Strong-Passphrase-401" },
            bootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.Conflict, repeatedSetup.StatusCode);

        var afterSetup = await host.GetBootstrapAsync();
        Assert.False(afterSetup.SetupRequired);
        Assert.DoesNotContain(response.RecoveryCode, JsonSerializer.Serialize(afterSetup, JsonOptions));

        var setupScreen = await host.Client.GetStringAsync("/app.js");
        Assert.Contains("رمز الاسترداد هو المسار الوحيد", setupScreen);
        Assert.Contains("لن يُعرض هذا الرمز مرة أخرى", setupScreen);
        Assert.Contains("خزّنه في مكان آمن أو اطبعه الآن", setupScreen);

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
    public async Task WrongPasswordsApplyIncrementalDelayAndTemporaryLockout()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var invalid = await host.PostAsync(
                "/api/v1/auth/login",
                new { username = "owner", password = "Wrong-Password-Not-Valid" },
                token);
            if (attempt < 4)
                Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
            else
            {
                Assert.Equal((HttpStatusCode)423, invalid.StatusCode);
                Assert.True(invalid.Headers.Contains("Retry-After"));
            }
        }

        Assert.Contains(TimeSpan.FromSeconds(1), host.LoginDelay.Delays);
        Assert.Contains(TimeSpan.FromSeconds(2), host.LoginDelay.Delays);
        Assert.Contains(TimeSpan.FromSeconds(4), host.LoginDelay.Delays);
        Assert.Contains(TimeSpan.FromSeconds(8), host.LoginDelay.Delays);
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
        var (token, _) = await SetupOwnerAsync(host);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/private/status")).StatusCode);

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
            new { username = "owner", password = "A-Strong-Passphrase-401" });
        Assert.Equal(HttpStatusCode.Forbidden, noToken.StatusCode);

        var staleToken = await host.PostAsync(
            "/api/v1/auth/setup",
            new { username = "owner", password = "A-Strong-Passphrase-401" },
            secondLaunchBootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.Forbidden, staleToken.StatusCode);

        var wrongOrigin = await host.PostAsync(
            "/api/v1/auth/setup",
            new { username = "owner", password = "A-Strong-Passphrase-401" },
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
        var repositoryRoot = FindRepositoryRoot();
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
            Assert.Contains("Reset cancelled", declinedOutput.ToString());

            using var confirmedOutput = new StringWriter();
            Assert.True(LocalDatabaseReset.DeleteAfterConfirmation(
                databasePath,
                new StringReader("RESET"),
                confirmedOutput));
            Assert.All(sidecarPaths, path => Assert.False(File.Exists(path)));
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
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-10-03T10:00:00Z"));
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

    private static async Task<(string Token, string RecoveryCode)> SetupOwnerAsync(TestHost host)
    {
        var bootstrap = await host.GetBootstrapAsync();
        var response = await host.PostAsync(
            "/api/v1/auth/setup",
            new { username = "owner", password = "A-Strong-Passphrase-401" },
            bootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var setupResult = await ReadAsync<SetupResponse>(response);
        return (bootstrap.LaunchToken, setupResult.RecoveryCode);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"smart-school-tests-{Guid.NewGuid():N}");
        private readonly WebApplicationFactory<Program> _factory;

        public TestHost()
        {
            Directory.CreateDirectory(_directory);
            _factory = new TestApplicationFactory(DatabasePath);
            Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = LocalOrigin,
                HandleCookies = true
            });
        }

        public HttpClient Client { get; }
        public string DatabasePath => Path.Combine(_directory, "test.db");
        public TestTimeProvider Clock => _factory.Services.GetRequiredService<TestTimeProvider>();
        public RecordingLoginDelay LoginDelay => _factory.Services.GetRequiredService<RecordingLoginDelay>();
        public IServiceProvider Services => _factory.Services;

        public async Task<BootstrapResponse> GetBootstrapAsync() =>
            await ReadAsync<BootstrapResponse>(await Client.GetAsync("/api/v1/bootstrap"));

        public async Task<HttpResponseMessage> PostAsync(
            string path,
            object body,
            string token,
            string origin = "http://127.0.0.1:5080")
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.TryAddWithoutValidation("Origin", origin);
            request.Headers.TryAddWithoutValidation("X-Local-Launch-Token", token);
            return await Client.SendAsync(request);
        }

        public async Task<HttpResponseMessage> PostWithoutTokenAsync(string path, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.TryAddWithoutValidation("Origin", LocalOrigin.ToString().TrimEnd('/'));
            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _factory.DisposeAsync();
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class TestApplicationFactory(string databasePath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("LocalHost:Port", "5080");
            builder.UseSetting("Authentication:InactivityTimeoutMinutes", "5");
            builder.UseSetting("Database:Path", databasePath);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-10-03T10:00:00Z"));
                services.AddSingleton(clock);
                services.AddSingleton<TimeProvider>(clock);

                services.RemoveAll<ILoginDelay>();
                services.AddSingleton<RecordingLoginDelay>();
                services.AddSingleton<ILoginDelay>(provider =>
                    provider.GetRequiredService<RecordingLoginDelay>());
            });
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        private DateTimeOffset _currentTime = currentTime;

        public override DateTimeOffset GetUtcNow() => _currentTime;
        public void Advance(TimeSpan duration) => _currentTime += duration;
    }

    private sealed class RecordingLoginDelay : ILoginDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private static HashSet<string> ReferencedAssemblyNames(System.Reflection.Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!).ToHashSet(StringComparer.Ordinal);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SmartSchoolTimetable.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate SmartSchoolTimetable.sln from the test output directory.");
    }

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
