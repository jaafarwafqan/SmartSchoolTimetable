using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Curriculum;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Application.Teachers;
using SmartSchoolTimetable.Application.Workload;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Workload;
using SmartSchoolTimetable.Infrastructure;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase3;

/// <summary>
/// Phase 3C: workload assignments (one active teacher per section and line), teacher loads and availability, the four
/// previewed bulk actions (preview = applied, idempotent, never overwriting unless chosen) and the protections.
/// </summary>
public sealed class WorkloadTests
{
    private static readonly int[] SundayToThursday = [7, 1, 2, 3, 4];
    private static readonly int[] NoDays = [];
    private static readonly BlockedPeriod[] NoBlocked = [];

    // ---------- Domain ----------

    [Fact]
    public void AssignmentReassignsArchivesAndRestores()
    {
        var assignment = WorkloadAssignment.Create(1, 2, 3);
        Assert.False(assignment.Reassign(3));
        Assert.True(assignment.Reassign(4));
        Assert.Equal((4L, 2), (assignment.TeacherId, assignment.Version));
        assignment.Archive(DateTimeOffset.UnixEpoch);
        assignment.Restore();
        Assert.Equal((false, 4), (assignment.IsArchived, assignment.Version));
        Assert.Equal(3, Assert.Throws<DomainValidationException>(() => WorkloadAssignment.Create(0, 0, 0)).Errors.Count);
    }

    [Fact]
    public void AvailabilityRemovesOffDaysBlockedPeriodsAndAppliesTheLimits()
    {
        // Two sections in the same shift share their slots; a section in the other shift adds its own.
        var morning = TeacherAvailability.SectionSlots(1, SundayToThursday, _ => 6).ToArray();
        var evening = TeacherAvailability.SectionSlots(2, SundayToThursday, day => day == 4 ? 4 : 5).ToArray();
        Assert.Equal(30, TeacherAvailability.Compute(morning.Concat(morning), NoDays, NoBlocked, null, null, false).Available);
        Assert.Equal(54, TeacherAvailability.Compute(morning.Concat(evening), NoDays, NoBlocked, null, null, false).Slots);

        // Off on Thursday (4): 4 days × 6; lesson 1 on Sunday blocked; at most 5 a day; 20 a week.
        var result = TeacherAvailability.Compute(morning, [4], [new BlockedPeriod(7, 1)], 5, 20, false);
        Assert.Equal((23, 20, 20), (result.Slots, result.ByDayLimit, result.Available));
        Assert.Equal(19, TeacherAvailability.Compute(morning, [4], [new BlockedPeriod(7, 1)], 5, 19, false).Available);
        Assert.Equal(0, TeacherAvailability.Compute(morning, NoDays, NoBlocked, null, null, released: true).Available);
        // A blocked lesson number applies to that lesson in every shift (DECISIONS_PENDING #57).
        Assert.Equal(52, TeacherAvailability.Compute(morning.Concat(evening), NoDays, [new BlockedPeriod(7, 2)], null, null, false).Slots);
    }

    // ---------- API ----------

    private sealed record School(ReferenceProtectionTests.School Base, long ArabicId, long ArabicEntryId, List<SectionDto> Sections, TeacherDto Ahmed, TeacherDto Ali)
    {
        public string Root => Base.Root;
        public string Token => Base.Token;
        public string Workload => $"{Base.Root}/workload";
        public long MathsEntryId => Base.EntryId;
    }

    private static async Task<School> SeedAsync(TestHost host)
    {
        var school = await ReferenceProtectionTests.SeedAsync(host); // الأول المتوسط (أ، ب), الرياضيات 5
        var arabic = await ReadAsync<SubjectDto>(await host.PostAsync("/api/v1/subjects/", new
        {
            name = "اللغة العربية", colorIndex = 0, priority = 0, distributionEnabled = true, spreadAcrossDays = false, heavy = false,
            requiresDoublePeriod = false, blockedPeriods = Array.Empty<object>(), notes = (string?)null, version = 0,
        }, school.Token));
        var table = await ReadAsync<CurriculumTableDto>(await host.PutAsync($"{school.Root}/curriculum/cell", new SetCurriculumCellCommand(school.Stage.Id, arabic.Id, null, 6, null, null), school.Token));
        var arabicEntry = table.Rows.Single(row => row.SubjectId == arabic.Id).Cells.Single().EntryId!.Value;
        var sections = (await ReadAsync<PagedResult<SectionDto>>(await host.Client.GetAsync($"{school.Root}/stages/{school.Stage.Id}/sections/"))).Items.ToList();
        var ahmed = await ReadAsync<TeacherDto>(await host.PostAsync("/api/v1/teachers/", new SaveTeacherCommand("أحمد علي حسن", "أحمد", [], [], false, null, null, null, null, 12, null, 0, [school.Subject.Id]), school.Token));
        var ali = await ReadAsync<TeacherDto>(await host.PostAsync("/api/v1/teachers/", new SaveTeacherCommand("علي كاظم جواد", "علي", [], [], false, null, null, null, null, null, null, 0, [arabic.Id]), school.Token));
        return new School(school, arabic.Id, arabicEntry, sections, ahmed, ali);
    }

    private static async Task<WorkloadStageDto> SetCellAsync(TestHost host, School school, long sectionId, long entryId, long? teacherId, WorkloadCellDto? current = null)
    {
        var response = await host.PutAsync($"{school.Workload}/cell", new SetWorkloadCellCommand(sectionId, entryId, teacherId, current?.AssignmentId, current?.Version), school.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<WorkloadStageDto>(response);
    }

    private static WorkloadCellDto Cell(WorkloadStageDto stage, long sectionId, long entryId) =>
        stage.Sections.Single(row => row.SectionId == sectionId).Cells.Single(cell => cell.EntryId == entryId);

    private static async Task<TeacherLoadDto> LoadAsync(TestHost host, School school, long teacherId) =>
        (await ReadAsync<List<TeacherLoadDto>>(await host.Client.GetAsync($"{school.Workload}/teachers"))).Single(load => load.TeacherId == teacherId);

    [Fact]
    public async Task CellsAreAssignedReassignedAndClearedWithVersions()
    {
        await using var host = new TestHost();
        var school = await SeedAsync(host);
        var matrix = await ReadAsync<WorkloadMatrixDto>(await host.Client.GetAsync($"{school.Workload}/matrix"));
        var stage = matrix.Stage!;
        Assert.Equal(["الرياضيات", "اللغة العربية"], stage.Lines.Select(line => line.SubjectName)); // alphabetical, like the curriculum table
        Assert.Equal((0, 4), (matrix.Stages.Single().AssignedCells, matrix.Stages.Single().TotalCells));
        var a = school.Sections[0].Id;

        stage = await SetCellAsync(host, school, a, school.MathsEntryId, school.Ahmed.Id);
        var cell = Cell(stage, a, school.MathsEntryId);
        Assert.Equal((school.Ahmed.Id, false), (cell.TeacherId, cell.OutsideSpecialization));
        Assert.Equal((1, 2, 5, 11), (stage.Sections[0].AssignedLines, stage.Sections[0].TotalLines, stage.Sections[0].AssignedLessons, stage.Sections[0].TotalLessons));

        // An empty cell assigned meanwhile is a conflict; a stale version too.
        await AssertApiErrorAsync(await host.PutAsync($"{school.Workload}/cell", new SetWorkloadCellCommand(a, school.MathsEntryId, school.Ali.Id, null, null), school.Token), ErrorCodes.Conflict);
        await AssertApiErrorAsync(await host.PutAsync($"{school.Workload}/cell", new SetWorkloadCellCommand(a, school.MathsEntryId, school.Ali.Id, cell.AssignmentId, cell.Version + 1), school.Token), ErrorCodes.Conflict);

        // Ali does not teach mathematics: allowed, marked outside the specialization.
        stage = await SetCellAsync(host, school, a, school.MathsEntryId, school.Ali.Id, cell);
        Assert.True(Cell(stage, a, school.MathsEntryId).OutsideSpecialization);
        stage = await SetCellAsync(host, school, a, school.MathsEntryId, null, Cell(stage, a, school.MathsEntryId));
        Assert.Null(Cell(stage, a, school.MathsEntryId).AssignmentId);

        // A line of another stage, an archived teacher and an unknown section are refused.
        Assert.Equal("EntryId", (await AssertApiErrorAsync(await host.PutAsync($"{school.Workload}/cell", new SetWorkloadCellCommand(a, 999, school.Ali.Id, null, null), school.Token), ErrorCodes.ValidationFailed)).Errors.Single().Field);
        Assert.Equal("SectionId", (await AssertApiErrorAsync(await host.PutAsync($"{school.Workload}/cell", new SetWorkloadCellCommand(999, school.MathsEntryId, school.Ali.Id, null, null), school.Token), ErrorCodes.ValidationFailed)).Errors.Single().Field);
        using var anonymous = host.CreateClientWithoutCookies();
        await AssertApiErrorAsync(await anonymous.GetAsync($"{school.Workload}/matrix"), ErrorCodes.Unauthenticated);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PutAsync($"{school.Workload}/cell", new SetWorkloadCellCommand(a, school.MathsEntryId, school.Ali.Id, null, null), "wrong")).StatusCode);
    }

    [Fact]
    public async Task TeacherLoadsShowAssignedLimitAndStatus()
    {
        await using var host = new TestHost();
        var school = await SeedAsync(host);
        foreach (var section in school.Sections)
            await SetCellAsync(host, school, section.Id, school.MathsEntryId, school.Ahmed.Id);
        // 2 sections × 5 = 10 of a 12-lesson limit: «قريب» (90% or more is 11), within at 10.
        var ahmed = await LoadAsync(host, school, school.Ahmed.Id);
        Assert.Equal((10, 12, 30, 12, "within"), (ahmed.AssignedLessons, ahmed.MaxPerWeek!.Value, ahmed.Available, ahmed.Limit, ahmed.Status));
        Assert.Equal(["أ", "ب"], ahmed.Assignments.Select(item => item.SectionLabel));

        // Mathematics in ب is given up and Arabic taken instead: 5 + 6 = 11 of 12 is «قريب».
        var matrix = (await ReadAsync<WorkloadMatrixDto>(await host.Client.GetAsync($"{school.Workload}/matrix"))).Stage!;
        await SetCellAsync(host, school, school.Sections[1].Id, school.MathsEntryId, null, Cell(matrix, school.Sections[1].Id, school.MathsEntryId));
        await SetCellAsync(host, school, school.Sections[1].Id, school.ArabicEntryId, school.Ahmed.Id);
        Assert.Equal((11, "near"), ((await LoadAsync(host, school, school.Ahmed.Id)).AssignedLessons, (await LoadAsync(host, school, school.Ahmed.Id)).Status));

        await SetCellAsync(host, school, school.Sections[0].Id, school.ArabicEntryId, school.Ahmed.Id);
        ahmed = await LoadAsync(host, school, school.Ahmed.Id);
        Assert.Equal((17, "over"), (ahmed.AssignedLessons, ahmed.Status));
        Assert.True(ahmed.Assignments.First(item => item.EntryId == school.ArabicEntryId).OutsideSpecialization);
        Assert.Equal("within", (await LoadAsync(host, school, school.Ali.Id)).Status);
    }

    [Fact]
    public async Task BulkActionsPreviewExactlyWhatTheyApplyAndNeverOverwriteUnlessChosen()
    {
        await using var host = new TestHost();
        var school = await SeedAsync(host);
        var (a, b) = (school.Sections[0].Id, school.Sections[1].Id);
        await SetCellAsync(host, school, b, school.MathsEntryId, school.Ali.Id);

        // Across the stage without overwrite: أ is created, ب (Ali) is skipped.
        var command = new AssignAcrossStageCommand(school.Ahmed.Id, school.MathsEntryId, Overwrite: false);
        var preview = await ReadAsync<WorkloadPlanDto>(await host.PostAsync($"{school.Workload}/bulk/across-stage/preview", command, school.Token));
        Assert.Equal([("أ", "create"), ("ب", "skip")], preview.Lines.Select(line => (line.SectionLabel, line.Action)));
        Assert.Equal((1, 0, 5), (preview.Changes, preview.Loads.Single().Before, preview.Loads.Single().After));
        var applied = await ReadAsync<WorkloadPlanDto>(await host.PostAsync($"{school.Workload}/bulk/across-stage", command, school.Token));
        Assert.Equal(preview.Lines, applied.Lines);
        var again = await ReadAsync<WorkloadPlanDto>(await host.PostAsync($"{school.Workload}/bulk/across-stage", command, school.Token));
        Assert.Equal((0, "unchanged"), (again.Changes, again.Lines[0].Action)); // idempotent

        // With overwrite, ب is replaced; Ali's load drops from 5 to 0.
        preview = await ReadAsync<WorkloadPlanDto>(await host.PostAsync($"{school.Workload}/bulk/across-stage/preview", command with { Overwrite = true }, school.Token));
        Assert.Equal(("replace", "علي كاظم جواد", "أحمد علي حسن"), (preview.Lines[1].Action, preview.Lines[1].CurrentTeacher, preview.Lines[1].NewTeacher));
        Assert.Contains(preview.Loads, load => load.TeacherId == school.Ali.Id && load is { Before: 5, After: 0 });
        await ReadAsync<WorkloadPlanDto>(await host.PostAsync($"{school.Workload}/bulk/across-stage", command with { Overwrite = true }, school.Token));

        // Class teacher for ب: both lines; mathematics is already Ahmed's.
        var classTeacher = await ReadAsync<WorkloadPlanDto>(await host.PostAsync($"{school.Workload}/bulk/class-teacher", new ClassTeacherCommand(school.Ahmed.Id, b, null, false), school.Token));
        Assert.Equal(["unchanged", "create"], classTeacher.Lines.Select(line => line.Action));
        Assert.Equal(16, (await LoadAsync(host, school, school.Ahmed.Id)).AssignedLessons); // mathematics in أ and ب, Arabic in ب
    }

    [Fact]
    public async Task TransferAndRemoveMoveOrArchiveEveryAssignmentOfTheTeacher()
    {
        await using var host = new TestHost();
        var school = await SeedAsync(host);
        await ReadAsync<WorkloadPlanDto>(await host.PostAsync($"{school.Workload}/bulk/class-teacher", new ClassTeacherCommand(school.Ahmed.Id, school.Sections[0].Id, null, false), school.Token));
        Assert.Equal(11, (await LoadAsync(host, school, school.Ahmed.Id)).AssignedLessons);

        var transfer = new TransferWorkloadCommand(school.Ahmed.Id, school.Ali.Id);
        var preview = await ReadAsync<WorkloadPlanDto>(await host.PostAsync($"{school.Workload}/bulk/transfer/preview", transfer, school.Token));
        Assert.Equal((2, "transfer"), (preview.Changes, preview.Lines[0].Action));
        Assert.Contains(preview.Loads, load => load.TeacherId == school.Ali.Id && load is { Before: 0, After: 11 });
        await ReadAsync<WorkloadPlanDto>(await host.PostAsync($"{school.Workload}/bulk/transfer", transfer, school.Token));
        Assert.Equal((0, 11), ((await LoadAsync(host, school, school.Ahmed.Id)).AssignedLessons, (await LoadAsync(host, school, school.Ali.Id)).AssignedLessons));
        Assert.Equal("ToTeacherId", (await AssertApiErrorAsync(await host.PostAsync($"{school.Workload}/bulk/transfer/preview", new TransferWorkloadCommand(school.Ali.Id, school.Ali.Id), school.Token), ErrorCodes.ValidationFailed)).Errors.Single().Field);

        var removed = await ReadAsync<WorkloadPlanDto>(await host.PostAsync($"{school.Workload}/bulk/remove", new RemoveWorkloadCommand(school.Ali.Id), school.Token));
        Assert.Equal(2, removed.Changes);
        Assert.Empty((await LoadAsync(host, school, school.Ali.Id)).Assignments);
        Assert.Equal(0, (await ReadAsync<WorkloadPlanDto>(await host.PostAsync($"{school.Workload}/bulk/remove", new RemoveWorkloadCommand(school.Ali.Id), school.Token))).Changes);
    }

    [Fact]
    public async Task AssignedRecordsAreProtectedAndClearingALineAsksFirst()
    {
        await using var host = new TestHost();
        var school = await SeedAsync(host);
        var b = school.Sections[1];
        await SetCellAsync(host, school, b.Id, school.MathsEntryId, school.Ahmed.Id);

        // Teacher and section: archive and delete refused with WORKLOAD_IN_USE; the report names the assignment.
        var report = await ReferenceProtectionTests.ReferencesAsync(host, ReferenceKinds.Teacher, school.Ahmed.Id);
        Assert.Equal((DependentKinds.WorkloadAssignment, "الأول المتوسط / ب: الرياضيات — أحمد علي حسن"), (report.Dependents.Single().Kind, report.Dependents.Single().Samples.Single()));
        await AssertApiErrorAsync(await host.PostAsync($"/api/v1/teachers/{school.Ahmed.Id}/archive", new { version = school.Ahmed.Version }, school.Token), ErrorCodes.WorkloadInUse);
        await AssertApiErrorAsync(await host.SendJsonAsync(HttpMethod.Delete, $"/api/v1/teachers/{school.Ahmed.Id}?version={school.Ahmed.Version}", new { }, school.Token), ErrorCodes.WorkloadInUse);
        var sectionPath = $"{school.Root}/stages/{school.Base.Stage.Id}/sections/{b.Id}";
        await AssertApiErrorAsync(await host.PostAsync($"{sectionPath}/archive", new { version = b.Version }, school.Token), ErrorCodes.WorkloadInUse);
        await AssertApiErrorAsync(await host.SendJsonAsync(HttpMethod.Delete, $"{sectionPath}?version={b.Version}", new { }, school.Token), ErrorCodes.WorkloadInUse);
        // The stepper never removes a section with workload (ب is the last one).
        await AssertApiErrorAsync(await host.PutAsync($"{school.Root}/stage-cards/{school.Base.Stage.Id}/section-count", new { count = 1, shiftId = school.Base.Shift.Id }, school.Token), ErrorCodes.WorkloadInUse);

        // Clearing the line: refused without confirmation; confirmed, it archives the line and its assignment together.
        var clear = new SetCurriculumCellCommand(school.Base.Stage.Id, school.Base.Subject.Id, null, null, school.MathsEntryId, school.Base.EntryVersion);
        await AssertApiErrorAsync(await host.PutAsync($"{school.Root}/curriculum/cell", clear, school.Token), ErrorCodes.WorkloadInUse);
        var cleared = (await ReadAsync<CurriculumTableDto>(await host.PutAsync($"{school.Root}/curriculum/cell", clear with { ConfirmWorkload = true }, school.Token))).Cleared!;
        Assert.Equal(0, (await LoadAsync(host, school, school.Ahmed.Id)).AssignedLessons);
        // Undo brings the assignment back with the line.
        await ReadAsync<CurriculumEntryDto>(await host.PostAsync($"/api/v1/curriculum-entries/{cleared.Id}/restore", new { version = cleared.Version }, school.Token));
        Assert.Equal(5, (await LoadAsync(host, school, school.Ahmed.Id)).AssignedLessons);

        // Archiving the line through its own endpoint follows the same rule.
        var entry = (await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{school.Root}/curriculum"))).Rows.Single(row => row.SubjectId == school.Base.Subject.Id).Cells.Single();
        await AssertApiErrorAsync(await host.PostAsync($"/api/v1/curriculum-entries/{entry.EntryId}/archive", new { version = entry.Version }, school.Token), ErrorCodes.WorkloadInUse);
        await ReadAsync<CurriculumEntryDto>(await host.PostAsync($"/api/v1/curriculum-entries/{entry.EntryId}/archive", new ArchiveEntryCommand(entry.Version!.Value, true), school.Token));
        Assert.Equal(0, (await LoadAsync(host, school, school.Ahmed.Id)).AssignedLessons);
    }

    [Fact]
    public async Task TheDatabaseAllowsOneActiveAssignmentPerSectionAndLine()
    {
        await using var host = new TestHost();
        var school = await SeedAsync(host);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
        var section = school.Sections[0].Id;
        db.Add(WorkloadAssignment.Create(section, school.MathsEntryId, school.Ahmed.Id));
        await db.SaveChangesAsync();
        var archived = WorkloadAssignment.Create(section, school.MathsEntryId, school.Ali.Id);
        archived.Archive(DateTimeOffset.UnixEpoch);
        db.Add(archived); // archived rows are history and do not count
        await db.SaveChangesAsync();
        db.Add(WorkloadAssignment.Create(section, school.MathsEntryId, school.Ali.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
