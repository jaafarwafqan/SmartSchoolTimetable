namespace SmartSchoolTimetable.Api;

public static class LocalSecurityHeaders
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    public static void Apply(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
    }
}

public static class SessionCookie
{
    public const string Name = "smartschool.local-session";

    public static string? Read(HttpContext context) => context.Request.Cookies[Name];

    public static void Append(HttpContext context, string sessionId, TimeSpan? inactivityTimeout)
    {
        var options = CreateOptions(context);
        if (inactivityTimeout is { } timeout)
            options.MaxAge = timeout;
        context.Response.Cookies.Append(Name, sessionId, options);
    }

    public static void Delete(HttpContext context) =>
        context.Response.Cookies.Delete(Name, CreateOptions(context));

    private static CookieOptions CreateOptions(HttpContext context) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = context.Request.IsHttps,
        IsEssential = true,
        Path = "/"
    };
}
