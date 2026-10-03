using FluentValidation;
using SmartSchoolTimetable.Application;

namespace SmartSchoolTimetable.Api;

public static class AuthEndpoints
{
    public const string SessionCookieName = SessionCookie.Name;

    public static IEndpointRouteBuilder MapLocalAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1");
        group.MapGet("/bootstrap", GetBootstrap);
        group.MapPost("/auth/setup", Setup);
        group.MapPost("/auth/login", Login);
        group.MapPost("/auth/recovery", Recover);
        group.MapPost("/auth/recovery-code/regenerate", RegenerateRecoveryCode);
        group.MapPost("/auth/recovery-code/acknowledge", AcknowledgeRecoveryCode);
        group.MapPost("/auth/logout", Logout);
        group.MapPost("/auth/change-password", ChangePassword);
        group.MapGet("/private/status", GetPrivateStatus);
        return endpoints;
    }

    private static async Task<IResult> GetBootstrap(
        HttpContext context,
        ILocalAuthService authService,
        LocalLaunchToken launchToken,
        LocalApplicationOptions options,
        CancellationToken cancellationToken)
    {
        var sessionId = SessionCookie.Read(context);
        var status = await authService.GetStatusAsync(sessionId, cancellationToken);
        if (status.Authenticated && sessionId is not null)
            SessionCookie.Append(context, sessionId, options.InactivityTimeout);
        return Results.Ok(new BootstrapResponse(
            status.SetupRequired,
            status.Authenticated,
            status.Username,
            status.RecoveryCodeAcknowledgementRequired,
            launchToken.Value,
            options.InactivityTimeoutMinutes));
    }

    private static async Task<IResult> Setup(
        SetupRequest request,
        HttpContext context,
        IValidator<SetupRequest> validator,
        ILocalAuthService authService,
        LocalApplicationOptions options,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, context, cancellationToken) is { } invalid)
            return invalid;

        var result = await authService.SetupAsync(request.Username!, request.Password!, cancellationToken);
        if (!result.Succeeded || result.SessionId is null || result.RecoveryCode is null)
            return Failure(context, result);

        SessionCookie.Append(context, result.SessionId, options.InactivityTimeout);
        return Results.Created("/api/v1/bootstrap", new SetupResponse(result.RecoveryCode));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        HttpContext context,
        IValidator<LoginRequest> validator,
        ILocalAuthService authService,
        LocalApplicationOptions options,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, context, cancellationToken) is { } invalid)
            return invalid;

        var result = await authService.LoginAsync(request.Username!, request.Password!, cancellationToken);
        if (!result.Succeeded || result.SessionId is null)
            return Failure(context, result);

        SessionCookie.Append(context, result.SessionId, options.InactivityTimeout);
        return Results.NoContent();
    }

    private static async Task<IResult> Recover(
        RecoveryRequest request,
        HttpContext context,
        IValidator<RecoveryRequest> validator,
        ILocalAuthService authService,
        LocalApplicationOptions options,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, context, cancellationToken) is { } invalid)
            return invalid;

        var result = await authService.RecoverAsync(request.RecoveryCode!, request.NewPassword!, cancellationToken);
        if (!result.Succeeded || result.SessionId is null || result.RecoveryCode is null)
            return Failure(context, result);

        SessionCookie.Append(context, result.SessionId, options.InactivityTimeout);
        return Results.Ok(new RecoveryResponse(result.RecoveryCode));
    }

    private static async Task<IResult> RegenerateRecoveryCode(
        RecoveryCodeRequest request,
        HttpContext context,
        IValidator<RecoveryCodeRequest> validator,
        ILocalAuthService authService,
        CancellationToken cancellationToken)
    {
        if (SessionCookie.Read(context) is not { } sessionId)
            return Failure(context, ErrorCodes.Unauthenticated);
        if (await ValidateAsync(request, validator, context, cancellationToken) is { } invalid)
            return invalid;

        var result = await authService.RegenerateRecoveryCodeAsync(
            sessionId,
            request.CurrentPassword!,
            cancellationToken);
        if (!result.Succeeded || result.RecoveryCode is null)
            return Failure(context, result);

        return Results.Ok(new RecoveryResponse(result.RecoveryCode));
    }

    private static async Task<IResult> AcknowledgeRecoveryCode(
        HttpContext context,
        ILocalAuthService authService,
        CancellationToken cancellationToken)
    {
        if (SessionCookie.Read(context) is not { } sessionId)
            return Failure(context, ErrorCodes.Unauthenticated);

        var result = await authService.AcknowledgeRecoveryCodeAsync(sessionId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : Failure(context, result);
    }

    private static IResult Logout(HttpContext context, ILocalAuthService authService)
    {
        authService.Logout(SessionCookie.Read(context));
        SessionCookie.Delete(context);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        HttpContext context,
        IValidator<ChangePasswordRequest> validator,
        ILocalAuthService authService,
        CancellationToken cancellationToken)
    {
        if (SessionCookie.Read(context) is not { } sessionId)
            return Failure(context, ErrorCodes.Unauthenticated);
        if (await ValidateAsync(request, validator, context, cancellationToken) is { } invalid)
            return invalid;

        var result = await authService.ChangePasswordAsync(
            sessionId,
            request.CurrentPassword!,
            request.NewPassword!,
            cancellationToken);
        if (!result.Succeeded)
            return Failure(context, result);

        SessionCookie.Delete(context);
        return Results.NoContent();
    }

    private static async Task<IResult> GetPrivateStatus(
        HttpContext context,
        ILocalAuthService authService,
        LocalApplicationOptions options,
        CancellationToken cancellationToken)
    {
        var sessionId = SessionCookie.Read(context);
        var status = await authService.GetStatusAsync(sessionId, cancellationToken);
        if (!status.Authenticated || sessionId is null)
            return Failure(context, ErrorCodes.Unauthenticated);

        SessionCookie.Append(context, sessionId, options.InactivityTimeout);
        return Results.Ok(new PrivateStatusResponse("authenticated"));
    }

    private static async Task<IResult?> ValidateAsync<T>(
        T request,
        IValidator<T> validator,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (result.IsValid)
            return null;

        context.Items[UnifiedApiErrorMiddleware.ValidationErrorsItem] = result.Errors
            .Select(error => new ValidationIssue(error.PropertyName, error.ErrorCode))
            .ToArray();
        return Failure(context, ErrorCodes.ValidationFailed);
    }

    private static IResult Failure(HttpContext context, AuthOperationResult result) =>
        Failure(context, result.ErrorCode ?? ErrorCodes.InternalError);

    private static IResult Failure(HttpContext context, string code)
    {
        context.Items[UnifiedApiErrorMiddleware.ErrorCodeItem] = code;
        return Results.StatusCode(ApiErrorCodes.StatusFor(code) ?? StatusCodes.Status500InternalServerError);
    }
}
