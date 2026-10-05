using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Resources;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Application.Stages;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>Resources and the scheduling profile (Phase 3B).</summary>
public static class Phase3Endpoints
{
    public static IEndpointRouteBuilder MapResourcesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var resources = endpoints.MapOwnerGroup("/resources");
        resources.MapGet("/", async ([AsParameters] ListQuery query, ResourcesService service, CancellationToken token) =>
            Results.Ok(await service.ListAsync(query, token)));
        resources.MapPost("/", async (SaveResourceCommand command, HttpContext context, ResourcesService service, CancellationToken token) =>
            ApiResults.From(context, await service.CreateAsync(command, token), value => Results.Created($"/api/v1/resources/{value.Id}", value)));
        resources.MapPut("/{id:long}", async (long id, SaveResourceCommand command, HttpContext context, ResourcesService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateAsync(id, command, token)));
        resources.MapDelete("/{id:long}", async (long id, int version, HttpContext context, ResourcesService service, CancellationToken token) =>
            ApiResults.NoContent(context, await service.DeleteAsync(id, version, token)));
        resources.MapPost("/{id:long}/archive", async (long id, ArchiveCommand command, HttpContext context, ResourcesService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetArchivedAsync(id, command.Version, true, token)));
        resources.MapPost("/{id:long}/restore", async (long id, ArchiveCommand command, HttpContext context, ResourcesService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetArchivedAsync(id, command.Version, false, token)));
        return endpoints;
    }

    public static IEndpointRouteBuilder MapSchedulingProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var profile = endpoints.MapOwnerGroup("/scheduling-profile");
        profile.MapGet("/", async (SchedulingProfileService service, CancellationToken token) => Results.Ok(await service.GetAsync(token)));
        profile.MapPut("/", async (SaveSchedulingProfileCommand command, HttpContext context, SchedulingProfileService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateAsync(command, token)));
        profile.MapPost("/restore-defaults", async (RestoreSchedulingDefaultsCommand command, HttpContext context, SchedulingProfileService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.RestoreDefaultsAsync(command, token)));
        return endpoints;
    }
}
