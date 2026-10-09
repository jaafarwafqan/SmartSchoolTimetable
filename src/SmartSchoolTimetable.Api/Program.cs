using SmartSchoolTimetable.Application.Setup;
using SmartSchoolTimetable.Application.Curriculum;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using SmartSchoolTimetable.Api;
using SmartSchoolTimetable.Api.Endpoints;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Dashboard;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Application.Teachers;
using SmartSchoolTimetable.Application.Calendar;
using SmartSchoolTimetable.Infrastructure;

const string resetArgument = "--reset-local-database";
var resetRequested = args.Contains(resetArgument, StringComparer.Ordinal);
var builderArguments = args.Where(argument => argument != resetArgument).ToArray();
var builder = WebApplication.CreateBuilder(builderArguments);
var localOptions = LocalApplicationOptions.FromConfiguration(builder.Configuration);
LocalListenerGuard.ValidateConfiguredEndpoint(IPAddress.Loopback, localOptions.Port);
builder.WebHost.ConfigureKestrel(options =>
    options.Listen(IPAddress.Loopback, localOptions.Port));

var defaultDatabasePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SmartSchoolTimetable",
    "timetable.db");
var databasePath = builder.Configuration["Database:Path"] ?? defaultDatabasePath;

if (resetRequested)
{
    // The reset prompts are Arabic; make the console exchange UTF-8 text on Windows.
    Console.OutputEncoding = Encoding.UTF8;
    Console.InputEncoding = Encoding.UTF8;
    LocalDatabaseReset.DeleteAfterConfirmation(databasePath, Console.In, Console.Out);
    return;
}

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));
builder.Services.AddOpenApi();
builder.Services.AddValidatorsFromAssemblyContaining<SetupRequestValidator>();
builder.Services.AddSingleton(localOptions);
builder.Services.AddSingleton(new SemaphoreSlim(1, 1));
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<LocalLaunchToken>();
builder.Services.AddScoped<ILocalAuthService>(services => new LocalAuthService(
    services.GetRequiredService<IOwnerRepository>(),
    services.GetRequiredService<ICredentialHasher>(),
    services.GetRequiredService<ILocalSessionStore>(),
    services.GetRequiredService<ILoginDelay>(),
    services.GetRequiredService<TimeProvider>(),
    localOptions.InactivityTimeout, // configured default; an owner preference overrides it at runtime
    services.GetRequiredService<SemaphoreSlim>()));
builder.Services.AddScoped<RequireOwnerSessionFilter>();
builder.Services.AddScoped<SchoolProfileService>();
builder.Services.AddScoped<SchoolContextService>();
builder.Services.AddScoped<AcademicYearService>();
builder.Services.AddScoped<TimetableStructureService>();
builder.Services.AddScoped<OrphanBlockedPeriodsService>();
builder.Services.AddScoped<StagesSectionsService>();
builder.Services.AddScoped<SubjectsService>();
builder.Services.AddScoped<TeachersService>();
builder.Services.AddScoped<CalendarService>();
builder.Services.AddScoped<ShiftModeService>();
builder.Services.AddScoped<SessionPlanService>();
builder.Services.AddScoped<SetupProgressService>();
builder.Services.AddScoped<StageCardsService>();
builder.Services.AddScoped<CurriculumService>();
builder.Services.AddScoped<CurriculumHelpersService>();
builder.Services.AddScoped<SetupTemplatesService>();
builder.Services.AddScoped<SetupWizardService>();
builder.Services.AddScoped<SuggestedCurriculumService>();
builder.Services.AddScoped<DailySuggestionService>();
builder.Services.AddScoped<IYearStructure, YearStructureService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<SmartSchoolTimetable.Application.Resources.ResourcesService>();
builder.Services.AddScoped<SmartSchoolTimetable.Application.Scheduling.SchedulingProfileService>();
builder.Services.AddScoped<SmartSchoolTimetable.Application.Workload.WorkloadService>();
builder.Services.AddScoped<SmartSchoolTimetable.Application.Scheduling.ReadinessService>();
builder.Services.AddScoped<SmartSchoolTimetable.Application.Common.ReferenceGuard>();
builder.Services.AddSingleton<SmartSchoolTimetable.Application.Generation.GenerationRegistry>();
builder.Services.AddScoped<SmartSchoolTimetable.Application.Generation.GenerationService>();
builder.Services.AddScoped<SmartSchoolTimetable.Application.Generation.TimetableService>();
builder.Services.AddScoped<SmartSchoolTimetable.Application.Generation.TimetableExportService>();
builder.Services.AddScoped<SmartSchoolTimetable.Application.Backup.BackupService>();
builder.Services.AddHostedService<GenerationWorker>();
builder.Services.AddLocalInfrastructure(
    databasePath,
    builder.Environment.IsEnvironment("Testing"));

var app = builder.Build();
await LocalInfrastructureRegistration.InitializeLocalDatabaseAsync(app.Services);
await using (var startupScope = app.Services.CreateAsyncScope())
{
    // Runs left active by a closed or crashed app are marked Interrupted (Phase 4 §6).
    var interrupted = await startupScope.ServiceProvider.GetRequiredService<SmartSchoolTimetable.Application.Generation.GenerationService>()
        .RecoverInterruptedAsync(CancellationToken.None);
    if (interrupted > 0)
        LocalLog.GenerationsInterrupted(app.Logger, interrupted);
}
// Solver self-check: a missing native library disables generation with an Arabic error instead of a crash.
if (!app.Services.GetRequiredService<SmartSchoolTimetable.Application.Generation.ISolverInfo>().Available)
    LocalLog.SolverUnavailable(app.Logger);

app.UseExceptionHandler();
app.UseMiddleware<UnifiedApiErrorMiddleware>();
app.UseMiddleware<LocalRequestSecurityMiddleware>();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapLocalAuthEndpoints();
app.MapSchoolSetupEndpoints();
app.MapAcademicYearEndpoints();
app.MapTimetableStructureEndpoints();
app.MapStagesSectionsEndpoints();
app.MapSubjectsEndpoints();
app.MapTeachersEndpoints();
app.MapCalendarEndpoints();
app.MapSetupEndpoints();
app.MapCurriculumEndpoints();
app.MapReferencesEndpoints();
app.MapResourcesEndpoints();
app.MapSchedulingProfileEndpoints();
app.MapWorkloadEndpoints();
app.MapReadinessEndpoints();
app.MapGenerationEndpoints();
app.MapTimetableEndpoints();
app.MapBackupEndpoints();
app.MapFallback(async (HttpContext context) =>
{
    if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        var methodEndpoint = app.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .FirstOrDefault(endpoint =>
                string.Equals(
                    endpoint.RoutePattern.RawText,
                    context.Request.Path.Value,
                    StringComparison.OrdinalIgnoreCase) &&
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>() is not null);
        if (methodEndpoint is null)
        {
            context.Items[UnifiedApiErrorMiddleware.ErrorCodeItem] = ErrorCodes.NotFound;
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var supportedMethods = methodEndpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods;
        if (supportedMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            context.Items[UnifiedApiErrorMiddleware.ErrorCodeItem] = ErrorCodes.UnsupportedMediaType;
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        context.Items[UnifiedApiErrorMiddleware.ErrorCodeItem] = ErrorCodes.MethodNotAllowed;
        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        context.Response.Headers.Allow = string.Join(", ", supportedMethods);
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath!, "index.html"));
});

await app.StartAsync();
if (!app.Environment.IsEnvironment("Testing"))
{
    var server = app.Services.GetRequiredService<IServer>();
    var boundAddresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
    LocalListenerGuard.ValidateBoundAddresses(boundAddresses, localOptions.Port);
}

await app.WaitForShutdownAsync();

public partial class Program;
