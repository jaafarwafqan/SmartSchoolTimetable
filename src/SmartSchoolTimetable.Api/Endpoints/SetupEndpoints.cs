using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Setup;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>Shift mode (with an impact preview), resumable setup progress and the wizard steps (spec 2.5 §3.2, §3.5, §5).</summary>
public static class SetupEndpoints
{
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var mode = endpoints.MapOwnerGroup("/shift-mode");
        mode.MapGet("/impact", async (string? mode, HttpContext context, ShiftModeService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.GetImpactAsync(mode, token)));
        mode.MapPut("/", async (SetShiftModeCommand command, HttpContext context, ShiftModeService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetAsync(command, token)));

        var sessions = endpoints.MapOwnerGroup("/session-plan");
        sessions.MapGet("/", async (HttpContext context, SessionPlanService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.GetAsync(token)));
        sessions.MapPut("/", async (SaveSessionPlanCommand command, HttpContext context, SessionPlanService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SaveAsync(command, token)));

        var progress = endpoints.MapOwnerGroup("/setup-progress");
        progress.MapGet("/", async (SetupProgressService service, CancellationToken token) => Results.Ok(await service.GetAsync(token)));
        progress.MapPut("/", async (SaveSetupProgressCommand command, HttpContext context, SetupProgressService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SaveAsync(command, token)));

        var wizard = endpoints.MapOwnerGroup("/setup-wizard");
        wizard.MapPut("/school", async (WizardSchoolCommand command, HttpContext context, SetupWizardService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SaveSchoolAsync(command, token)));
        wizard.MapPut("/year", async (WizardYearCommand command, HttpContext context, SetupWizardService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SaveYearAsync(command, token)));
        wizard.MapPut("/timing", async (WizardTimingCommand command, HttpContext context, SetupWizardService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SaveTimingAsync(command, token)));
        wizard.MapGet("/review", async (SetupWizardService service, CancellationToken token) => Results.Ok(await service.ReviewAsync(token)));
        return endpoints;
    }
}
