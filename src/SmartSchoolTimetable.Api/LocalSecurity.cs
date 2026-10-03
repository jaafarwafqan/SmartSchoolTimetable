using System.Net;
using System.Security.Cryptography;
using System.Text;
using SmartSchoolTimetable.Application;

namespace SmartSchoolTimetable.Api;

public sealed class LocalLaunchToken
{
    private readonly string _encodedToken = SecureToken.CreateUrlSafe();

    public string Value => _encodedToken;

    public bool Matches(string? candidate)
    {
        if (candidate is null)
            return false;
        var expected = Encoding.ASCII.GetBytes(_encodedToken);
        var actual = Encoding.ASCII.GetBytes(candidate);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}

public sealed record LocalApplicationOptions(int Port, TimeSpan? InactivityTimeout)
{
    public string Origin => $"http://127.0.0.1:{Port}";
    public string Host => $"127.0.0.1:{Port}";
    public int? InactivityTimeoutMinutes => InactivityTimeout is { } timeout
        ? (int)timeout.TotalMinutes
        : null;

    public static LocalApplicationOptions FromConfiguration(IConfiguration configuration)
    {
        var port = configuration.GetValue("LocalHost:Port", 5080);
        if (port is < 1024 or > 65535)
            throw new InvalidOperationException("LocalHost:Port must be between 1024 and 65535.");

        var inactivityValue = configuration["Authentication:InactivityTimeoutMinutes"] ?? "30";
        TimeSpan? inactivityTimeout;
        if (string.Equals(inactivityValue, "Never", StringComparison.OrdinalIgnoreCase))
        {
            inactivityTimeout = null;
        }
        else if (int.TryParse(inactivityValue, out var inactivityMinutes) && inactivityMinutes is >= 1 and <= 1440)
        {
            inactivityTimeout = TimeSpan.FromMinutes(inactivityMinutes);
        }
        else
        {
            throw new InvalidOperationException(
                "Authentication:InactivityTimeoutMinutes must be between 1 and 1440, or Never.");
        }

        return new LocalApplicationOptions(port, inactivityTimeout);
    }
}

public static class LocalListenerGuard
{
    public static void ValidateConfiguredEndpoint(IPAddress address, int port)
    {
        if (!IPAddress.Loopback.Equals(address))
            throw new InvalidOperationException($"Kestrel must bind to 127.0.0.1 only; configured address was {address}.");
        if (port is < 1024 or > 65535)
            throw new InvalidOperationException("The local listener port is outside the supported range.");
    }

    public static void ValidateBoundAddresses(IEnumerable<string> boundAddresses, int expectedPort)
    {
        var addresses = boundAddresses.Select(value =>
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                throw new InvalidOperationException($"Kestrel reported an invalid bound address: {value}.");
            return uri;
        }).ToArray();

        if (addresses.Length != 1 ||
            !string.Equals(addresses[0].Host, IPAddress.Loopback.ToString(), StringComparison.OrdinalIgnoreCase) ||
            addresses[0].Port != expectedPort)
        {
            var actual = string.Join(", ", boundAddresses);
            throw new InvalidOperationException(
                $"Kestrel must be bound only to http://127.0.0.1:{expectedPort}; actual bindings: {actual}.");
        }
    }
}

public sealed class LocalRequestSecurityMiddleware(
    RequestDelegate next,
    LocalApplicationOptions options,
    LocalLaunchToken launchToken,
    ILogger<LocalRequestSecurityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        LocalSecurityHeaders.Apply(context.Response.Headers);

        if (context.Request.Host.Value != options.Host)
        {
            LocalLog.RejectedHost(logger);
            context.Items[UnifiedApiErrorMiddleware.ErrorCodeItem] = ErrorCodes.InvalidHost;
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var hasOrigin = context.Request.Headers.TryGetValue("Origin", out var origins);
        if ((IsStateChangingMethod(context.Request.Method) && !hasOrigin) ||
            (hasOrigin &&
             (origins.Count != 1 || !string.Equals(origins[0], options.Origin, StringComparison.Ordinal))))
        {
            LocalLog.RejectedOrigin(logger);
            context.Items[UnifiedApiErrorMiddleware.ErrorCodeItem] = ErrorCodes.InvalidOrigin;
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (IsStateChangingMethod(context.Request.Method) &&
            !launchToken.Matches(context.Request.Headers["X-Local-Launch-Token"].FirstOrDefault()))
        {
            context.Items[UnifiedApiErrorMiddleware.ErrorCodeItem] = ErrorCodes.InvalidLaunchToken;
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            context.Response.Headers.CacheControl = "no-store";

        await next(context);
    }

    private static bool IsStateChangingMethod(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) ||
        HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

}
