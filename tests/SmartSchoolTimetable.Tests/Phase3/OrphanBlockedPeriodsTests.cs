using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Application.Teachers;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase3;

/// <summary>
/// Orphan blocked periods (Phase 3 §5.5): after lessons per day shrink, blocked slots beyond the new grid are
/// reported, kept until the owner confirms, then removed from exactly the confirmed records (versions checked).
/// </summary>
public sealed class OrphanBlockedPeriodsTests
{
    private const string OrphansPath = "/api/v1/blocked-periods/orphans";
    private static readonly int[] SundayToThursday = [7, 1, 2, 3, 4];

    private static async Task<TeacherDto> TeacherAsync(TestHost host, long id) =>
        (await ReadAsync<PagedResult<TeacherDto>>(await host.Client.GetAsync("/api/v1/teachers/?pageSize=100"))).Items.Single(row => row.Id == id);

    [Fact]
    public async Task ReducingLessonsPerDayReportsOrphansAndCleansOnlyAfterConfirmation()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host); // 6 lessons a day, Sunday–Thursday
        var teacher = await ReadAsync<TeacherDto>(await host.PostAsync("/api/v1/teachers/", new SaveTeacherCommand(
            "أحمد علي حسن", "أحمد", [], [new BlockedPeriodDto(7, 1), new BlockedPeriodDto(7, 6), new BlockedPeriodDto(1, 5)],
            false, null, null, null, null, null, null, 0), school.Token));
        var subjectResponse = await host.PutAsync($"/api/v1/subjects/{school.Subject.Id}", new
        {
            name = school.Subject.Name, colorIndex = school.Subject.ColorIndex, priority = school.Subject.Priority, distributionEnabled = true, spreadAcrossDays = false, heavy = false,
            requiresDoublePeriod = false, blockedPeriods = new[] { new BlockedPeriodDto(4, 6) }, notes = (string?)null, version = school.Subject.Version,
        }, school.Token);
        var subject = await ReadAsync<SubjectDto>(subjectResponse);
        Assert.Empty((await ReadAsync<OrphanBlockedPeriodsDto>(await host.Client.GetAsync($"{OrphansPath}/"))).Owners);

        // Four lessons on every day: lessons 5 and 6 no longer exist.
        var days = SundayToThursday.Select(day => new DayLessonsDto(day, 4)).ToArray();
        await ReadAsync<ShiftDto>(await host.PutAsync($"{school.Root}/shifts/{school.Shift.Id}/day-lessons", new SetDayLessonsCommand(days, school.Shift.Version, true), school.Token));

        var report = await ReadAsync<OrphanBlockedPeriodsDto>(await host.Client.GetAsync($"{OrphansPath}/"));
        Assert.Equal(3, report.Total);
        Assert.Equal([("teacher", teacher.Id, 2), ("subject", subject.Id, 1)], report.Owners.Select(owner => (owner.Kind, owner.Id, owner.Periods.Count)));
        Assert.Equal([new BlockedPeriodDto(1, 5), new BlockedPeriodDto(7, 6)], report.Owners[0].Periods);
        // Nothing was removed by the change itself.
        Assert.Equal(3, (await TeacherAsync(host, teacher.Id)).BlockedPeriods.Count);

        // A stale version is refused; confirming only the teacher leaves the subject's orphan in place.
        await AssertApiErrorAsync(await host.PostAsync($"{OrphansPath}/clean", new CleanOrphanBlockedPeriodsCommand([new("teacher", teacher.Id, teacher.Version + 5)]), school.Token), ErrorCodes.Conflict);
        var after = await ReadAsync<OrphanBlockedPeriodsDto>(await host.PostAsync($"{OrphansPath}/clean", new CleanOrphanBlockedPeriodsCommand([new("teacher", teacher.Id, report.Owners[0].Version)]), school.Token));
        Assert.Equal(("subject", 1), (after.Owners.Single().Kind, after.Total));
        var cleaned = await TeacherAsync(host, teacher.Id);
        Assert.Equal([new BlockedPeriodDto(7, 1)], cleaned.BlockedPeriods);
        Assert.Equal(teacher.Version + 1, cleaned.Version);
        await AssertApiErrorAsync(await host.PostAsync($"{OrphansPath}/clean", new CleanOrphanBlockedPeriodsCommand([new("subject", 999, 1)]), school.Token), ErrorCodes.NotFound);
    }
}
