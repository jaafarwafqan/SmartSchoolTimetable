using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Teachers;

namespace SmartSchoolTimetable.Api.Endpoints;

public static class TeachersEndpoints
{
    public static IEndpointRouteBuilder MapTeachersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var teachers = endpoints.MapOwnerGroup("/teachers");
        teachers.MapGet("/", async ([AsParameters] ListQuery query, bool? released, TeachersService service, CancellationToken token) =>
            Results.Ok(await service.ListAsync(query, released, token)));
        teachers.MapPost("/", async (SaveTeacherCommand command, HttpContext context, TeachersService service, CancellationToken token) =>
            ApiResults.From(context, await service.CreateAsync(command, token), value => Results.Created($"/api/v1/teachers/{value.Id}", value)));
        teachers.MapPut("/{id:long}", async (long id, SaveTeacherCommand command, HttpContext context, TeachersService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateAsync(id, command, token)));
        teachers.MapDelete("/{id:long}", async (long id, int version, HttpContext context, TeachersService service, CancellationToken token) =>
            ApiResults.NoContent(context, await service.DeleteAsync(id, version, token)));
        teachers.MapPost("/{id:long}/archive", async (long id, ArchiveCommand command, HttpContext context, TeachersService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetArchivedAsync(id, command.Version, true, token)));
        teachers.MapPost("/{id:long}/restore", async (long id, ArchiveCommand command, HttpContext context, TeachersService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetArchivedAsync(id, command.Version, false, token)));
        teachers.MapPost("/bulk/preview", async (BulkTeachersCommand command, HttpContext context, TeachersService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.PreviewBulkAsync(command, token)));
        teachers.MapPost("/bulk", async (BulkTeachersCommand command, HttpContext context, TeachersService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.CreateBulkAsync(command, token)));
        return endpoints;
    }
}
