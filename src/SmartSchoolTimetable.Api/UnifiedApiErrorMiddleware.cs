using System.Text.Json;
using SmartSchoolTimetable.Application;

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
        ArgumentNullException.ThrowIfNull(context);
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
                context.Items[ErrorCodeItem] = ErrorCodes.InternalError;
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                LocalLog.UnhandledApiFailure(logger, context.TraceIdentifier);
            }

            if (context.Response.StatusCode >= 400 || context.Items.ContainsKey(ErrorCodeItem))
            {
                // Capture everything that Response.Clear() would reset before clearing the response.
                var (statusCode, code) = ResolveError(
                    context.Items[ErrorCodeItem] as string,
                    context.Response.StatusCode);
                var errors = context.Items[ValidationErrorsItem] as IReadOnlyList<ValidationIssue> ?? [];
                context.Response.Body = originalBody;
                context.Response.Clear();
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
                LocalSecurityHeaders.Apply(context.Response.Headers);
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

    /// <summary>
    /// Chooses the error status and code. A registered code uses its mapped status. An unregistered code keeps
    /// the original status when that is already an error, and otherwise becomes 500 INTERNAL_ERROR, so an error
    /// is never sent with a 2xx/3xx status.
    /// </summary>
    public static (int StatusCode, string Code) ResolveError(string? code, int originalStatusCode)
    {
        code ??= ApiErrorCodes.CodeForStatus(originalStatusCode);
        if (ApiErrorCodes.StatusFor(code) is { } mappedStatus)
            return (mappedStatus, code);
        return originalStatusCode >= 400
            ? (originalStatusCode, code)
            : (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError);
    }
}
