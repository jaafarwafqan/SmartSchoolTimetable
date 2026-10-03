using FluentValidation;
using FluentValidation.Results;
using SmartSchoolTimetable.Application;

namespace SmartSchoolTimetable.Api;

public static class AuthEndpoints
{
    public const string SessionCookieName = "smartschool.local-session";

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
        var status = await authService.GetStatusAsync(
            context.Request.Cookies[SessionCookieName],
            cancellationToken);
        if (status.Authenticated)
            AppendSessionCookie(context, context.Request.Cookies[SessionCookieName]!, options);
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
        var validation = await ValidateAsync(request, validator, context, cancellationToken);
        if (validation is not null)
            return validation;

        var result = await authService.SetupAsync(request.Username!, request.Password!, cancellationToken);
        if (!result.Succeeded)
            return Failure(
                context,
                result.ErrorCode == "SETUP_ALREADY_COMPLETE" ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest,
                result.ErrorCode ?? "INVALID_SETUP");

        AppendSessionCookie(context, result.SessionId!, options);
        return Results.Created(
            "/api/v1/bootstrap",
            new SetupResponse(result.RecoveryCode!));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        HttpContext context,
        IValidator<LoginRequest> validator,
        ILocalAuthService authService,
        LocalApplicationOptions options,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(request, validator, context, cancellationToken);
        if (validation is not null)
            return validation;

        var result = await authService.LoginAsync(request.Username!, request.Password!, cancellationToken);
        if (!result.Succeeded)
            return Failure(context, StatusCodes.Status401Unauthorized, result.ErrorCode ?? "INVALID_CREDENTIALS");

        AppendSessionCookie(context, result.SessionId!, options);
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
        var validation = await ValidateAsync(request, validator, context, cancellationToken);
        if (validation is not null)
            return validation;

        var result = await authService.RecoverAsync(request.RecoveryCode!, request.NewPassword!, cancellationToken);
        if (!result.Succeeded)
            return Failure(
                context,
                result.ErrorCode == "INVALID_PASSWORD" ? StatusCodes.Status422UnprocessableEntity : StatusCodes.Status401Unauthorized,
                result.ErrorCode ?? "INVALID_RECOVERY_CODE");

        AppendSessionCookie(context, result.SessionId!, options);
        return Results.Ok(new RecoveryResponse(result.RecoveryCode!));
    }

    private static async Task<IResult> RegenerateRecoveryCode(
        RecoveryCodeRequest request,
        HttpContext context,
        IValidator<RecoveryCodeRequest> validator,
        ILocalAuthService authService,
        CancellationToken cancellationToken)
    {
        var sessionId = context.Request.Cookies[SessionCookieName];
        if (sessionId is null)
            return Failure(context, StatusCodes.Status401Unauthorized, "UNAUTHENTICATED");

        var validation = await ValidateAsync(request, validator, context, cancellationToken);
        if (validation is not null)
            return validation;

        var result = await authService.RegenerateRecoveryCodeAsync(
            sessionId,
            request.CurrentPassword!,
            cancellationToken);
        if (!result.Succeeded)
            return Failure(
                context,
                result.ErrorCode == "CURRENT_PASSWORD_INCORRECT" ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden,
                result.ErrorCode ?? "RECOVERY_CODE_REGENERATION_FAILED");

        context.Items["RecoveryCodeRegeneration"] = true;
        return Results.Ok(new RecoveryResponse(result.RecoveryCode!));
    }

    private static async Task<IResult> AcknowledgeRecoveryCode(
        HttpContext context,
        ILocalAuthService authService,
        CancellationToken cancellationToken)
    {
        var sessionId = context.Request.Cookies[SessionCookieName];
        if (sessionId is null)
            return Failure(context, StatusCodes.Status401Unauthorized, "UNAUTHENTICATED");

        var result = await authService.AcknowledgeRecoveryCodeAsync(sessionId, cancellationToken);
        if (!result.Succeeded)
            return Failure(
                context,
                result.ErrorCode == "RECOVERY_MISSING" ? StatusCodes.Status409Conflict : StatusCodes.Status401Unauthorized,
                result.ErrorCode ?? "UNAUTHENTICATED");
        context.Items["RecoveryCodeAcknowledged"] = true;
        return Results.NoContent();
    }

    private static IResult Logout(HttpContext context, ILocalAuthService authService)
    {
        authService.Logout(context.Request.Cookies[SessionCookieName]);
        DeleteSessionCookie(context);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        HttpContext context,
        IValidator<ChangePasswordRequest> validator,
        ILocalAuthService authService,
        CancellationToken cancellationToken)
    {
        var sessionId = context.Request.Cookies[SessionCookieName];
        if (sessionId is null)
            return Failure(context, StatusCodes.Status401Unauthorized, "UNAUTHENTICATED");

        var validation = await ValidateAsync(request, validator, context, cancellationToken);
        if (validation is not null)
            return validation;

        var result = await authService.ChangePasswordAsync(
            sessionId,
            request.CurrentPassword!,
            request.NewPassword!,
            cancellationToken);
        if (!result.Succeeded)
            return Failure(
                context,
                result.ErrorCode == "CURRENT_PASSWORD_INCORRECT" ? StatusCodes.Status401Unauthorized : StatusCodes.Status422UnprocessableEntity,
                result.ErrorCode ?? "PASSWORD_CHANGE_FAILED");

        DeleteSessionCookie(context);
        return Results.NoContent();
    }

    private static async Task<IResult> GetPrivateStatus(
        HttpContext context,
        ILocalAuthService authService,
        LocalApplicationOptions options,
        CancellationToken cancellationToken)
    {
        var status = await authService.GetStatusAsync(
            context.Request.Cookies[SessionCookieName],
            cancellationToken);
        if (!status.Authenticated)
            return Failure(context, StatusCodes.Status401Unauthorized, "UNAUTHENTICATED");

        AppendSessionCookie(context, context.Request.Cookies[SessionCookieName]!, options);
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
        return Failure(context, StatusCodes.Status422UnprocessableEntity, "VALIDATION_FAILED");
    }

    private static IResult Failure(HttpContext context, int statusCode, string code)
    {
        context.Items[UnifiedApiErrorMiddleware.ErrorCodeItem] = code;
        return Results.StatusCode(statusCode);
    }

    private static void AppendSessionCookie(
        HttpContext context,
        string sessionId,
        LocalApplicationOptions options)
    {
        var cookie = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            IsEssential = true,
            Path = "/"
        };
        if (options.InactivityTimeout is { } timeout)
            cookie.MaxAge = timeout;
        context.Response.Cookies.Append(SessionCookieName, sessionId, cookie);
    }

    private static void DeleteSessionCookie(HttpContext context) =>
        context.Response.Cookies.Delete(SessionCookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/"
        });
}

public sealed record BootstrapResponse(
    bool SetupRequired,
    bool Authenticated,
    string? Username,
    bool RecoveryCodeAcknowledgementRequired,
    string LaunchToken,
    int? InactivityTimeoutMinutes);

public sealed record SetupRequest(string? Username, string? Password, string? ConfirmPassword);
public sealed record SetupResponse(string RecoveryCode);
public sealed record LoginRequest(string? Username, string? Password);
public sealed record RecoveryRequest(string? RecoveryCode, string? NewPassword);
public sealed record RecoveryCodeRequest(string? CurrentPassword);
public sealed record RecoveryResponse(string RecoveryCode);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public sealed record PrivateStatusResponse(string Status);
public sealed record ValidationIssue(string Field, string Code);
public sealed record ApiErrorResponse(string Code, string CorrelationId, IReadOnlyList<ValidationIssue> Errors);

public sealed class SetupRequestValidator : AbstractValidator<SetupRequest>
{
    public SetupRequestValidator()
    {
        RuleFor(request => request.Username)
            .NotEmpty().WithErrorCode("REQUIRED").WithMessage("REQUIRED")
            .MinimumLength(3).WithErrorCode("USERNAME_TOO_SHORT").WithMessage("USERNAME_TOO_SHORT")
            .MaximumLength(64).WithErrorCode("USERNAME_TOO_LONG").WithMessage("USERNAME_TOO_LONG");
        RuleFor(request => request.Password)
            .NotEmpty().WithErrorCode("REQUIRED").WithMessage("REQUIRED")
            .MinimumLength(12).WithErrorCode("PASSWORD_TOO_SHORT").WithMessage("PASSWORD_TOO_SHORT")
            .MaximumLength(1024).WithErrorCode("PASSWORD_TOO_LONG").WithMessage("PASSWORD_TOO_LONG");
        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.Password).WithErrorCode("PASSWORD_MISMATCH").WithMessage("PASSWORD_MISMATCH");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Username).NotEmpty().WithErrorCode("REQUIRED").WithMessage("REQUIRED");
        RuleFor(request => request.Password).NotEmpty().WithErrorCode("REQUIRED").WithMessage("REQUIRED");
    }
}

public sealed class RecoveryRequestValidator : AbstractValidator<RecoveryRequest>
{
    public RecoveryRequestValidator()
    {
        RuleFor(request => request.RecoveryCode).NotEmpty().WithErrorCode("REQUIRED").WithMessage("REQUIRED");
        RuleFor(request => request.NewPassword)
            .NotEmpty().WithErrorCode("REQUIRED").WithMessage("REQUIRED")
            .MinimumLength(12).WithErrorCode("PASSWORD_TOO_SHORT").WithMessage("PASSWORD_TOO_SHORT")
            .MaximumLength(1024).WithErrorCode("PASSWORD_TOO_LONG").WithMessage("PASSWORD_TOO_LONG");
    }
}

public sealed class RecoveryCodeRequestValidator : AbstractValidator<RecoveryCodeRequest>
{
    public RecoveryCodeRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().WithErrorCode("REQUIRED").WithMessage("REQUIRED");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().WithErrorCode("REQUIRED").WithMessage("REQUIRED");
        RuleFor(request => request.NewPassword)
            .NotEmpty().WithErrorCode("REQUIRED").WithMessage("REQUIRED")
            .MinimumLength(12).WithErrorCode("PASSWORD_TOO_SHORT").WithMessage("PASSWORD_TOO_SHORT")
            .MaximumLength(1024).WithErrorCode("PASSWORD_TOO_LONG").WithMessage("PASSWORD_TOO_LONG");
    }
}
