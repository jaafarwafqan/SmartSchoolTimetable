using SmartSchoolTimetable.Application.Backup;

namespace SmartSchoolTimetable.Api;

/// <summary>
/// The automatic backup (M2): a first check shortly after the application starts, then once an hour. The service itself decides whether
/// a backup is due (switched on, none in the last 20 hours, an owner account exists). A failure is logged in Arabic-free developer text
/// and never stops the application.
/// </summary>
public sealed class AutoBackupWorker(IServiceScopeFactory scopes, ILogger<AutoBackupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstCheck, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckOnceAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The application is stopping.
        }
    }

    private async Task CheckOnceAsync(CancellationToken token)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<BackupService>().RunAutoBackupAsync(token);
            if (outcome.Created)
                LocalLog.AutoBackupCreated(logger, outcome.Pruned);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            LocalLog.AutoBackupFailed(logger, exception);
        }
    }
}
