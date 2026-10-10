using System.Net;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Workload;
using SmartSchoolTimetable.Tests.Phase4;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;
using static SmartSchoolTimetable.Tests.Phase4.GenerationApiTests;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>MF2: the flat «الأنصبة» rows (every section × curriculum line of the year with its teacher) behind the wizard's table.</summary>
public sealed class WorkloadRowsTests
{
    [Fact]
    public async Task EveryCellIsARowWithItsTeacherAndClearingOneShowsItWithoutATeacher()
    {
        await using var host = new TestHost();
        var (school, _) = await ReadySchoolAsync(host);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClientWithoutCookies().GetAsync($"{school.Root}/workload/rows")).StatusCode);

        var rows = await ReadAsync<List<WorkloadRowDto>>(await host.Client.GetAsync($"{school.Root}/workload/rows"));
        Assert.Equal(2, rows.Count); // two sections × one line
        Assert.All(rows, row => Assert.Equal((school.Stage.Id, school.EntryId, "الرياضيات", 5), (row.StageId, row.EntryId, row.SubjectName, row.WeeklyLessons)));
        Assert.All(rows, row => Assert.NotNull(row.TeacherId));
        Assert.Equal(["أ", "ب"], rows.Select(row => row.SectionLabel).Order(StringComparer.Ordinal));

        var first = rows[0];
        var cleared = await host.PutAsync($"{school.Root}/workload/cell",
            new { sectionId = first.SectionId, entryId = first.EntryId, teacherId = (long?)null, assignmentId = first.AssignmentId, version = first.Version }, school.Token);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        rows = await ReadAsync<List<WorkloadRowDto>>(await host.Client.GetAsync($"{school.Root}/workload/rows"));
        Assert.Equal(1, rows.Count(row => row.TeacherId is null));
        await AssertApiErrorAsync(await host.Client.GetAsync("/api/v1/academic-years/999999/workload/rows"), ErrorCodes.NotFound);
    }
}
