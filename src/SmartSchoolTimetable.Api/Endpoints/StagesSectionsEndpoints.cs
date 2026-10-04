using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Stages;

namespace SmartSchoolTimetable.Api.Endpoints;

public static class StagesSectionsEndpoints
{
    public static IEndpointRouteBuilder MapStagesSectionsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var stages = endpoints.MapOwnerGroup("/academic-years/{yearId:long}/stages");
        stages.MapGet("/", async (long yearId, [AsParameters] ListQuery query, StagesSectionsService service, CancellationToken token) =>
            Results.Ok(await service.ListStagesAsync(yearId, query, token)));
        stages.MapPost("/", async (long yearId, SaveStageCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) =>
            ApiResults.From(context, await service.CreateStageAsync(yearId, command, token), value => Results.Created($"/api/v1/academic-years/{yearId}/stages/{value.Id}", value)));
        stages.MapPut("/{id:long}", async (long yearId, long id, SaveStageCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateStageAsync(yearId, id, command, token)));
        stages.MapDelete("/{id:long}", async (long yearId, long id, int version, HttpContext context, StagesSectionsService service, CancellationToken token) =>
            ApiResults.NoContent(context, await service.DeleteStageAsync(yearId, id, version, token)));
        stages.MapPost("/{id:long}/archive", async (long yearId, long id, ArchiveCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetStageArchivedAsync(yearId, id, command, true, token)));
        stages.MapPost("/{id:long}/restore", async (long yearId, long id, ArchiveCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetStageArchivedAsync(yearId, id, command, false, token)));

        var sections = stages.MapGroup("/{stageId:long}/sections");
        sections.MapGet("/", async (long yearId, long stageId, [AsParameters] ListQuery query, StagesSectionsService service, CancellationToken token) =>
            Results.Ok(await service.ListSectionsAsync(yearId, stageId, query, token)));
        sections.MapPost("/", async (long yearId, long stageId, SaveSectionCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) =>
            ApiResults.From(context, await service.CreateSectionAsync(yearId, stageId, command, token), value => Results.Created($"/api/v1/academic-years/{yearId}/stages/{stageId}/sections/{value.Id}", value)));
        sections.MapPut("/{id:long}", async (long yearId, long stageId, long id, SaveSectionCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateSectionAsync(yearId, stageId, id, command, token)));
        sections.MapDelete("/{id:long}", async (long yearId, long stageId, long id, int version, HttpContext context, StagesSectionsService service, CancellationToken token) =>
            ApiResults.NoContent(context, await service.DeleteSectionAsync(yearId, stageId, id, version, token)));
        sections.MapPost("/{id:long}/archive", async (long yearId, long stageId, long id, ArchiveCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetSectionArchivedAsync(yearId, stageId, id, command, true, token)));
        sections.MapPost("/{id:long}/restore", async (long yearId, long stageId, long id, ArchiveCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetSectionArchivedAsync(yearId, stageId, id, command, false, token)));
        return endpoints;
    }
}
