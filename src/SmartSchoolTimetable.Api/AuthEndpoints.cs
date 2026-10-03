using Microsoft.AspNetCore.Http.HttpResults;
using SmartSchoolTimetable.Application;

namespace SmartSchoolTimetable.Api;

public static class AuthEndpoints
{
    public const string SessionCookieName = "smartschool.local-session";

    public static IEndpointRouteBuilder MapLocalAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1");

        group.MapGet("/bootstrap", GetBootstrap)
            .WithName("GetLocalBootstrap")
            .WithSummary("Return local setup state and per-launch request token")
            .Produces<BootstrapResponse>();

        group.MapPost("/auth/setup", Setup)
            .WithName("CreateLocalOwner")
            .WithSummary("Create the initial local owner account and issue its one-time recovery code")
            .Produces<SetupResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/auth/login", Login)
            .WithName("LoginLocalOwner")
            .WithSummary("Sign in to the local application")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked);

        group.MapPost("/auth/recovery", Recover)
            .WithName("RecoverLocalOwnerPassword")
            .WithSummary("Reset the local owner password with the one-time recovery code")
            .Produces<RecoveryResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/auth/logout", Logout)
            .WithName("LogoutLocalOwner")
            .WithSummary("End the local owner session")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/auth/change-password", ChangePassword)
            .WithName("ChangeLocalOwnerPassword")
            .WithSummary("Change the local owner password after verifying the current password")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/private/status", GetPrivateStatus)
            .WithName("GetAuthenticatedLocalStatus")
            .WithSummary("Verify that the local owner session is active")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<Ok<BootstrapResponse>> GetBootstrap(
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
            AppendSessionCookie(context, status.Username!, options);
        return TypedResults.Ok(new BootstrapResponse(
            status.SetupRequired,
            status.Authenticated,
            status.Username,
            launchToken.Value,
            (int)options.InactivityTimeout.TotalMinutes));
    }

    private static async Task<IResult> Setup(
        SetupRequest request,
        HttpContext context,
        ILocalAuthService authService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Password is null)
            return Problem(context, StatusCodes.Status400BadRequest, "Invalid setup", "Username and password are required.");

        var result = await authService.SetupAsync(request.Username, request.Password, cancellationToken);
        if (!result.Succeeded)
            return result.ErrorCode == "setup_already_complete"
                ? Problem(context, StatusCodes.Status409Conflict, "Setup already complete", "The owner account already exists.")
                : Problem(context, StatusCodes.Status400BadRequest, "Invalid setup", PasswordValidationDetail(result.ErrorCode));

        return TypedResults.Created(
            "/api/v1/bootstrap",
            new SetupResponse(result.RecoveryCode!));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        HttpContext context,
        ILocalAuthService authService,
        LocalApplicationOptions options,
        CancellationToken cancellationToken)
    {
        if (request.Username is null || request.Password is null)
            return Problem(context, StatusCodes.Status400BadRequest, "Invalid login", "Username and password are required.");

        var result = await authService.LoginAsync(request.Username, request.Password, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorCode == "temporarily_locked")
            {
                var retry = Math.Max(1, (int)Math.Ceiling(result.RetryAfter?.TotalSeconds ?? 1));
                context.Response.Headers.RetryAfter = retry.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Problem(context, StatusCodes.Status423Locked, "Temporarily locked", "Sign-in is temporarily delayed. Try again later.");
            }

            return Problem(
                context,
                StatusCodes.Status401Unauthorized,
                "Sign-in failed",
                "The username or password is incorrect.");
        }

        AppendSessionCookie(context, result.SessionId!, options);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Recover(
        RecoveryRequest request,
        HttpContext context,
        ILocalAuthService authService,
        CancellationToken cancellationToken)
    {
        if (request.RecoveryCode is null || request.NewPassword is null)
            return Problem(context, StatusCodes.Status400BadRequest, "Invalid recovery request", "Recovery code and new password are required.");

        var result = await authService.RecoverAsync(request.RecoveryCode, request.NewPassword, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorCode == "invalid_password")
                return Problem(context, StatusCodes.Status400BadRequest, "Invalid password", "Use a password from 12 to 1024 characters.");
            return Problem(context, StatusCodes.Status401Unauthorized, "Recovery failed", "The recovery code is invalid or recovery is unavailable.");
        }

        context.Response.Cookies.Delete(SessionCookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/"
        });
        return TypedResults.Ok(new RecoveryResponse(result.RecoveryCode!));
    }

    private static IResult Logout(HttpContext context, ILocalAuthService authService)
    {
        authService.Logout(context.Request.Cookies[SessionCookieName]);
        context.Response.Cookies.Delete(SessionCookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/"
        });
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        HttpContext context,
        ILocalAuthService authService,
        CancellationToken cancellationToken)
    {
        var sessionId = context.Request.Cookies[SessionCookieName];
        if (sessionId is null)
            return Problem(context, StatusCodes.Status401Unauthorized, "Sign-in required", "Sign in again to continue.");
        if (request.CurrentPassword is null || request.NewPassword is null)
            return Problem(context, StatusCodes.Status400BadRequest, "Invalid password change", "Current and new passwords are required.");

        var result = await authService.ChangePasswordAsync(
            sessionId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorCode == "current_password_incorrect")
                return Problem(context, StatusCodes.Status401Unauthorized, "Current password incorrect", "The current password is incorrect.");
            if (result.ErrorCode == "invalid_password")
                return Problem(context, StatusCodes.Status400BadRequest, "Invalid password", "Use a password from 12 to 1024 characters.");
            return Problem(context, StatusCodes.Status401Unauthorized, "Sign-in required", "Sign in again to continue.");
        }

        context.Response.Cookies.Delete(SessionCookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/"
        });
        return TypedResults.NoContent();
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
            return Problem(context, StatusCodes.Status401Unauthorized, "Sign-in required", "Sign in to access this resource.");

        AppendSessionCookie(context, status.Username!, options);
        return TypedResults.Ok(new PrivateStatusResponse("authenticated"));
    }

    private static void AppendSessionCookie(HttpContext context, string sessionId, LocalApplicationOptions options) =>
        context.Response.Cookies.Append(
            SessionCookieName,
            sessionId,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = context.Request.IsHttps,
                IsEssential = true,
                Path = "/",
                MaxAge = options.InactivityTimeout
            });

    private static IResult Problem(HttpContext context, int status, string title, string detail) =>
        TypedResults.Problem(
            statusCode: status,
            title: title,
            detail: detail,
            instance: context.Request.Path);

    private static string PasswordValidationDetail(string? errorCode) => errorCode switch
    {
        "invalid_username" => "Username must be 3 to 64 characters and contain no control characters.",
        "invalid_password" => "Use a password from 12 to 1024 characters.",
        _ => "The setup request is invalid."
    };
}

/// <summary>Initial state and per-launch request token for the local browser application.</summary>
public sealed record BootstrapResponse(
    bool SetupRequired,
    bool Authenticated,
    string? Username,
    string LaunchToken,
    int InactivityTimeoutMinutes);

/// <summary>Payload for creating the first local owner account.</summary>
public sealed record SetupRequest(string? Username, string? Password);

/// <summary>One-time recovery code returned after initial setup.</summary>
public sealed record SetupResponse(string RecoveryCode);

/// <summary>Payload for local owner sign-in.</summary>
public sealed record LoginRequest(string? Username, string? Password);

/// <summary>Payload for resetting the owner password with the recovery code.</summary>
public sealed record RecoveryRequest(string? RecoveryCode, string? NewPassword);

/// <summary>Replacement one-time recovery code issued after successful recovery.</summary>
public sealed record RecoveryResponse(string RecoveryCode);

/// <summary>Payload for changing the current owner's password.</summary>
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>Minimal confirmation that the local session is authenticated.</summary>
public sealed record PrivateStatusResponse(string Status);
