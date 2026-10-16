using System.Security.Cryptography;
using System.Text;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Backup;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>The tray's «نسخة احتياطية الآن» result: where the copy went, or why nothing was made.</summary>
public sealed record ControlBackupDto(string FileName, long SizeBytes);

/// <summary>
/// The launcher's control channel (M5): «نسخة احتياطية الآن» and «إيقاف» from the tray. The routes exist ONLY when the application was started with a
/// control token (the launcher passes a fresh random one in the environment of the server process it starts); a browser never learns it, so a web page
/// cannot use these routes. Every request must still pass the Host, Origin and launch-token checks of the whole application, and then present
/// <c>X-Control-Token</c>; a wrong or missing token answers 404 as if the route did not exist. The token is never logged.
/// </summary>
public static class ControlEndpoints
{
    public const string TokenHeader = "X-Control-Token";

    public static IEndpointRouteBuilder MapControlEndpoints(this IEndpointRouteBuilder endpoints, string? controlToken)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (string.IsNullOrEmpty(controlToken))
            return endpoints; // started by hand (a developer, a test): no control channel at all

        var expected = Encoding.UTF8.GetBytes(controlToken);
        var control = endpoints.MapGroup("/api/v1/control");
        control.AddEndpointFilter(async (context, next) =>
        {
            var presented = Encoding.UTF8.GetBytes(context.HttpContext.Request.Headers[TokenHeader].FirstOrDefault() ?? string.Empty);
            return presented.Length == expected.Length && CryptographicOperations.FixedTimeEquals(presented, expected)
                ? await next(context)
                : Results.NotFound();
        });

        control.MapPost("/backup-now", async (BackupService service, CancellationToken token) =>
        {
            var settings = await service.GetSettingsAsync(token);
            var result = await service.CreateAsync(new CreateBackupCommand(settings.EffectiveFolder), token);
            return result.Succeeded
                ? Results.Ok(new ControlBackupDto(Path.GetFileName(result.Value!.FilePath), result.Value.SizeBytes))
                : Results.Json(new { code = result.ErrorCode ?? ErrorCodes.BackupFailed }, statusCode: StatusCodes.Status409Conflict);
        });
        control.MapPost("/stop", (IHostApplicationLifetime lifetime) =>
        {
            lifetime.StopApplication();
            return Results.NoContent();
        });
        return endpoints;
    }
}
