using System.Diagnostics;
using System.Text;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Backup;
using SmartSchoolTimetable.Infrastructure.Persistence;

namespace SmartSchoolTimetable.Api.Endpoints;

/// <param name="Name">Shown as data (a product name), not translated.</param>
public sealed record ThirdPartyLicenseDto(string Name, string License);

/// <param name="SchemaVersion">How many database migrations this build has (the schema the data is at).</param>
/// <param name="License">The licence notice, Arabic part first.</param>
public sealed record AboutDto(string Version, string BuildDate, int SchemaVersion, string License, string DataFolder, IReadOnlyList<ThirdPartyLicenseDto> ThirdParty);

/// <summary>Opens a folder in the system file manager. The only caller opens the application's own data folder; no path comes from a request.</summary>
public interface IFolderOpener
{
    bool Open(string folder);
}

public sealed class ExplorerFolderOpener : IFolderOpener
{
    public bool Open(string folder)
    {
        if (!OperatingSystem.IsWindows() || !Directory.Exists(folder))
            return false;
        try
        {
            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            start.ArgumentList.Add(Path.GetFullPath(folder));
            using var process = Process.Start(start);
            return process is not null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}

/// <summary>«حول البرنامج»: version, licence, data folder, support log. Owner only; the data-folder action also needs the launch token (it is a POST).</summary>
public static class AboutEndpoints
{
    /// <summary>The components the product ships with and their licences (names are product names, shown as data).</summary>
    public static readonly IReadOnlyList<ThirdPartyLicenseDto> ThirdParty =
    [
        new("ASP.NET Core / .NET", "MIT"),
        new("Entity Framework Core", "MIT"),
        new("SQLite", "Public Domain"),
        new("Google OR-Tools", "Apache-2.0"),
        new("ClosedXML", "MIT"),
        new("FluentValidation", "Apache-2.0"),
        new("React / React DOM", "MIT"),
        new("React Router", "MIT"),
        new("TanStack Query", "MIT"),
        new("Zustand", "MIT"),
        new("Lucide", "ISC"),
        new("Noto Sans Arabic", "OFL-1.1"),
    ];

    public static IEndpointRouteBuilder MapAboutEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var about = endpoints.MapOwnerGroup("/about");
        about.MapGet("/", (LocalDatabaseLocation location, IDatabaseBackup backup) =>
            Results.Ok(new AboutDto(AppInfo.Version, AppInfo.BuildDate, backup.KnownMigrations.Count, AppInfo.LicenseText(), DataFolder(location), ThirdParty)));
        about.MapPost("/open-data-folder", (HttpContext context, LocalDatabaseLocation location, IFolderOpener opener) =>
            opener.Open(DataFolder(location)) ? Results.NoContent() : ApiResults.Failure(context, ErrorCodes.OpenFolderFailed));
        about.MapGet("/support-log", async (SupportLogBuilder builder, TimeProvider clock, CancellationToken token) =>
        {
            var content = await builder.BuildAsync(token);
            return Results.File([.. new UTF8Encoding(true).GetPreamble(), .. Encoding.UTF8.GetBytes(content)], "text/plain; charset=utf-8",
                $"support-log-{clock.GetUtcNow():yyyyMMdd-HHmmss}.txt");
        });
        return endpoints;
    }

    /// <summary>The application's own data folder (where the database lives): the only folder the app ever opens.</summary>
    private static string DataFolder(LocalDatabaseLocation location) => Path.GetDirectoryName(Path.GetFullPath(location.DatabasePath))!;
}
