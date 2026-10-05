using System.Net;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Teachers;
using SmartSchoolTimetable.Application.Workload;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase3;

public sealed class AssignmentSuggesterTests
{
    private static async Task<TeacherDto> AddTeacherAsync(TestHost host, ReferenceProtectionTests.School school, string fullName, string shortName)
    {
        return await ReadAsync<TeacherDto>(await host.PostAsync("/api/v1/teachers/", new
        {
            fullName, shortName, offDays = Array.Empty<int>(), blockedPeriods = Array.Empty<object>(), fullyReleased = false,
            maxLessonsPerDay = (int?)null, maxLessonsPerWeek = 12, notes = (string?)null, version = 0,
            specializationIds = new[] { school.Subject.Id },
        }, school.Token));
    }

    [Fact]
    public async Task PreviewIsDeterministicBalancedWithinLimitsAndEqualsTheConfirmedApply()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host);
        var firstTeacher = await AddTeacherAsync(host, school, "أحمد علي حسن", "أحمد");
        var secondTeacher = await AddTeacherAsync(host, school, "سعد كاظم حسن", "سعد");
        var path = $"{school.Root}/workload/suggestions/preview";
        using var anonymous = host.CreateClientWithoutCookies();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);

        var preview = await ReadAsync<AssignmentSuggestionPlanDto>(await host.Client.GetAsync(path));
        var repeated = await ReadAsync<AssignmentSuggestionPlanDto>(await host.Client.GetAsync(path));
        Assert.Equal(preview.Assignments, repeated.Assignments);
        Assert.Equal(2, preview.Assignments.Count);
        Assert.Empty(preview.Unassigned);
        Assert.All(preview.Loads, load => Assert.Equal((0, 5, 12), (load.Before, load.After, load.Limit)));
        Assert.Equal(new[] { firstTeacher.Id, secondTeacher.Id }.Order(), preview.Assignments.Select(item => item.TeacherId).Order());

        var unconfirmed = await AssertApiErrorAsync(await host.PostAsync($"{school.Root}/workload/suggestions/apply", new { confirm = false }, school.Token), ErrorCodes.ValidationFailed);
        Assert.Equal(("Confirm", ErrorCodes.Required), (unconfirmed.Errors.Single().Field, unconfirmed.Errors.Single().Code));
        var applied = await ReadAsync<AssignmentSuggestionPlanDto>(await host.PostAsync($"{school.Root}/workload/suggestions/apply", new { confirm = true }, school.Token));
        Assert.Equal(preview.Assignments, applied.Assignments);
        var after = await ReadAsync<AssignmentSuggestionPlanDto>(await host.Client.GetAsync(path));
        Assert.Empty(after.Assignments);
        Assert.All(after.Unassigned, line => Assert.Equal("NO_SPECIALIST", line.Reason));
    }

    [Fact]
    public async Task ExistingAssignmentsAreNeverChangedByTheSuggester()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host);
        var teacher = await AddTeacherAsync(host, school, "أحمد علي حسن", "أحمد");
        var sections = (await ReadAsync<PagedResult<SmartSchoolTimetable.Application.Stages.SectionDto>>(
            await host.Client.GetAsync($"{school.Root}/stages/{school.Stage.Id}/sections/"))).Items.OrderBy(section => section.Id).ToArray();
        await host.PutAsync($"{school.Root}/workload/cell", new SetWorkloadCellCommand(sections[0].Id, school.EntryId, teacher.Id, null, null), school.Token);

        var preview = await ReadAsync<AssignmentSuggestionPlanDto>(await host.Client.GetAsync($"{school.Root}/workload/suggestions/preview"));
        Assert.Single(preview.Assignments);
        Assert.Equal(sections[1].Id, preview.Assignments[0].SectionId);
        await ReadAsync<AssignmentSuggestionPlanDto>(await host.PostAsync($"{school.Root}/workload/suggestions/apply", new { confirm = true }, school.Token));
        var matrix = await ReadAsync<WorkloadMatrixDto>(await host.Client.GetAsync($"{school.Root}/workload/matrix"));
        Assert.Equal(teacher.Id, matrix.Stage!.Sections.Single(section => section.SectionId == sections[0].Id).Cells.Single().TeacherId);
        Assert.Equal(teacher.Id, matrix.Stage.Sections.Single(section => section.SectionId == sections[1].Id).Cells.Single().TeacherId);
    }
}