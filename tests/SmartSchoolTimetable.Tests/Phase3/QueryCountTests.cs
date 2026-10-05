using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Curriculum;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Subjects;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase3;

/// <summary>
/// No N+1 on the main list endpoints (Phase 3 §5.6, extended in 3B/3C): each runs the same number of SQL commands for a small school
/// and for one with several times more stages, sections, subjects, teachers and curriculum lines.
/// </summary>
public sealed class QueryCountTests
{
    private static readonly string[] FirstTeacher = ["علي حسن كاظم"];
    private static readonly string[] SubjectNames = ["اللغة العربية", "اللغة الإنكليزية", "العلوم", "الاجتماعيات", "التربية الإسلامية", "الحاسوب", "التربية الفنية", "التربية الرياضية"];

    private static async Task<Dictionary<string, int>> MeasureAsync(TestHost host, IEnumerable<string> paths)
    {
        var counts = new Dictionary<string, int>();
        foreach (var path in paths)
        {
            await host.Client.GetAsync(path); // warm-up: the first call may load per-scope caches
            host.Queries.Reset();
            var response = await host.Client.GetAsync(path);
            Assert.True(response.IsSuccessStatusCode, path);
            counts[path] = host.Queries.Count;
        }
        return counts;
    }

    [Fact]
    public async Task ListEndpointsRunAConstantNumberOfQueries()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host);
        var paths = new[]
        {
            $"{school.Root}/stages/", $"{school.Root}/stages/{school.Stage.Id}/sections/", $"{school.Root}/shifts/",
            "/api/v1/subjects/", "/api/v1/teachers/", $"{school.Root}/stage-cards", $"{school.Root}/curriculum",
            "/api/v1/dashboard-summary/", $"/api/v1/references/stage/{school.Stage.Id}", "/api/v1/blocked-periods/orphans/",
            "/api/v1/resources/", $"{school.Root}/workload/matrix", $"{school.Root}/workload/teachers",
            $"{school.Root}/readiness/",
        };
        await host.PostAsync("/api/v1/teachers/bulk", new { names = FirstTeacher }, school.Token);
        var small = await MeasureAsync(host, paths);

        // Grow every list several times over.
        await host.PostAsync($"{school.Root}/templates/stages", new
        {
            schoolType = "intermediate",
            grades = new[] { new { gradeKey = "intermediate-2", sections = 6, shiftId = school.Shift.Id }, new { gradeKey = "intermediate-3", sections = 6, shiftId = school.Shift.Id } },
        }, token: school.Token);
        await ReadAsync<StageCardDto>(await host.PutAsync($"{school.Root}/stage-cards/{school.Stage.Id}/section-count", new { count = 6, shiftId = school.Shift.Id }, school.Token));
        var stages = (await ReadAsync<PagedResult<StageDto>>(await host.Client.GetAsync($"{school.Root}/stages/"))).Items;
        foreach (var name in SubjectNames)
        {
            var subject = await ReadAsync<SubjectDto>(await host.PostAsync("/api/v1/subjects/", new
            {
                name, colorIndex = 0, priority = 0, distributionEnabled = true, spreadAcrossDays = false, heavy = false,
                requiresDoublePeriod = false, blockedPeriods = Array.Empty<object>(), notes = (string?)null, version = 0,
            }, school.Token));
            foreach (var stage in stages)
                await ReadAsync<CurriculumTableDto>(await host.PutAsync($"{school.Root}/curriculum/cell", new SetCurriculumCellCommand(stage.Id, subject.Id, null, 2, null, null), school.Token));
        }
        await host.PostAsync("/api/v1/teachers/bulk", new { names = Enumerable.Range(1, 12).Select(index => $"معلم تجريبي {index}").ToArray() }, school.Token);
        Assert.Equal(18, (await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{school.Root}/stage-cards"))).Sum(card => card.Sections.Count));

        Assert.Equal(13, (await ReadAsync<PagedResult<object>>(await host.Client.GetAsync("/api/v1/teachers/"))).Total);
        // Every section of the first stage gets a class teacher, so the workload views have rows to load.
        var teacherId = (await ReadAsync<PagedResult<Application.Teachers.TeacherDto>>(await host.Client.GetAsync("/api/v1/teachers/"))).Items[0].Id;
        foreach (var section in (await ReadAsync<PagedResult<SectionDto>>(await host.Client.GetAsync($"{school.Root}/stages/{school.Stage.Id}/sections/"))).Items)
            await host.PostAsync($"{school.Root}/workload/bulk/class-teacher", new { teacherId, sectionId = section.Id, entryIds = Array.Empty<long>(), overwrite = false }, school.Token);

        var large = await MeasureAsync(host, paths);
        Assert.All(small.Values, count => Assert.InRange(count, 1, 20));
        Assert.Equal(small, large);
    }
}
