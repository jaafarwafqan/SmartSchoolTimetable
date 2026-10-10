using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Curriculum;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase3;

/// <summary>
/// Clearing a curriculum cell is a soft delete (Phase 3 §5.4): the line is archived and returned for the undo
/// notice; restoring it brings the cell back with its value; the cleared line stays in history.
/// </summary>
public sealed class CurriculumClearUndoTests
{
    [Fact]
    public async Task ClearingACellArchivesTheLineAndUndoRestoresIt()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host);
        var clear = new SetCurriculumCellCommand(school.Stage.Id, school.Subject.Id, null, null, school.EntryId, school.EntryVersion);
        var table = await ReadAsync<CurriculumTableDto>(await host.PutAsync($"{school.Root}/curriculum/cell", clear, school.Token));

        var cleared = Assert.IsType<CurriculumEntryDto>(table.Cleared);
        Assert.Equal((school.EntryId, 5, true), (cleared.Id, cleared.WeeklyLessons, cleared.IsArchived));
        Assert.Null(table.Rows.Single(row => row.SubjectId == school.Subject.Id).Cells.Single().WeeklyLessons);
        Assert.Equal(0, table.Stages.Single().PlannedLessons);
        // The line is kept (archived), so it still appears in the subject's references as history.
        var report = await ReferenceProtectionTests.ReferencesAsync(host, ReferenceKinds.Subject, school.Subject.Id);
        Assert.Equal((0, 1), (report.Dependents.Single().Active, report.Dependents.Single().Archived));

        // Undo: restore the line with the version from the response.
        var restored = await ReadAsync<CurriculumEntryDto>(await host.PostAsync($"/api/v1/curriculum-entries/{cleared.Id}/restore", new { version = cleared.Version }, school.Token));
        Assert.False(restored.IsArchived);
        table = await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{school.Root}/curriculum"));
        Assert.Equal(5, table.Rows.Single(row => row.SubjectId == school.Subject.Id).Cells.Single().WeeklyLessons);
        Assert.Null(table.Cleared);
    }
}
