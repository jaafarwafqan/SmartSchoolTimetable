using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Resources;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Workload;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>Resources and the scheduling profile (Phase 3B); workload assignments (Phase 3C).</summary>
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

    public static IEndpointRouteBuilder MapWorkloadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var workload = endpoints.MapOwnerGroup("/academic-years/{yearId:long}/workload");
        workload.MapGet("/matrix", async (long yearId, long? stageId, HttpContext context, WorkloadService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.GetMatrixAsync(yearId, stageId, token)));
        workload.MapGet("/teachers", async (long yearId, HttpContext context, WorkloadService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.GetTeacherLoadsAsync(yearId, token)));
        workload.MapPut("/cell", async (long yearId, SetWorkloadCellCommand command, HttpContext context, WorkloadService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetCellAsync(yearId, command, token)));
        workload.MapGet("/suggestions/preview", async (long yearId, HttpContext context, WorkloadService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SuggestAssignmentsAsync(yearId, apply: false, confirm: false, token)));
        workload.MapPost("/suggestions/apply", async (long yearId, ConfirmSuggestionsCommand command, HttpContext context, WorkloadService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SuggestAssignmentsAsync(yearId, apply: true, command.Confirm, token)));
        foreach (var apply in new[] { false, true })
        {
            var suffix = apply ? string.Empty : "/preview";
            workload.MapPost($"/bulk/across-stage{suffix}", async (long yearId, AssignAcrossStageCommand command, HttpContext context, WorkloadService service, CancellationToken token) =>
                ApiResults.Ok(context, await service.AssignAcrossStageAsync(yearId, command, apply, token)));
            workload.MapPost($"/bulk/class-teacher{suffix}", async (long yearId, ClassTeacherCommand command, HttpContext context, WorkloadService service, CancellationToken token) =>
                ApiResults.Ok(context, await service.ClassTeacherAsync(yearId, command, apply, token)));
            workload.MapPost($"/bulk/transfer{suffix}", async (long yearId, TransferWorkloadCommand command, HttpContext context, WorkloadService service, CancellationToken token) =>
                ApiResults.Ok(context, await service.TransferAsync(yearId, command, apply, token)));
            workload.MapPost($"/bulk/remove{suffix}", async (long yearId, RemoveWorkloadCommand command, HttpContext context, WorkloadService service, CancellationToken token) =>
                ApiResults.Ok(context, await service.RemoveAsync(yearId, command, apply, token)));
        }
        return endpoints;
    }

    public static IEndpointRouteBuilder MapReadinessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOwnerGroup("/academic-years/{yearId:long}/readiness").MapGet("/", async (long yearId, bool? doublePeriods, HttpContext context, ReadinessService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.CheckAsync(yearId, token, new ValidatorOptions(doublePeriods == true))));
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
