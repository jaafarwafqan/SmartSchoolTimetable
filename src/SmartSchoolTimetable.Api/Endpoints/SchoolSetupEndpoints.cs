using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Dashboard;
using SmartSchoolTimetable.Application.SchoolSetup;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <summary>School profile, its images, the app-shell context and the dashboard summary.</summary>
public static class SchoolSetupEndpoints
{
    /// <summary>Upload requests may be slightly larger than the 2 MB image limit (multipart overhead).</summary>
    private const long UploadRequestLimitBytes = 3 * 1024 * 1024;

    public static IEndpointRouteBuilder MapSchoolSetupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var profile = endpoints.MapOwnerGroup("/school-profile");
        profile.MapGet("/", async (SchoolProfileService service, CancellationToken token) =>
            Results.Ok(await service.GetAsync(token)));
        profile.MapPut("/", async (UpdateSchoolProfileCommand command, HttpContext context, SchoolProfileService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.UpdateAsync(command, token)));
        profile.MapGet("/{kind}", GetAsset);
        // The Origin check and per-launch token already protect this state-changing request (no cookie-only CSRF),
        // so ASP.NET antiforgery for form binding is not used.
        profile.MapPost("/{kind}", UploadAsset)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitMetadata(UploadRequestLimitBytes));
        profile.MapDelete("/{kind}", async (string kind, int version, HttpContext context, SchoolProfileService service, CancellationToken token) =>
            ApiResults.Ok(context, await service.RemoveAssetAsync(kind, version, token)));

        endpoints.MapOwnerGroup("/school-context").MapGet("/", async (SchoolContextService service, CancellationToken token) =>
            Results.Ok(await service.GetAsync(token)));
        endpoints.MapOwnerGroup("/dashboard-summary").MapGet("/", async (DashboardService service, CancellationToken token) =>
            Results.Ok(await service.GetSummaryAsync(token)));
        return endpoints;
    }

    private static async Task<IResult> GetAsset(string kind, HttpContext context, SchoolProfileService service, CancellationToken token)
    {
        if (await service.OpenAssetAsync(kind, token) is not { } asset)
            return ApiResults.Failure(context, ErrorCodes.NotFound);
        // Images are served as inert downloads-for-display: exact type, no sniffing, no script execution.
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.ContentDisposition = $"inline; filename=\"{asset.FileName}\"";
        return Results.Stream(asset.Content, asset.ContentType);
    }

    private static async Task<IResult> UploadAsset(string kind, HttpContext context, SchoolProfileService service, CancellationToken token)
    {
        if (!context.Request.HasFormContentType)
            return ApiResults.Failure(context, ErrorCodes.UnsupportedMediaType);
        // Enforced here too, so the limit holds even on hosts that ignore request-size metadata.
        if (context.Request.ContentLength > UploadRequestLimitBytes)
            return ApiResults.Failure(context, ErrorCodes.PayloadTooLarge);
        var form = await context.Request.ReadFormAsync(token);
        if (!int.TryParse(form["version"], out var version))
            return ApiResults.From(context, OperationResult.Invalid<bool>("Version", ErrorCodes.Required), _ => Results.Empty);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
            return ApiResults.From(context, OperationResult.Invalid<bool>("File", ErrorCodes.Required), _ => Results.Empty);
        if (file.Length > ImageSignature.MaxBytes)
            return ApiResults.From(context, OperationResult.Invalid<bool>("File", ErrorCodes.AssetTooLarge), _ => Results.Empty);

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, token);
        return ApiResults.Ok(context, await service.UploadAssetAsync(kind, buffer.ToArray(), file.ContentType, version, token));
    }
}

internal sealed class RequestSizeLimitMetadata(long maxRequestBodySize) : Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata
{
    public long? MaxRequestBodySize { get; } = maxRequestBodySize;
}
