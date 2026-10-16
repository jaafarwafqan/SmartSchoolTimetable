using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>
/// M3 localhost-attack suite on top of <c>MaliciousCrossOriginAndDnsRebindingRequestsAreRejected</c>: EVERY state-changing route refuses a request without the
/// per-launch token or from a foreign origin, a foreign page can neither read nor preflight the API, odd Host spellings are refused, and the launcher's
/// control channel exists only with its secret token.
/// </summary>
public sealed class LocalAttackSuiteTests
{
    private static readonly string[] StateChangingMethods = ["POST", "PUT", "PATCH", "DELETE"];

    /// <summary>Every mapped route that changes state, with its path parameters filled in.</summary>
    private static List<(string Method, string Path)> StateChangingRoutes(TestHost host)
    {
        var source = host.Services.GetRequiredService<EndpointDataSource>();
        var routes = new List<(string, string)>();
        foreach (var endpoint in source.Endpoints.OfType<RouteEndpoint>())
        {
            var path = endpoint.RoutePattern.RawText ?? string.Empty;
            if (!path.StartsWith("/api/", StringComparison.Ordinal))
                continue;
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
            foreach (var method in methods.Where(candidate => StateChangingMethods.Contains(candidate, StringComparer.OrdinalIgnoreCase)))
                routes.Add((method, System.Text.RegularExpressions.Regex.Replace(path, @"\{[^}]+\}", "1")));
        }
        return routes;
    }

    private static HttpRequestMessage Request(string method, string path, string? origin, string? launchToken, string host = "127.0.0.1:5080")
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Host = host;
        if (origin is not null)
            request.Headers.TryAddWithoutValidation("Origin", origin);
        if (launchToken is not null)
            request.Headers.TryAddWithoutValidation("X-Local-Launch-Token", launchToken);
        return request;
    }

    [Fact]
    public async Task EveryStateChangingRouteRefusesARequestWithoutTheLaunchTokenOrFromAForeignOrigin()
    {
        await using var host = new TestHost();
        var token = (await host.GetBootstrapAsync()).LaunchToken;
        var routes = StateChangingRoutes(host);
        Assert.True(routes.Count > 60, $"expected the whole API surface, found {routes.Count} state-changing routes");

        foreach (var (method, path) in routes)
        {
            // No Origin at all, a foreign Origin, the right Origin with no token, the right Origin with a wrong token: all refused before any handler runs.
            foreach (var (origin, launchToken) in new (string?, string?)[] { (null, token), ("https://evil.example", token), ("http://127.0.0.1:5080", null), ("http://127.0.0.1:5080", "not-the-token") })
            {
                using var response = await host.Client.SendAsync(Request(method, path, origin, launchToken));
                Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {path} with origin={origin ?? "none"} token={(launchToken is null ? "none" : "wrong/right")} answered {(int)response.StatusCode}");
            }
        }
    }

    [Fact]
    public async Task AForeignPageCannotReadOrPreflightTheApiAndNoCorsHeaderIsEverSent()
    {
        await using var host = new TestHost();
        foreach (var origin in new[] { "https://evil.example", "http://localhost:5080", "http://127.0.0.1:5081", "null" })
        {
            // A cross-origin read of the bootstrap (the page would learn the launch token): refused.
            using var read = Request("GET", "/api/v1/bootstrap", origin, null);
            using var readResponse = await host.Client.SendAsync(read);
            Assert.Equal(HttpStatusCode.Forbidden, readResponse.StatusCode);
            Assert.False(readResponse.Headers.Contains("Access-Control-Allow-Origin"));

            // A preflight (OPTIONS): never allowed, never a wildcard.
            using var preflight = Request("OPTIONS", "/api/v1/teachers/", origin, null);
            preflight.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
            preflight.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "x-local-launch-token");
            using var preflightResponse = await host.Client.SendAsync(preflight);
            Assert.NotEqual(HttpStatusCode.OK, preflightResponse.StatusCode);
            Assert.False(preflightResponse.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.False(preflightResponse.Headers.Contains("Access-Control-Allow-Headers"));
        }
        // The legitimate origin gets no CORS header either (same-origin needs none).
        using var own = await host.Client.GetAsync("/api/v1/bootstrap");
        Assert.False(own.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("localhost:5080")]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.1:5081")]
    [InlineData("127.0.0.1.evil.example:5080")]
    [InlineData("evil.example")]
    [InlineData("[::1]:5080")]
    [InlineData("0.0.0.0:5080")]
    [InlineData("127.1:5080")]
    public async Task AnyHostSpellingOtherThanTheCanonicalLoopbackAddressIsRefused(string hostHeader)
    {
        await using var host = new TestHost();
        using var response = await host.Client.SendAsync(Request("GET", "/api/v1/bootstrap", null, null, hostHeader));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("launchToken", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResponsesCarryTheSecurityHeadersAndNoApiAnswerIsCached()
    {
        await using var host = new TestHost();
        using var response = await host.Client.GetAsync("/api/v1/bootstrap");
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("Content-Security-Policy") || response.Content.Headers.Contains("Content-Security-Policy"));
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("http:", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("https:", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-eval'", csp, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheControlChannelDoesNotExistUnlessTheLauncherStartedTheServerWithItsToken()
    {
        await using var host = new TestHost();
        var token = (await host.GetBootstrapAsync()).LaunchToken;
        foreach (var path in new[] { "/api/v1/control/backup-now", "/api/v1/control/stop" })
        {
            using var request = Request("POST", path, "http://127.0.0.1:5080", token);
            request.Headers.TryAddWithoutValidation("X-Control-Token", "anything");
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task TheControlChannelNeedsItsSecretOnTopOfTheOrdinaryChecksAndNeverStopsTheTestHostByAccident()
    {
        const string secret = "launcher-secret-token-for-this-test";
        await using var host = new TestHost(controlToken: secret);
        await SetupOwnerAsync(host);
        var token = (await host.GetBootstrapAsync()).LaunchToken;

        // Wrong or missing control token: 404, as if the route did not exist.
        foreach (var presented in new string?[] { null, "", "wrong", secret + "x", secret[..^1] })
        {
            using var request = Request("POST", "/api/v1/control/backup-now", "http://127.0.0.1:5080", token);
            if (presented is not null)
                request.Headers.TryAddWithoutValidation("X-Control-Token", presented);
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        // The ordinary checks still apply: the right control token without the launch token or from a foreign origin is refused first.
        using (var noLaunchToken = Request("POST", "/api/v1/control/backup-now", "http://127.0.0.1:5080", null))
        {
            noLaunchToken.Headers.TryAddWithoutValidation("X-Control-Token", secret);
            using var response = await host.Client.SendAsync(noLaunchToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using (var foreign = Request("POST", "/api/v1/control/backup-now", "https://evil.example", token))
        {
            foreign.Headers.TryAddWithoutValidation("X-Control-Token", secret);
            using var response = await host.Client.SendAsync(foreign);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // Everything right: the tray's «نسخة احتياطية الآن» writes a verified copy in the configured backup folder.
        var settings = await host.Client.GetAsync("/api/v1/backup/settings");
        Assert.True(settings.IsSuccessStatusCode);
        using var good = Request("POST", "/api/v1/control/backup-now", "http://127.0.0.1:5080", token);
        good.Headers.TryAddWithoutValidation("X-Control-Token", secret);
        using var done = await host.Client.SendAsync(good);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var body = await done.Content.ReadAsStringAsync();
        Assert.Contains("timetable-backup-", body, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
    }
}
