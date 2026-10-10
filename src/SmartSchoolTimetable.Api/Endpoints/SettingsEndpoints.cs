using SmartSchoolTimetable.Application.Settings;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>The owner's stored preferences (M2): owner only, launch-token protected like every state change.</summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var preferences = endpoints.MapOwnerGroup("/preferences");
        preferences.MapGet("/", async (PreferencesService service, CancellationToken token) => Results.Ok(await service.GetAsync(token)));
        preferences.MapPut("/", async (SavePreferencesCommand command, HttpContext context, PreferencesService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateAsync(command, token)));
        return endpoints;
    }
}
