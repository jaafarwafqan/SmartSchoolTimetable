using SmartSchoolTimetable.Application.Backup;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>«النسخ الاحتياطي والاستعادة» (Phase 4 M6): owner only, launch-token protected like every state change.</summary>
public static class BackupEndpoints
{
    public static IEndpointRouteBuilder MapBackupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var backup = endpoints.MapOwnerGroup("/backup");
        backup.MapGet("/defaults", () => Results.Ok(BackupService.Defaults()));
        backup.MapPost("/", async (CreateBackupCommand command, HttpContext context, BackupService service, CancellationToken token) =>
            ApiResults.From(context, await service.CreateAsync(command, token), value => Results.Created((string?)null, value)));
        backup.MapPost("/restore", async (RestoreBackupCommand command, HttpContext context, BackupService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.RestoreAsync(command, token)));
        return endpoints;
    }
}
