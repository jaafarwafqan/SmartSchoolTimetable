using SmartSchoolTimetable.Application.Audit;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>The local audit history (M1): a paged, filtered, newest-first list for the history screen and the dashboard feed.</summary>
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOwnerGroup("/audit").MapGet("/", async ([AsParameters] AuditListQuery query, HttpContext context, AuditService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.ListAsync(query, token)));
        return endpoints;
    }
}
