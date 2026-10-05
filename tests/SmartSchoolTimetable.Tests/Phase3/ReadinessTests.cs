using System.Diagnostics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Dashboard;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Application.Teachers;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Workload;
using SmartSchoolTimetable.Infrastructure;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase3;

public sealed class ReadinessTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public async Task ReadinessRequiresAuthenticationAndUsesThePersistedWorkload()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host);
        var path = $"{school.Root}/readiness/";
        using var anonymous = host.CreateClientWithoutCookies();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);

        var before = await ReadAsync<ReadinessDto>(await host.Client.GetAsync(path));
        Assert.False(before.Ready);
        Assert.Equal((2, 2, 0), (before.Sections, before.Lines, before.Assigned));
        Assert.Contains(before.Findings, finding => finding.Code == FindingCodes.UnassignedLines && finding.Required == 1);
        Assert.Equal(64, before.InputHash.Length);

        var teacher = await ReadAsync<TeacherDto>(await host.PostAsync("/api/v1/teachers/", new
        {
            fullName = "أحمد علي حسن", shortName = "أحمد", offDays = Array.Empty<int>(), blockedPeriods = Array.Empty<object>(),
            fullyReleased = false, maxLessonsPerDay = (int?)null, maxLessonsPerWeek = (int?)null, version = 0,
        }, school.Token));
        foreach (var section in (await ReadAsync<PagedResult<SectionDto>>(
                     await host.Client.GetAsync($"{school.Root}/stages/{school.Stage.Id}/sections/"))).Items)
        {
            await host.PostAsync($"{school.Root}/workload/bulk/class-teacher", new
            {
                teacherId = teacher.Id, sectionId = section.Id, entryIds = Array.Empty<long>(), overwrite = false,
            }, school.Token);
        }

        var after = await ReadAsync<ReadinessDto>(await host.Client.GetAsync(path));
        Assert.True(after.Ready);
        Assert.Equal((2, 2, 2), (after.Sections, after.Lines, after.Assigned));
        var dashboard = await ReadAsync<DashboardSummaryDto>(await host.Client.GetAsync("/api/v1/dashboard-summary/"));
        Assert.True(dashboard.Checklist.Single(item => item.Key == "workload").Done);
    }

    [Fact]
    public async Task ReadinessAndSuggestionsFollowTheLocalRequestRules()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host);
        var path = $"{school.Root}/readiness/";

        using var foreignOrigin = new HttpRequestMessage(HttpMethod.Get, path);
        foreignOrigin.Headers.TryAddWithoutValidation("Origin", "http://evil.example");
        var refused = await host.Client.SendAsync(foreignOrigin);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.False(refused.Headers.Contains("Access-Control-Allow-Origin"));

        using var foreignHost = new HttpRequestMessage(HttpMethod.Get, path);
        foreignHost.Headers.Host = "127.0.0.1.attacker.example:5080";
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.SendAsync(foreignHost)).StatusCode);

        using var anonymous = host.CreateClientWithoutCookies();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{path}?doublePeriods=true")).StatusCode);

        // A state-changing Phase 3 request without the per-launch token is refused before it runs.
        var withoutToken = await host.PostAsync($"{school.Root}/workload/suggestions/apply", new { confirm = true }, "wrong-token");
        Assert.Equal(HttpStatusCode.Forbidden, withoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task TheDoubleLessonModeTurnsAnImpossibleDoubleIntoAnError()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host); // الأول المتوسط (أ، ب), الرياضيات 5 lessons, 6 a day
        // Only lesson 1 of each day is allowed: no two consecutive lessons, so no double can be placed.
        var blocked = InputFactory.Week.SelectMany(day => Enumerable.Range(2, 5).Select(lesson => new BlockedPeriodDto(day, lesson))).ToArray();
        await ReadAsync<SubjectDto>(await host.PutAsync($"/api/v1/subjects/{school.Subject.Id}", new SaveSubjectCommand(
            school.Subject.Name, school.Subject.ColorIndex, school.Subject.Priority, true, false, false, true, blocked, null, school.Subject.Version), school.Token));
        var path = $"{school.Root}/readiness/";

        var standard = await ReadAsync<ReadinessDto>(await host.Client.GetAsync(path));
        var warning = Assert.Single(standard.Findings, finding => finding.Code == FindingCodes.DoublePeriodImpossible && finding.Related[0].Name.EndsWith('أ'));
        Assert.Equal((2, 0, "warning"), (warning.Required!.Value, warning.Available!.Value, warning.Severity));

        var doubles = await ReadAsync<ReadinessDto>(await host.Client.GetAsync($"{path}?doublePeriods=true"));
        var error = Assert.Single(doubles.Findings, finding => finding.Code == FindingCodes.DoublePeriodImpossible && finding.Related[0].Name.EndsWith('أ'));
        Assert.Equal("error", error.Severity);
        Assert.False(doubles.Ready);
        Assert.Equal(standard.InputHash, doubles.InputHash); // the mode checks the same data
    }

    [Fact]
    public async Task SqliteSnapshotHashAndValidationMeetTheFortySectionBudget()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host);
        await host.PutAsync($"{school.Root}/stage-cards/{school.Stage.Id}/section-count", new { count = 20, shiftId = school.Shift.Id }, school.Token);
        await host.PostAsync($"{school.Root}/templates/stages", new
        {
            schoolType = "intermediate",
            grades = new[] { new { gradeKey = "intermediate-2", sections = 20, shiftId = school.Shift.Id } },
        }, school.Token);
        var stages = (await ReadAsync<PagedResult<StageDto>>(await host.Client.GetAsync($"{school.Root}/stages/"))).Items.OrderBy(stage => stage.Id).ToArray();
        var subjects = new List<SubjectDto> { school.Subject };
        foreach (var index in Enumerable.Range(1, 8))
            subjects.Add(await ReadAsync<SubjectDto>(await host.PostAsync("/api/v1/subjects/", new
            {
                name = $"مادة تجريبية {index}", colorIndex = 0, priority = 0, distributionEnabled = true, spreadAcrossDays = false,
                heavy = false, requiresDoublePeriod = false, blockedPeriods = Array.Empty<object>(), version = 0,
            }, school.Token)));
        foreach (var stage in stages)
        foreach (var subject in subjects.Where(subject => stage.Id != school.Stage.Id || subject.Id != school.Subject.Id))
            await host.PutAsync($"{school.Root}/curriculum/cell", new
            {
                stageId = stage.Id, subjectId = subject.Id, label = (string?)null, weeklyLessons = subject.Id == school.Subject.Id ? 5 : 2,
                entryId = (long?)null, version = (int?)null,
            }, school.Token);

        await host.PostAsync("/api/v1/teachers/bulk", new
        {
            names = Enumerable.Range(1, 40).Select(index => $"معلم تجريبي {index}").ToArray(),
        }, school.Token);
        var teachers = (await ReadAsync<PagedResult<TeacherDto>>(await host.Client.GetAsync("/api/v1/teachers/?pageSize=100"))).Items.OrderBy(teacher => teacher.Id).ToArray();
        using var scope = host.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
        var sectionRows = await database.Set<Section>().AsNoTracking().Where(section => stages.Select(stage => stage.Id).Contains(section.StageId)).OrderBy(section => section.Id).ToArrayAsync();
        var entryRows = await database.Set<CurriculumEntry>().AsNoTracking().Where(entry => stages.Select(stage => stage.Id).Contains(entry.StageId) && !entry.IsArchived).ToArrayAsync();
        for (var index = 0; index < sectionRows.Length; index++)
        foreach (var entry in entryRows.Where(entry => entry.StageId == sectionRows[index].StageId))
            database.Add(WorkloadAssignment.Create(sectionRows[index].Id, entry.Id, teachers[index].Id));
        await database.SaveChangesAsync();

        var store = scope.ServiceProvider.GetRequiredService<IDataStore>();
        async Task<(SchedulingInput Input, string Hash, ValidationReport Report)> CheckAsync()
        {
            var input = await SchedulingInputBuilder.BuildAsync(store, long.Parse(school.Root.Split('/')[4], System.Globalization.CultureInfo.InvariantCulture), default)
                ?? throw new InvalidOperationException("The seeded academic year was not found.");
            return (input, SchedulingInputHash.Compute(input), PreSolveValidator.Validate(input));
        }
        await CheckAsync();

        var timer = Stopwatch.StartNew();
        var (input, hash, report) = await CheckAsync();
        timer.Stop();

        Assert.Equal((40, 18, 360), (input.Sections.Count, input.Lines.Count, input.Assignments.Count));
        Assert.Equal(64, hash.Length);
        Assert.True(report.Ready);
        output.WriteLine($"40 sections × 9 subjects: SQLite snapshot + hash + validation took {timer.ElapsedMilliseconds} ms.");
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(1), $"SQLite snapshot, hash and validation took {timer.ElapsedMilliseconds} ms.");
    }
}
