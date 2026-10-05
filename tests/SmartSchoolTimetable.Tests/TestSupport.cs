using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SmartSchoolTimetable.Api;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Infrastructure;
using static SmartSchoolTimetable.Tests.ApiTestDefaults;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests;

internal static class ApiTestDefaults
{
    public static readonly Uri LocalOrigin = new("http://127.0.0.1:5080");
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static readonly DateTimeOffset TestClockStart = new(2026, 10, 3, 10, 0, 0, TimeSpan.Zero);
}

internal static class ApiTestHelpers
{
    public static async Task<(string Token, string RecoveryCode)> SetupOwnerAsync(TestHost host)
    {
        var bootstrap = await host.GetBootstrapAsync();
        var response = await host.PostAsync(
            "/api/v1/auth/setup",
            new
            {
                username = "owner",
                password = "A-Strong-Passphrase-401",
                confirmPassword = "A-Strong-Passphrase-401"
            },
            bootstrap.LaunchToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var setupResult = await ReadAsync<SetupResponse>(response);
        return (bootstrap.LaunchToken, setupResult.RecoveryCode);
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;

    public static async Task<ApiErrorResponse> AssertApiErrorAsync(
        HttpResponseMessage response,
        string expectedCode)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(expectedCode, json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("correlationId").GetString()));
        Assert.False(json.RootElement.TryGetProperty("title", out _));
        Assert.False(json.RootElement.TryGetProperty("detail", out _));
        return (await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions))!;
    }
}

internal sealed class TestHost : IAsyncDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"smart-school-tests-{Guid.NewGuid():N}");
    private readonly TestApplicationFactory _factory;

    /// <param name="defaultOwner">Enables the default owner account (ADR 0025); off so tests go through setup.</param>
    public TestHost(bool defaultOwner = false)
    {
        Directory.CreateDirectory(_directory);
        _factory = new TestApplicationFactory(DatabasePath, defaultOwner);
        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = LocalOrigin,
            HandleCookies = true
        });
    }

    public HttpClient Client { get; }

    public HttpClient CreateClientWithoutCookies() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = LocalOrigin,
            HandleCookies = false
        });

    public string DatabasePath => Path.Combine(_directory, "test.db");
    public TestTimeProvider Clock => _factory.Services.GetRequiredService<TestTimeProvider>();
    public RecordingLoginDelay LoginDelay => _factory.Services.GetRequiredService<RecordingLoginDelay>();
    public CapturingLoggerProvider LogProvider => _factory.LogProvider;
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

    public async Task<HttpResponseMessage> PutAsync(string path, object body, string token) =>
        await SendJsonAsync(HttpMethod.Put, path, body, token);

    public async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string path,
        object body,
        string token,
        string origin = "http://127.0.0.1:5080")
    {
        using var request = new HttpRequestMessage(method, path)
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

internal sealed class TestApplicationFactory(string databasePath, bool defaultOwner = false) : WebApplicationFactory<Program>
{
    public CapturingLoggerProvider LogProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("LocalHost:Port", "5080");
        builder.UseSetting("Authentication:InactivityTimeoutMinutes", "5");
        builder.UseSetting("Database:Path", databasePath);
        builder.UseSetting("DefaultOwner:Enabled", defaultOwner ? "true" : "false");
        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(LogProvider);
            // Keep test output readable: EF migration and SQL command logs are noise at Information level.
            logging.AddFilter("Microsoft.EntityFrameworkCore.Migrations", LogLevel.Warning);
            logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            var clock = new TestTimeProvider(TestClockStart);
            services.AddSingleton(clock);
            services.AddSingleton<TimeProvider>(clock);

            services.RemoveAll<ILocalSessionStore>();
            services.AddSingleton<ILocalSessionStore>(_ => new LocalSessionStore(clock));

            services.RemoveAll<ILoginDelay>();
            services.AddSingleton<RecordingLoginDelay>();
            services.AddSingleton<ILoginDelay>(provider =>
                provider.GetRequiredService<RecordingLoginDelay>());
        });
    }
}

internal sealed class TestTimeProvider(DateTimeOffset currentTime) : TimeProvider
{
    private DateTimeOffset _currentTime = currentTime;

    public override DateTimeOffset GetUtcNow() => _currentTime;
    public void Advance(TimeSpan duration) => _currentTime += duration;
}

internal sealed class RecordingLoginDelay : ILoginDelay
{
    public List<TimeSpan> Delays { get; } = [];

    public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        Delays.Add(delay);
        return Task.CompletedTask;
    }
}

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Add(string categoryName, string message) =>
        _messages.Enqueue($"{categoryName}: {message}");

    private sealed class CapturingLogger(
        CapturingLoggerProvider provider,
        string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            provider.Add(categoryName, formatter(state, exception));
    }
}
