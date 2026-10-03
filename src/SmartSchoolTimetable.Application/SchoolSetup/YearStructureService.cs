namespace SmartSchoolTimetable.Application.SchoolSetup;

/// <summary>
/// Year-scoped structure checks and copying. Checkpoint 2A has no year-scoped structure yet, so a year
/// never has structure to protect or copy; shifts/periods (2B) and stages/sections (2C) extend this class.
/// </summary>
public sealed class YearStructureService : IYearStructure
{
    public Task<bool> HasStructureAsync(long yearId, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task CopyAsync(long sourceYearId, long targetYearId, CancellationToken cancellationToken) => Task.CompletedTask;
}
