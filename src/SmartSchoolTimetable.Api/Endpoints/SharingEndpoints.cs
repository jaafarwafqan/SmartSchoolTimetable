using SmartSchoolTimetable.Application.Sharing;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>
/// «مشاركة الجدول مع الهاتف» (Phase 9, ADR 0056): signed .sst packages for phones. Every export is a POST (it signs with the
/// school key and writes an audit entry), so it carries the per-launch token like any other state-changing request.
/// </summary>
public static class SharingEndpoints
{
    public static IEndpointRouteBuilder MapSharingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var sharing = endpoints.MapOwnerGroup("/sharing");
        sharing.MapGet("/", async (HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.StatusAsync(token)));
        sharing.MapPost("/pairing.sst", async (HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.From(context, await service.PairingAsync(token), File));
        sharing.MapPost("/teachers/{teacherId:long}/package.sst", async (long teacherId, HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.From(context, await service.TeacherAsync(teacherId, token), File));
        sharing.MapPost("/teachers.zip", async (HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.From(context, await service.AllTeachersAsync(token), File));
        sharing.MapPost("/principal.sst", async (HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.From(context, await service.PrincipalAsync(token), File));
        sharing.MapPost("/key/regenerate", async (RegenerateSchoolKeyCommand command, HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.RegenerateKeyAsync(command, token)));
        return endpoints;
    }

    private static IResult File(Application.Generation.TimetableFile file) => Results.File(file.Content, file.ContentType, file.FileName);
}
