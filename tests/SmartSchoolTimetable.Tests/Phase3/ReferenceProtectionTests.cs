using System.Net;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Curriculum;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Subjects;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase3;

/// <summary>
/// Reference protection (Phase 3 §5.3): one guard answers what depends on a record; every delete and archive path
/// uses it, and the preview endpoint returns the same answer (dependent kinds, counts, names, blocking code).
/// </summary>
public sealed class ReferenceProtectionTests
{
    private static readonly int[] SundayToThursday = [7, 1, 2, 3, 4];
    private static readonly string[] StageSections = ["الأول المتوسط / أ", "الأول المتوسط / ب"];
    private static readonly string[] StageLines = ["الأول المتوسط / الرياضيات"];

    internal sealed record Report(string Kind, long Id, List<DependentGroupDto> Dependents, string? ArchiveBlockedBy, string? DeleteBlockedBy);

    internal sealed record School(string Token, string Root, ShiftDto Shift, StageDto Stage, SubjectDto Subject, long EntryId, int EntryVersion);

    internal static async Task<School> SeedAsync(TestHost host)
    {
        var (token, _) = await SetupOwnerAsync(host);
        await host.PutAsync("/api/v1/setup-wizard/school", new { name = "متوسطة الرافدين", schoolType = "intermediate", shiftMode = "single" }, token);
        await host.PutAsync("/api/v1/setup-wizard/year", new { label = "2026-2027", startDate = "2026-09-01", endDate = "2027-06-30", terms = Array.Empty<object>() }, token);
        var shiftsInput = new[] { new { kind = "morning", firstStartTime = "08:00", lessonMinutes = 40, lessonCount = 6, breaks = Array.Empty<object>(), dayLessons = Array.Empty<object>() } };
        await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-wizard/timing", new { days = SundayToThursday, weekStartDay = 7, shifts = shiftsInput }, token));
        var year = (await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync("/api/v1/academic-years/"))).Items.Single();
        var root = $"/api/v1/academic-years/{year.Id}";
        var shift = (await ReadAsync<PagedResult<ShiftDto>>(await host.Client.GetAsync($"{root}/shifts/"))).Items.Single();
        await host.PostAsync($"{root}/templates/stages", new { schoolType = "intermediate", grades = new[] { new { gradeKey = "intermediate-1", sections = 2, shiftId = shift.Id } } }, token);
        var stage = (await ReadAsync<PagedResult<StageDto>>(await host.Client.GetAsync($"{root}/stages/"))).Items.Single();
        var subject = await ReadAsync<SubjectDto>(await host.PostAsync("/api/v1/subjects/", new
        {
            name = "الرياضيات", colorIndex = 0, priority = 0, distributionEnabled = true, spreadAcrossDays = false, heavy = false,
            requiresDoublePeriod = false, blockedPeriods = Array.Empty<object>(), notes = (string?)null, version = 0,
        }, token));
        var table = await ReadAsync<CurriculumTableDto>(await host.PutAsync($"{root}/curriculum/cell", new SetCurriculumCellCommand(stage.Id, subject.Id, null, 5, null, null), token));
        var cell = table.Rows.Single(row => row.SubjectId == subject.Id).Cells.Single();
        return new School(token, root, shift, stage, subject, cell.EntryId!.Value, cell.Version!.Value);
    }

    internal static async Task<Report> ReferencesAsync(TestHost host, string kind, long id)
    {
        var response = await host.Client.GetAsync($"/api/v1/references/{kind}/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<Report>(response);
    }

    private static Task<HttpResponseMessage> DeleteAsync(TestHost host, string path, int version, string token) =>
        host.SendJsonAsync(HttpMethod.Delete, $"{path}?version={version}", new { }, token);

    [Fact]
    public async Task StageWithSectionsAndCurriculumCannotBeArchivedOrDeleted()
    {
        await using var host = new TestHost();
        var school = await SeedAsync(host);
        var report = await ReferencesAsync(host, ReferenceKinds.Stage, school.Stage.Id);
        Assert.Equal([(DependentKinds.Section, 2, 0), (DependentKinds.CurriculumEntry, 1, 0)], report.Dependents.Select(group => (group.Kind, group.Active, group.Archived)));
        Assert.Equal(StageSections, report.Dependents[0].Samples);
        Assert.Equal(StageLines, report.Dependents[1].Samples);
        Assert.Equal((ErrorCodes.RecordInUse, ErrorCodes.RecordInUse), (report.ArchiveBlockedBy, report.DeleteBlockedBy));

        var stagePath = $"{school.Root}/stages/{school.Stage.Id}";
        await AssertApiErrorAsync(await host.PostAsync($"{stagePath}/archive", new { version = school.Stage.Version }, school.Token), ErrorCodes.RecordInUse);
        await AssertApiErrorAsync(await DeleteAsync(host, stagePath, school.Stage.Version, school.Token), ErrorCodes.RecordInUse);
    }

    [Fact]
    public async Task SubjectInTheCurriculumIsProtectedAndArchivedLinesStillBlockDelete()
    {
        await using var host = new TestHost();
        var school = await SeedAsync(host);
        var subjectPath = $"/api/v1/subjects/{school.Subject.Id}";
        var report = await ReferencesAsync(host, ReferenceKinds.Subject, school.Subject.Id);
        Assert.Equal((ErrorCodes.CurriculumInUse, ErrorCodes.CurriculumInUse), (report.ArchiveBlockedBy, report.DeleteBlockedBy));
        await AssertApiErrorAsync(await host.PostAsync($"{subjectPath}/archive", new { version = school.Subject.Version }, school.Token), ErrorCodes.CurriculumInUse);
        await AssertApiErrorAsync(await DeleteAsync(host, subjectPath, school.Subject.Version, school.Token), ErrorCodes.CurriculumInUse);

        // An archived line no longer blocks archiving the subject, but it keeps history, so delete stays blocked.
        await ReadAsync<CurriculumEntryDto>(await host.PostAsync($"/api/v1/curriculum-entries/{school.EntryId}/archive", new { version = school.EntryVersion }, school.Token));
        report = await ReferencesAsync(host, ReferenceKinds.Subject, school.Subject.Id);
        Assert.Equal((0, 1), (report.Dependents.Single().Active, report.Dependents.Single().Archived));
        Assert.Equal((null, ErrorCodes.CurriculumInUse), (report.ArchiveBlockedBy, report.DeleteBlockedBy));
        var archived = await ReadAsync<SubjectDto>(await host.PostAsync($"{subjectPath}/archive", new { version = school.Subject.Version }, school.Token));
        Assert.True(archived.IsArchived);
        await AssertApiErrorAsync(await DeleteAsync(host, subjectPath, archived.Version, school.Token), ErrorCodes.CurriculumInUse);
    }

    [Fact]
    public async Task ShiftUsedBySectionsCannotBeDeleted()
    {
        await using var host = new TestHost();
        var school = await SeedAsync(host);
        var report = await ReferencesAsync(host, ReferenceKinds.Shift, school.Shift.Id);
        Assert.Equal((DependentKinds.Section, 2), (report.Dependents.Single().Kind, report.Dependents.Single().Active));
        await AssertApiErrorAsync(await DeleteAsync(host, $"{school.Root}/shifts/{school.Shift.Id}", school.Shift.Version, school.Token), ErrorCodes.RecordInUse);
    }

    [Fact]
    public async Task UnreferencedKindsReportNothingAndUnknownKindsAreNotFound()
    {
        await using var host = new TestHost();
        var school = await SeedAsync(host);
        var section = (await ReadAsync<PagedResult<SectionDto>>(await host.Client.GetAsync($"{school.Root}/stages/{school.Stage.Id}/sections/"))).Items[0];
        foreach (var (kind, id) in new[] { (ReferenceKinds.Section, section.Id), (ReferenceKinds.CurriculumEntry, school.EntryId), (ReferenceKinds.Teacher, 1L), (ReferenceKinds.Resource, 1L) })
        {
            var report = await ReferencesAsync(host, kind, id);
            Assert.Empty(report.Dependents);
            Assert.Null(report.DeleteBlockedBy);
        }
        await AssertApiErrorAsync(await host.Client.GetAsync("/api/v1/references/planet/1"), ErrorCodes.NotFound);

        // Unreferenced records go through the same guard and are deleted normally.
        var sectionPath = $"{school.Root}/stages/{school.Stage.Id}/sections/{section.Id}";
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(host, sectionPath, section.Version, school.Token)).StatusCode);
    }

    [Fact]
    public async Task ReferencePreviewRequiresTheOwnerSession()
    {
        await using var host = new TestHost();
        await SetupOwnerAsync(host);
        using var anonymous = host.CreateClientWithoutCookies();
        await AssertApiErrorAsync(await anonymous.GetAsync("/api/v1/references/subject/1"), ErrorCodes.Unauthenticated);
    }
}
