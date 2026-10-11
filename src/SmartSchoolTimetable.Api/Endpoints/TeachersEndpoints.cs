using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Teachers;

namespace SmartSchoolTimetable.Api.Endpoints;

public static class TeachersEndpoints
{
    /// <summary>A teachers workbook is a few kilobytes; 2 MB is far more than 500 rows need.</summary>
    private const long ImportRequestLimitBytes = 2 * 1024 * 1024;

    private static async Task<IResult> ImportAsync(HttpContext context, Func<Stream, Task<IResult>> handle, CancellationToken token)
    {
        if (!context.Request.HasFormContentType)
            return ApiResults.Failure(context, ErrorCodes.UnsupportedMediaType);
        // Enforced here too, so the limit holds even on hosts that ignore request-size metadata.
        if (context.Request.ContentLength > ImportRequestLimitBytes)
            return ApiResults.Failure(context, ErrorCodes.PayloadTooLarge);
        var form = await context.Request.ReadFormAsync(token);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
            return ApiResults.From(context, OperationResult.Invalid<bool>("File", ErrorCodes.Required), _ => Results.Empty);
        if (file.Length > ImportRequestLimitBytes)
            return ApiResults.Failure(context, ErrorCodes.PayloadTooLarge);
        await using var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream, token);
        stream.Position = 0;
        return await handle(stream);
    }

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
        teachers.MapPost("/{id:long}/specializations/{subjectId:long}", async (long id, long subjectId, AddSpecializationCommand command, HttpContext context, TeachersService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.AddSpecializationAsync(id, subjectId, command, token)));
        teachers.MapPost("/bulk/preview", async (BulkTeachersCommand command, HttpContext context, TeachersService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.PreviewBulkAsync(command, token)));
        teachers.MapPost("/bulk", async (BulkTeachersCommand command, HttpContext context, TeachersService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.CreateBulkAsync(command, token)));
        teachers.MapGet("/import/template", (TeacherImportService service) =>
            Results.File(service.Template(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "teachers-template.xlsx"));
        teachers.MapPost("/import/preview", async (HttpContext context, TeacherImportService service, CancellationToken token) =>
            await ImportAsync(context, async stream => ApiResults.Ok(context, await service.PreviewAsync(stream, token)), token))
            .WithMetadata(new RequestSizeLimitMetadata(ImportRequestLimitBytes));
        teachers.MapPost("/import", async (HttpContext context, TeacherImportService service, CancellationToken token) =>
            await ImportAsync(context, async stream => ApiResults.Ok(context, await service.ApplyAsync(stream, token)), token))
            .WithMetadata(new RequestSizeLimitMetadata(ImportRequestLimitBytes));
        return endpoints;
    }
}
