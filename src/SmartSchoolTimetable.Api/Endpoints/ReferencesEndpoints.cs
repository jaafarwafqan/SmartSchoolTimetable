using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>Reference preview (Phase 3 §5.3): what depends on a record, before the owner deletes or archives it.</summary>
public static class ReferencesEndpoints
{
    public static IEndpointRouteBuilder MapReferencesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var references = endpoints.MapOwnerGroup("/references");
        references.MapGet("/{kind}/{id:long}", async (string kind, long id, HttpContext context, ReferenceGuard guard, CancellationToken token) =>
            await guard.InspectAsync(kind, id, token) is { } report ? Results.Ok(report) : ApiResults.Failure(context, ErrorCodes.NotFound));
        return endpoints;
    }
}
