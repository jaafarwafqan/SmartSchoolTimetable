using System.Diagnostics;

namespace SmartSchoolTimetable.Api;

/// <summary>
/// When the launcher starts the server it names itself (<c>Launcher:ParentPid</c>): if the launcher ends in any way (a crash, a killed process) the server
/// stops too, so no hidden server is ever left running. Not configured when the server is started by hand or by a test.
/// </summary>
public sealed class ParentProcessWatcher(IConfiguration configuration, IHostApplicationLifetime lifetime, ILogger<ParentProcessWatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!int.TryParse(configuration["Launcher:ParentPid"], out var pid) || pid <= 0)
            return;
        Process parent;
        try
        {
            parent = Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            lifetime.StopApplication(); // the launcher is already gone
            return;
        }
        try
        {
            await parent.WaitForExitAsync(stoppingToken);
            LocalLog.LauncherEnded(logger);
            lifetime.StopApplication();
        }
        catch (OperationCanceledException)
        {
            // The server is stopping by itself.
        }
        finally
        {
            parent.Dispose();
        }
    }
}
