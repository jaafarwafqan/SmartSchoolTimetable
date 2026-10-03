using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.SchoolSetup;

/// <summary>
/// Year-scoped shift structure checks and copying. Stages/sections extend this in checkpoint 2C.
/// </summary>
public sealed class YearStructureService(IDataStore store) : IYearStructure
{
    public Task<bool> HasStructureAsync(long yearId, CancellationToken cancellationToken) =>
        store.AnyAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == yearId), cancellationToken);

    public async Task CopyAsync(long sourceYearId, long targetYearId, CancellationToken cancellationToken)
    {
        var sourceShifts = await store.ListAsync(
            store.Query<Shift>().Where(shift => shift.AcademicYearId == sourceYearId), cancellationToken);
        foreach (var shift in sourceShifts)
            store.Add(shift.CopyTo(targetYearId));
    }

    public async Task<string?> DeletionBlockAsync(long yearId, CancellationToken cancellationToken) =>
        await HasStructureAsync(yearId, cancellationToken) ? ErrorCodes.YearStructureInUse : null;
}
