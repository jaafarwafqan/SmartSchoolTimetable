using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;

namespace SmartSchoolTimetable.Api.Endpoints;

public static class TimetableStructureEndpoints
{
    public static IEndpointRouteBuilder MapTimetableStructureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var shifts = endpoints.MapOwnerGroup("/academic-years/{yearId:long}/shifts");
        shifts.MapPost("/generate-periods", (GeneratePeriodsCommand command, HttpContext context) => ApiResults.Ok(context, TimetableStructureService.Generate(command)));
        shifts.MapGet("/", async (long yearId, [AsParameters] ListQuery query, TimetableStructureService service, CancellationToken token) => Results.Ok(await service.ListShiftsAsync(yearId, query, token)));
        shifts.MapPost("/", async (long yearId, SaveShiftCommand command, HttpContext context, TimetableStructureService service, CancellationToken token) => ApiResults.From(context, await service.CreateShiftAsync(yearId, command, token), value => Results.Created($"/api/v1/academic-years/{yearId}/shifts/{value.Id}", value)));
        shifts.MapPut("/{id:long}", async (long yearId, long id, SaveShiftCommand command, HttpContext context, TimetableStructureService service, CancellationToken token) => ApiResults.Ok(context, await service.UpdateShiftAsync(yearId, id, command, token)));
        shifts.MapDelete("/{id:long}", async (long yearId, long id, int version, HttpContext context, TimetableStructureService service, CancellationToken token) => ApiResults.NoContent(context, await service.DeleteShiftAsync(yearId, id, version, token)));
        shifts.MapPost("/{id:long}/day-lessons/impact", async (long yearId, long id, SetDayLessonsCommand command, HttpContext context, TimetableStructureService service, CancellationToken token) => ApiResults.Ok(context, await service.PreviewDayLessonsAsync(yearId, id, command, token)));
        shifts.MapPut("/{id:long}/day-lessons", async (long yearId, long id, SetDayLessonsCommand command, HttpContext context, TimetableStructureService service, CancellationToken token) => ApiResults.Ok(context, await service.SetDayLessonsAsync(yearId, id, command, token)));
        shifts.MapPut("/{id:long}/periods", async (long yearId, long id, ReplacePeriodsCommand command, HttpContext context, TimetableStructureService service, CancellationToken token) => ApiResults.Ok(context, await service.ReplacePeriodsAsync(yearId, id, command, token)));

        endpoints.MapOwnerGroup("/schedule-grid").MapGet("/", async (TimetableStructureService service, CancellationToken token) => Results.Ok(await service.GetGridAsync(token)));

        var workingWeek = endpoints.MapOwnerGroup("/working-days");
        workingWeek.MapGet("/", async (TimetableStructureService service, CancellationToken token) => Results.Ok(await service.GetWorkingWeekAsync(token)));
        workingWeek.MapPut("/", async (UpdateWorkingWeekCommand command, HttpContext context, TimetableStructureService service, CancellationToken token) => ApiResults.Ok(context, await service.UpdateWorkingWeekAsync(command, token)));

        var bells = endpoints.MapOwnerGroup("/bell-settings");
        bells.MapGet("/", async (TimetableStructureService service, CancellationToken token) => Results.Ok(await service.GetBellSettingsAsync(token)));
        bells.MapPut("/", async (UpdateBellSettingsCommand command, HttpContext context, TimetableStructureService service, CancellationToken token) => ApiResults.Ok(context, await service.UpdateBellSettingsAsync(command, token)));
        return endpoints;
    }
}
