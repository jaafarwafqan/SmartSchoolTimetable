using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>Rejects requests without a valid owner session (401) and slides the session cookie lifetime.</summary>
public sealed class RequireOwnerSessionFilter(ILocalAuthService authService) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var httpContext = context.HttpContext;
        var sessionId = SessionCookie.Read(httpContext);
        var status = await authService.GetStatusAsync(sessionId, httpContext.RequestAborted);
        if (!status.Authenticated || sessionId is null)
            return ApiResults.Failure(httpContext, ErrorCodes.Unauthenticated);

        SessionCookie.Append(httpContext, sessionId, status.InactivityTimeout);
        return await next(context);
    }
}

/// <summary>Maps Application results onto the unified error envelope (codes only, never prose).</summary>
public static class ApiResults
{
    public static IResult Failure(HttpContext context, string code)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[UnifiedApiErrorMiddleware.ErrorCodeItem] = code;
        return Results.StatusCode(ApiErrorCodes.StatusFor(code) ?? StatusCodes.Status500InternalServerError);
    }

    public static IResult From<T>(HttpContext context, OperationResult<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        if (result.Succeeded)
            return onSuccess(result.Value!);
        if (result.FieldErrors.Count > 0)
        {
            context.Items[UnifiedApiErrorMiddleware.ValidationErrorsItem] = result.FieldErrors
                .Select(error => new ValidationIssue(error.Field, error.Code))
                .ToArray();
        }
        return Failure(context, result.ErrorCode ?? ErrorCodes.InternalError);
    }

    public static IResult Ok<T>(HttpContext context, OperationResult<T> result) =>
        From(context, result, value => Results.Ok(value));

    public static IResult NoContent<T>(HttpContext context, OperationResult<T> result) =>
        From(context, result, _ => Results.NoContent());

    /// <summary>A /api/v1 group whose every route requires the owner session.</summary>
    public static RouteGroupBuilder MapOwnerGroup(this IEndpointRouteBuilder endpoints, string prefix) =>
        endpoints.MapGroup($"/api/v1{prefix}").AddEndpointFilter<RequireOwnerSessionFilter>();
}
