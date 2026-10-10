using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Generation;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>Timetable generation (Phase 4 M2): start, poll, cancel, list. Progress is polled; there is no push channel.</summary>
public static class GenerationEndpoints
{
    public static IEndpointRouteBuilder MapGenerationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var engine = endpoints.MapOwnerGroup("/generation");
        engine.MapGet("/engine", (GenerationService service) => Results.Ok(service.Engine()));
        engine.MapGet("/runs/{id:long}", async (long id, HttpContext context, GenerationService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.GetAsync(id, token)));
        engine.MapPost("/runs/{id:long}/cancel", async (long id, HttpContext context, GenerationService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.CancelAsync(id, token)));

        var runs = endpoints.MapOwnerGroup("/academic-years/{yearId:long}/generation");
        runs.MapPost("/runs", async (long yearId, StartGenerationCommand command, HttpContext context, GenerationService service, CancellationToken token) =>
            ApiResults.From(context, await service.StartAsync(yearId, command, token), value => Results.Accepted($"/api/v1/generation/runs/{value.Id}", value)));
        runs.MapGet("/runs", async (long yearId, [AsParameters] ListQuery query, GenerationService service, CancellationToken token) =>
            Results.Ok(await service.ListAsync(yearId, query, token)));
        runs.MapGet("/manual-edits", async (long yearId, GenerationService service, CancellationToken token) =>
            Results.Ok(await service.ManualEditsOfLatestAsync(yearId, token)));
        runs.MapGet("/current", async (long yearId, GenerationService service, CancellationToken token) =>
            Results.Ok(new CurrentRunDto(await service.CurrentAsync(yearId, token))));
        return endpoints;
    }

    /// <summary>The latest run, or null when the year has none (an object, so the response is never an empty body).</summary>
    public sealed record CurrentRunDto(GenerationRunDto? Run);

    /// <summary>Saved timetables (Phase 4 M3): versions of a year, one version's grids, approval.</summary>
    public static IEndpointRouteBuilder MapTimetableEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOwnerGroup("/academic-years/{yearId:long}/timetables").MapGet("/", async (long yearId, HttpContext context, TimetableService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.ListAsync(yearId, token)));
        var timetables = endpoints.MapOwnerGroup("/timetables");
        timetables.MapGet("/{id:long}", async (long id, HttpContext context, TimetableService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.GetAsync(id, token)));
        timetables.MapPost("/{id:long}/check", async (long id, EditedTimetableCommand command, HttpContext context, TimetableService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.CheckAsync(id, command, token)));
        timetables.MapPost("/{id:long}/edits", async (long id, EditedTimetableCommand command, HttpContext context, TimetableService service, CancellationToken token) =>
            ApiResults.From(context, await service.SaveEditAsync(id, command, token), value => Results.Created($"/api/v1/timetables/{value.Id}", value)));
        timetables.MapGet("/{id:long}/export.xlsx", async (long id, int? term, HttpContext context, TimetableExportService service, CancellationToken token) =>
            ApiResults.From(context, await service.ExcelAsync(id, token, term ?? 1), file => Results.File(file.Content, file.ContentType, file.FileName)));
        timetables.MapGet("/{fromId:long}/compare/{toId:long}", async (long fromId, long toId, HttpContext context, TimetableService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.CompareAsync(fromId, toId, token)));
        timetables.MapPost("/{id:long}/archive", async (long id, ArchiveTimetableCommand command, HttpContext context, TimetableService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.ArchiveAsync(id, command, token)));
        timetables.MapPost("/{id:long}/rollback", async (long id, RollbackTimetableCommand command, HttpContext context, TimetableService service, CancellationToken token) =>
            ApiResults.From(context, await service.RollbackAsync(id, command, token), value => Results.Created($"/api/v1/timetables/{value.Id}", value)));
        timetables.MapPost("/{id:long}/approve", async (long id, ApproveTimetableCommand command, HttpContext context, TimetableService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.ApproveAsync(id, command, token)));
        return endpoints;
    }
}

/// <summary>
/// The local generation worker (ADR 0037): reads queued runs one at a time and runs each in its own scope, off the
/// request path. Closing the browser does not stop it; stopping the app leaves the run to be marked Interrupted.
/// </summary>
public sealed class GenerationWorker(IServiceScopeFactory scopes, GenerationRegistry registry, ILogger<GenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in registry.Queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<GenerationService>().ExecuteAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // One failed run must not stop the worker; the run stays visible and the next start recovers it.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LocalLog.GenerationFailed(logger, runId, exception);
            }
        }
    }
}
