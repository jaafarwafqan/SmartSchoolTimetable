using System.Text.Json;

namespace SmartSchoolTimetable.Api;

public sealed class UnifiedApiErrorMiddleware(
    RequestDelegate next,
    ILogger<UnifiedApiErrorMiddleware> logger)
{
    public const string ErrorCodeItem = "ApiErrorCode";
    public const string ValidationErrorsItem = "ApiValidationErrors";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var bufferedBody = new MemoryStream();
        context.Response.Body = bufferedBody;
        try
        {
            try
            {
                await next(context);
            }
            catch (Exception) when (!context.RequestAborted.IsCancellationRequested)
            {
                context.Items[ErrorCodeItem] = "INTERNAL_ERROR";
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                logger.LogError("Unhandled API failure. CorrelationId: {CorrelationId}", context.TraceIdentifier);
            }

            if (context.Response.StatusCode >= 400)
            {
                var code = context.Items[ErrorCodeItem] as string ?? CodeForStatus(context.Response.StatusCode);
                var errors = context.Items[ValidationErrorsItem] as IReadOnlyList<ValidationIssue> ?? [];
                context.Response.Body = originalBody;
                context.Response.Clear();
                context.Response.StatusCode = StatusCodeFor(code, context.Response.StatusCode);
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
                context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
                await JsonSerializer.SerializeAsync(
                    originalBody,
                    new ApiErrorResponse(code, context.TraceIdentifier, errors),
                    JsonOptions,
                    context.RequestAborted);
            }
            else
            {
                bufferedBody.Position = 0;
                await bufferedBody.CopyToAsync(originalBody, context.RequestAborted);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static int StatusCodeFor(string code, int fallback) => code switch
    {
        "INVALID_CREDENTIALS" or "INVALID_RECOVERY_CODE" or "CURRENT_PASSWORD_INCORRECT" or "UNAUTHENTICATED" =>
            StatusCodes.Status401Unauthorized,
        "INVALID_ORIGIN" or "INVALID_LAUNCH_TOKEN" or "REQUEST_FORBIDDEN" or "SETUP_REQUIRED" =>
            StatusCodes.Status403Forbidden,
        "RECOVERY_MISSING" or "SETUP_ALREADY_COMPLETE" or "CONFLICT" =>
            StatusCodes.Status409Conflict,
        "INVALID_HOST" or "INVALID_REQUEST" => StatusCodes.Status400BadRequest,
        "NOT_FOUND" => StatusCodes.Status404NotFound,
        "METHOD_NOT_ALLOWED" => StatusCodes.Status405MethodNotAllowed,
        "UNSUPPORTED_MEDIA_TYPE" => StatusCodes.Status415UnsupportedMediaType,
        "VALIDATION_FAILED" or "INVALID_PASSWORD" or "INVALID_USERNAME" or "PASSWORD_MISMATCH" =>
            StatusCodes.Status422UnprocessableEntity,
        "TOO_MANY_REQUESTS" => StatusCodes.Status429TooManyRequests,
        "INTERNAL_ERROR" => StatusCodes.Status500InternalServerError,
        _ => fallback
    };

    private static string CodeForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "INVALID_REQUEST",
        StatusCodes.Status401Unauthorized => "UNAUTHENTICATED",
        StatusCodes.Status403Forbidden => "REQUEST_FORBIDDEN",
        StatusCodes.Status404NotFound => "NOT_FOUND",
        StatusCodes.Status405MethodNotAllowed => "METHOD_NOT_ALLOWED",
        StatusCodes.Status409Conflict => "CONFLICT",
        StatusCodes.Status415UnsupportedMediaType => "UNSUPPORTED_MEDIA_TYPE",
        StatusCodes.Status422UnprocessableEntity => "VALIDATION_FAILED",
        StatusCodes.Status429TooManyRequests => "TOO_MANY_REQUESTS",
        _ => "INTERNAL_ERROR"
    };
}

public static class ApiErrorCodes
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "CONFLICT",
        "CURRENT_PASSWORD_INCORRECT",
        "INTERNAL_ERROR",
        "INVALID_CREDENTIALS",
        "INVALID_HOST",
        "INVALID_LAUNCH_TOKEN",
        "INVALID_ORIGIN",
        "INVALID_PASSWORD",
        "INVALID_RECOVERY_CODE",
        "INVALID_REQUEST",
        "INVALID_SETUP",
        "INVALID_USERNAME",
        "METHOD_NOT_ALLOWED",
        "NOT_FOUND",
        "PASSWORD_MISMATCH",
        "RECOVERY_CODE_REGENERATION_FAILED",
        "REQUEST_FORBIDDEN",
        "SETUP_ALREADY_COMPLETE",
        "SETUP_REQUIRED",
        "TOO_MANY_REQUESTS",
        "UNAUTHENTICATED",
        "UNSUPPORTED_MEDIA_TYPE",
        "VALIDATION_FAILED",
        "RECOVERY_MISSING",
        "REQUIRED",
        "USERNAME_TOO_SHORT",
        "USERNAME_TOO_LONG",
        "PASSWORD_TOO_SHORT",
        "PASSWORD_TOO_LONG"
    };
}
