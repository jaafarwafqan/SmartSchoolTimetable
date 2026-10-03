namespace SmartSchoolTimetable.Application;

public interface ILoginDelay
{
    Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken);
}
