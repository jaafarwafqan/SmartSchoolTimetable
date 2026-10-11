using SmartSchoolTimetable.Application.Backup;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>«النسخ الاحتياطي والاستعادة» (Phase 4 M6): owner only, launch-token protected like every state change.</summary>
public static class BackupEndpoints
{
    public static IEndpointRouteBuilder MapBackupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var backup = endpoints.MapOwnerGroup("/backup");
        backup.MapGet("/defaults", (BackupService service) => Results.Ok(service.Defaults()));
        backup.MapGet("/settings", async (BackupService service, CancellationToken token) => Results.Ok(await service.GetSettingsAsync(token)));
        backup.MapPut("/settings", async (SaveBackupSettingsCommand command, HttpContext context, BackupService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateSettingsAsync(command, token)));
        backup.MapGet("/folders", (string? path, HttpContext context) => ApiResults.Ok(context, BackupService.BrowseFolders(path)));
        backup.MapGet("/files", async (string? folder, HttpContext context, BackupService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.ListFilesAsync(folder, token)));
        backup.MapPost("/", async (CreateBackupCommand command, HttpContext context, BackupService service, CancellationToken token) =>
            ApiResults.From(context, await service.CreateAsync(command, token), value => Results.Created((string?)null, value)));
        backup.MapPost("/restore", async (RestoreBackupCommand command, HttpContext context, BackupService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.RestoreAsync(command, token)));
        return endpoints;
    }
}
