using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;

namespace SmartSchoolTimetable.Api.Endpoints;

public static class AcademicYearEndpoints
{
    public static IEndpointRouteBuilder MapAcademicYearEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var years = endpoints.MapOwnerGroup("/academic-years");
        years.MapGet("/", async ([AsParameters] ListQuery query, AcademicYearService service, CancellationToken token) =>
            Results.Ok(await service.ListAsync(query, token)));
        years.MapGet("/{id:long}", async (long id, HttpContext context, AcademicYearService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.GetAsync(id, token)));
        years.MapPost("/", async (SaveAcademicYearCommand command, HttpContext context, AcademicYearService service, CancellationToken token) =>
            ApiResults.From(context, await service.CreateAsync(command, token), year => Results.Created($"/api/v1/academic-years/{year.Id}", year)));
        years.MapPut("/{id:long}", async (long id, SaveAcademicYearCommand command, HttpContext context, AcademicYearService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateAsync(id, command, token)));
        years.MapDelete("/{id:long}", async (long id, int version, HttpContext context, AcademicYearService service, CancellationToken token) =>
            ApiResults.NoContent(context, await service.DeleteAsync(id, version, token)));
        years.MapPost("/{id:long}/make-current", async (long id, VersionCommand command, HttpContext context, AcademicYearService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetCurrentAsync(id, command.Version, token)));

        years.MapPost("/{id:long}/terms", async (long id, SaveTermCommand command, HttpContext context, AcademicYearService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.AddTermAsync(id, command, token)));
        years.MapPut("/{id:long}/terms/{termId:long}", async (long id, long termId, SaveTermCommand command, HttpContext context, AcademicYearService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateTermAsync(id, termId, command, token)));
        years.MapDelete("/{id:long}/terms/{termId:long}", async (long id, long termId, int version, HttpContext context, AcademicYearService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.DeleteTermAsync(id, termId, version, token)));
        years.MapPost("/{id:long}/terms/{termId:long}/make-current", async (long id, long termId, VersionCommand command, HttpContext context, AcademicYearService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetCurrentTermAsync(id, termId, command.Version, token)));
        return endpoints;
    }
}
