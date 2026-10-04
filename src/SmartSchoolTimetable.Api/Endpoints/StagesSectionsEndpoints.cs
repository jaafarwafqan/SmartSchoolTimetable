using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;

namespace SmartSchoolTimetable.Api.Endpoints;

public static class StagesSectionsEndpoints
{
    public static IEndpointRouteBuilder MapStagesSectionsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var stages = endpoints.MapOwnerGroup("/academic-years/{yearId:long}/stages");
        stages.MapGet("/", async (long yearId, [AsParameters] ListQuery query, StagesSectionsService service, CancellationToken token) => Results.Ok(await service.ListStagesAsync(yearId, query, token)));
        stages.MapPost("/", async (long yearId, SaveStageCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) => ApiResults.From(context, await service.CreateStageAsync(yearId, command, token), value => Results.Created($"/api/v1/academic-years/{yearId}/stages/{value.Id}", value)));
        stages.MapPut("/{id:long}", async (long yearId, long id, SaveStageCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) => ApiResults.Ok(context, await service.UpdateStageAsync(yearId, id, command, token)));
        stages.MapPost("/{id:long}/archive", async (long yearId, long id, ArchiveCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) => ApiResults.Ok(context, await service.SetStageArchivedAsync(yearId, id, command, true, token)));
        stages.MapPost("/{id:long}/restore", async (long yearId, long id, ArchiveCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) => ApiResults.Ok(context, await service.SetStageArchivedAsync(yearId, id, command, false, token)));
        stages.MapGet("/{stageId:long}/sections", async (long yearId, long stageId, [AsParameters] ListQuery query, StagesSectionsService service, CancellationToken token) => Results.Ok(await service.ListSectionsAsync(yearId, stageId, query, token)));
        stages.MapPost("/{stageId:long}/sections", async (long yearId, long stageId, SaveSectionCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) => ApiResults.From(context, await service.CreateSectionAsync(yearId, stageId, command, token), value => Results.Created($"/api/v1/academic-years/{yearId}/stages/{stageId}/sections/{value.Id}", value)));
        stages.MapPut("/{stageId:long}/sections/{id:long}", async (long yearId, long stageId, long id, SaveSectionCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) => ApiResults.Ok(context, await service.UpdateSectionAsync(yearId, stageId, id, command, token)));
        stages.MapPost("/{stageId:long}/sections/{id:long}/archive", async (long yearId, long stageId, long id, ArchiveCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) => ApiResults.Ok(context, await service.SetSectionArchivedAsync(yearId, stageId, id, command, true, token)));
        stages.MapPost("/{stageId:long}/sections/{id:long}/restore", async (long yearId, long stageId, long id, ArchiveCommand command, HttpContext context, StagesSectionsService service, CancellationToken token) => ApiResults.Ok(context, await service.SetSectionArchivedAsync(yearId, stageId, id, command, false, token)));
        return endpoints;
    }
}
