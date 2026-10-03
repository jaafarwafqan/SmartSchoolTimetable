using System.Net;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using SmartSchoolTimetable.Api;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Infrastructure;

const string resetArgument = "--reset-local-database";
var resetRequested = args.Contains(resetArgument, StringComparer.Ordinal);
var builderArguments = args.Where(argument =>
    !string.Equals(argument, resetArgument, StringComparison.Ordinal)).ToArray();
var builder = WebApplication.CreateBuilder(builderArguments);
var localOptions = LocalApplicationOptions.FromConfiguration(builder.Configuration);
LocalListenerGuard.ValidateConfiguredEndpoint(IPAddress.Loopback, localOptions.Port);
builder.WebHost.ConfigureKestrel(options =>
    options.Listen(IPAddress.Loopback, localOptions.Port));

var databasePath = builder.Configuration["Database:Path"] ??
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SmartSchoolTimetable",
        "timetable.db");

if (resetRequested)
{
    // The reset prompts are Arabic; make the console exchange UTF-8 text on Windows.
    Console.OutputEncoding = Encoding.UTF8;
    Console.InputEncoding = Encoding.UTF8;
    LocalDatabaseReset.DeleteAfterConfirmation(databasePath, Console.In, Console.Out);
    return;
}

builder.Services.AddProblemDetails();
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
    localOptions.InactivityTimeout,
    services.GetRequiredService<SemaphoreSlim>()));
builder.Services.AddLocalInfrastructure(
    databasePath,
    builder.Environment.IsEnvironment("Testing"));

var app = builder.Build();
await LocalInfrastructureRegistration.InitializeLocalDatabaseAsync(app.Services);

app.UseExceptionHandler();
app.UseMiddleware<UnifiedApiErrorMiddleware>();
app.UseMiddleware<LocalRequestSecurityMiddleware>();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapLocalAuthEndpoints();
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
