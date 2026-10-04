using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Subjects;

namespace SmartSchoolTimetable.Api.Endpoints;

public static class SubjectsEndpoints
{
    public static IEndpointRouteBuilder MapSubjectsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var subjects = endpoints.MapOwnerGroup("/subjects");
        subjects.MapGet("/", async ([AsParameters] ListQuery query, SubjectsService service, CancellationToken token) =>
            Results.Ok(await service.ListAsync(query, token)));
        subjects.MapPost("/", async (SaveSubjectCommand command, HttpContext context, SubjectsService service, CancellationToken token) =>
            ApiResults.From(context, await service.CreateAsync(command, token), value => Results.Created($"/api/v1/subjects/{value.Id}", value)));
        subjects.MapPut("/{id:long}", async (long id, SaveSubjectCommand command, HttpContext context, SubjectsService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateAsync(id, command, token)));
        subjects.MapDelete("/{id:long}", async (long id, int version, HttpContext context, SubjectsService service, CancellationToken token) =>
            ApiResults.NoContent(context, await service.DeleteAsync(id, version, token)));
        subjects.MapPost("/{id:long}/archive", async (long id, ArchiveCommand command, HttpContext context, SubjectsService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetArchivedAsync(id, command.Version, true, token)));
        subjects.MapPost("/{id:long}/restore", async (long id, ArchiveCommand command, HttpContext context, SubjectsService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetArchivedAsync(id, command.Version, false, token)));
        return endpoints;
    }
}
