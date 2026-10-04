using SmartSchoolTimetable.Application.SchoolSetup;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>Shift mode (with an impact preview) and resumable setup progress (spec 2.5 §3.2, §3.5).</summary>
public static class SetupEndpoints
{
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var mode = endpoints.MapOwnerGroup("/shift-mode");
        mode.MapGet("/impact", async (string? mode, HttpContext context, ShiftModeService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.GetImpactAsync(mode, token)));
        mode.MapPut("/", async (SetShiftModeCommand command, HttpContext context, ShiftModeService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetAsync(command, token)));

        var progress = endpoints.MapOwnerGroup("/setup-progress");
        progress.MapGet("/", async (SetupProgressService service, CancellationToken token) => Results.Ok(await service.GetAsync(token)));
        progress.MapPut("/", async (SaveSetupProgressCommand command, HttpContext context, SetupProgressService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SaveAsync(command, token)));
        return endpoints;
    }
}
