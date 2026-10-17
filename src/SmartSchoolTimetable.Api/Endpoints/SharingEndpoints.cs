using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
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
        sharing.MapPost("/pairing-qr", async (HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.PairingQrAsync(token)));
        sharing.MapPost("/teachers/{teacherId:long}/qr", async (long teacherId, HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.TeacherQrAsync(teacherId, token)));
        sharing.MapPost("/cards", async (TeacherCardsCommand command, HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.CardsAsync(command, token)));
        sharing.MapGet("/settings", async (SharingService service, CancellationToken token) => Results.Ok(await service.SettingsAsync(token)));
        sharing.MapPut("/settings", async (SaveSharingSettingsCommand command, HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SaveSettingsAsync(command, token)));
        sharing.MapPost("/kiosk/start", (HttpContext context, KioskAccess kiosk, IDataStore store, TimeProvider clock) =>
        {
            var key = kiosk.Start();
            AuditTrail.Record(store, clock, AuditEvents.SharingKioskStarted, "kiosk", "Staff-room screen opened.");
            return SaveAndReturnAsync(store, new KioskSessionDto(key), context.RequestAborted);
        });
        sharing.MapPost("/kiosk/stop", (KioskAccess kiosk) =>
        {
            kiosk.Stop();
            return Results.NoContent();
        });

        // The staff-room screen: no owner session (it must keep running while the owner's session locks), only the in-memory kiosk key.
        endpoints.MapGet("/api/v1/kiosk/today", async (HttpContext context, KioskAccess kiosk, SharingService service, CancellationToken token) =>
        {
            if (!kiosk.IsValid(context.Request.Headers[KioskKeyHeader]))
                return ApiResults.Failure(context, ErrorCodes.KioskKeyInvalid);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await service.KioskAsync(token));
        });
        sharing.MapPost("/key/backup.sstkey", async (BackupSchoolKeyCommand command, HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.From(context, await service.BackupKeyAsync(command, token), File));
        sharing.MapPost("/key/import", async (ImportSchoolKeyCommand command, HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.ImportKeyAsync(command, token)));
        sharing.MapPost("/key/regenerate", async (RegenerateSchoolKeyCommand command, HttpContext context, SharingService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.RegenerateKeyAsync(command, token)));
        return endpoints;
    }

    public const string KioskKeyHeader = "X-Kiosk-Key";

    private static IResult File(Application.Generation.TimetableFile file) => Results.File(file.Content, file.ContentType, file.FileName);

    private static async Task<IResult> SaveAndReturnAsync(IDataStore store, KioskSessionDto session, CancellationToken token)
    {
        await store.SaveChangesAsync(token);
        return Results.Ok(session);
    }
}
