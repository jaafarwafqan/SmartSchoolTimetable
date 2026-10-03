using SmartSchoolTimetable.Application;

namespace SmartSchoolTimetable.Infrastructure;

public sealed class RealLoginDelay : ILoginDelay
{
    public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);
}
