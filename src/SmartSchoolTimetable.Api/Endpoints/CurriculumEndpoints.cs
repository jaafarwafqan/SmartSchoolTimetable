using SmartSchoolTimetable.Application.Curriculum;
using SmartSchoolTimetable.Application.Setup;
using SmartSchoolTimetable.Application.Stages;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>
/// Stage cards with a section stepper, the curriculum table and its helpers, and the setup templates
/// (spec 2.5 §3.3, §4). Every helper has a side-effect-free "/preview" twin.
/// </summary>
public static class CurriculumEndpoints
{
    public static IEndpointRouteBuilder MapCurriculumEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var year = endpoints.MapOwnerGroup("/academic-years/{yearId:long}");
        year.MapGet("/stage-cards", async (long yearId, bool? includeArchived, StageCardsService service, CancellationToken token) =>
            Results.Ok(await service.ListAsync(yearId, includeArchived == true, token)));
        year.MapPut("/stage-cards/{stageId:long}/section-count", async (long yearId, long stageId, SetSectionCountCommand command, HttpContext context, StageCardsService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetSectionCountAsync(yearId, stageId, command, token)));

        year.MapGet("/curriculum", async (long yearId, CurriculumService service, CancellationToken token) =>
            Results.Ok(await service.GetTableAsync(yearId, token)));
        year.MapPut("/curriculum/cell", async (long yearId, SetCurriculumCellCommand command, HttpContext context, CurriculumService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetCellAsync(yearId, command, token)));
        year.MapPost("/curriculum/copy/preview", async (long yearId, CopyCurriculumCommand command, HttpContext context, CurriculumHelpersService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.CopyAsync(yearId, command, false, token)));
        year.MapPost("/curriculum/copy", async (long yearId, CopyCurriculumCommand command, HttpContext context, CurriculumHelpersService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.CopyAsync(yearId, command, true, token)));
        year.MapPost("/curriculum/set-across/preview", async (long yearId, SetLessonsAcrossCommand command, HttpContext context, CurriculumHelpersService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetAcrossAsync(yearId, command, false, token)));
        year.MapPost("/curriculum/set-across", async (long yearId, SetLessonsAcrossCommand command, HttpContext context, CurriculumHelpersService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetAcrossAsync(yearId, command, true, token)));

        year.MapPost("/templates/stages/preview", async (long yearId, StageTemplateCommand command, HttpContext context, SetupTemplatesService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.StagesAsync(yearId, command, false, token)));
        year.MapPost("/templates/stages", async (long yearId, StageTemplateCommand command, HttpContext context, SetupTemplatesService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.StagesAsync(yearId, command, true, token)));
        year.MapPost("/curriculum/suggested/preview", async (long yearId, SuggestedCurriculumCommand command, HttpContext context, SuggestedCurriculumService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.PreviewAsync(yearId, command, token)));
        year.MapPost("/curriculum/suggested", async (long yearId, SuggestedCurriculumCommand command, HttpContext context, SuggestedCurriculumService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.ApplyAsync(yearId, command, token)));
        year.MapPost("/curriculum/suggested/stages/{stageId:long}/reset/preview", async (long yearId, long stageId, SuggestedCurriculumCommand command, HttpContext context, SuggestedCurriculumService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.PreviewResetAsync(yearId, stageId, command, token)));
        year.MapPost("/curriculum/suggested/stages/{stageId:long}/reset", async (long yearId, long stageId, SuggestedCurriculumCommand command, HttpContext context, SuggestedCurriculumService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.ResetStageAsync(yearId, stageId, command, token)));
        year.MapGet("/daily-suggestion", async (long yearId, DailySuggestionService service, CancellationToken token) =>
            Results.Ok(await service.PreviewAsync(yearId, token)));
        year.MapPost("/daily-suggestion", async (long yearId, ApplyDailySuggestionCommand command, HttpContext context, DailySuggestionService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.ApplyAsync(yearId, command, token)));
        year.MapGet("/templates/suggested-subjects", async (long yearId, SetupTemplatesService service, CancellationToken token) =>
            Results.Ok(await service.SuggestedSubjectsAsync(yearId, token)));

        var entries = endpoints.MapOwnerGroup("/curriculum-entries");
        entries.MapPut("/{id:long}", async (long id, SaveCurriculumEntryCommand command, HttpContext context, CurriculumService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateEntryAsync(id, command, token)));
        entries.MapDelete("/{id:long}", async (long id, int version, HttpContext context, CurriculumService service, CancellationToken token) =>
            ApiResults.NoContent(context, await service.DeleteEntryAsync(id, version, token)));
        entries.MapPost("/{id:long}/archive", async (long id, ArchiveCommand command, HttpContext context, CurriculumService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetArchivedAsync(id, command.Version, true, token)));
        entries.MapPost("/{id:long}/restore", async (long id, ArchiveCommand command, HttpContext context, CurriculumService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SetArchivedAsync(id, command.Version, false, token)));

        var templates = endpoints.MapOwnerGroup("/templates");
        templates.MapGet("/", () => Results.Ok(SetupTemplatesService.Catalog()));
        templates.MapPost("/subjects/preview", async (SubjectTemplateCommand command, HttpContext context, SetupTemplatesService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SubjectsAsync(command, false, token)));
        templates.MapPost("/subjects", async (SubjectTemplateCommand command, HttpContext context, SetupTemplatesService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.SubjectsAsync(command, true, token)));
        return endpoints;
    }
}
