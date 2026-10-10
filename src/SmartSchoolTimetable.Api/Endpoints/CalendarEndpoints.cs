using SmartSchoolTimetable.Application.Calendar;
using SmartSchoolTimetable.Application.Common;

namespace SmartSchoolTimetable.Api.Endpoints;

public static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var days = endpoints.MapOwnerGroup("/calendar-days");
        days.MapGet("/", async ([AsParameters] ListQuery query, string? from, string? to, HttpContext context, CalendarService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.ListAsync(query, from, to, token)));
        days.MapPost("/", async (SaveCalendarDayCommand command, HttpContext context, CalendarService service, CancellationToken token) =>
            ApiResults.From(context, await service.CreateAsync(command, token), value => Results.Created($"/api/v1/calendar-days/{value.Id}", value)));
        days.MapGet("/iraq-holidays", async (long yearId, HttpContext context, CalendarService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.PreviewIraqHolidaysAsync(yearId, token)));
        days.MapPost("/iraq-holidays", async (ImportIraqHolidaysCommand command, HttpContext context, CalendarService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.ImportIraqHolidaysAsync(command, token)));
        days.MapPut("/{id:long}/enabled", async (long id, SetCalendarDayEnabledCommand command, HttpContext context, CalendarService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetEnabledAsync(id, command, token)));
        days.MapPut("/{id:long}", async (long id, SaveCalendarDayCommand command, HttpContext context, CalendarService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateAsync(id, command, token)));
        days.MapDelete("/{id:long}", async (long id, int version, HttpContext context, CalendarService service, CancellationToken token) =>
            ApiResults.NoContent(context, await service.DeleteAsync(id, version, token)));
        return endpoints;
    }
}
