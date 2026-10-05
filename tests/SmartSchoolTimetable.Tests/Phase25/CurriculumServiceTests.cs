using System.Net;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Curriculum;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Setup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Tests.Phase2;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase25;

/// <summary>Section stepper, curriculum table and helpers, and idempotent templates (spec 2.5 §3.3, §3.4, §4).</summary>
public sealed class CurriculumServiceTests
{
    private static readonly string[] MathsOnly = ["الرياضيات"];

    private sealed record Seed(FakeDataStore Store, AcademicYear Year, Shift Morning, Shift Evening, Stage First, Stage Second, Subject Arabic, Subject Maths);

    private static async Task<Seed> SeedAsync()
    {
        var store = new FakeDataStore();
        store.Add(SchoolProfile.CreateDefault(DateTimeOffset.UnixEpoch));
        store.Add(WorkingWeek.CreateDefault());
        var year = AcademicYear.Create("2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        store.Add(year);
        await store.SaveChangesAsync(default);
        var morning = DayLessonsAndShiftModeTests.ShiftWithLessons(year.Id, 7, "الدوام الصباحي", ShiftKind.Morning); // 35 a week
        var evening = DayLessonsAndShiftModeTests.ShiftWithLessons(year.Id, 6, "الدوام المسائي", ShiftKind.Evening); // 30 a week
        var first = Stage.Create(year.Id, "الأول المتوسط", 10, "intermediate-1");
        var second = Stage.Create(year.Id, "الثاني المتوسط", 20, "intermediate-2");
        var arabic = Subject.Create(new SubjectDetails("اللغة العربية", 1, 3, true, false, false, false, null, null), ScheduleGrid.Uniform(DayLessonsAndShiftModeTests.SundayToThursday, 7));
        var maths = Subject.Create(new SubjectDetails("الرياضيات", 2, 3, true, false, false, false, null, null), ScheduleGrid.Uniform(DayLessonsAndShiftModeTests.SundayToThursday, 7));
        store.Add(morning);
        store.Add(evening);
        store.Add(first);
        store.Add(second);
        store.Add(arabic);
        store.Add(maths);
        await store.SaveChangesAsync(default);
        return new Seed(store, year, morning, evening, first, second, arabic, maths);
    }

    [Fact]
    public async Task StepperAddsNextLabelsAndRemovesOnlyTheLastSections()
    {
        var seed = await SeedAsync();
        var cards = new StageCardsService(seed.Store, TimeProvider.System);
        var three = (await cards.SetSectionCountAsync(seed.Year.Id, seed.First.Id, new SetSectionCountCommand(3, seed.Morning.Id, null), default)).Value!;
        Assert.Equal(["أ", "ب", "ج"], three.Sections.Select(section => section.Label));

        var two = (await cards.SetSectionCountAsync(seed.Year.Id, seed.First.Id, new SetSectionCountCommand(2, 0, null), default)).Value!;
        Assert.Equal(["أ", "ب"], two.Sections.Select(section => section.Label));
        var numbered = (await cards.SetSectionCountAsync(seed.Year.Id, seed.Second.Id, new SetSectionCountCommand(2, seed.Evening.Id, "numbers"), default)).Value!;
        Assert.Equal(["1", "2"], numbered.Sections.Select(section => section.Label));
        Assert.Equal(2, (await cards.ListAsync(seed.Year.Id, false, default)).Count);

        var invalid = await cards.SetSectionCountAsync(seed.Year.Id, seed.First.Id, new SetSectionCountCommand(31, seed.Morning.Id, "roman"), default);
        Assert.Equal(["LabelStyle", "Count"], invalid.FieldErrors.Select(error => error.Field));
        Assert.Equal(ErrorCodes.ShiftNotInYear, (await cards.SetSectionCountAsync(seed.Year.Id, seed.First.Id, new SetSectionCountCommand(4, 999, null), default)).FieldErrors.Single().Code);
        Assert.Equal(ErrorCodes.NotFound, (await cards.SetSectionCountAsync(seed.Year.Id, 999, new SetSectionCountCommand(1, seed.Morning.Id, null), default)).ErrorCode);
        seed.First.Archive(DateTimeOffset.UnixEpoch);
        Assert.Equal(ErrorCodes.StageArchived, (await cards.SetSectionCountAsync(seed.Year.Id, seed.First.Id, new SetSectionCountCommand(1, seed.Morning.Id, null), default)).ErrorCode);
    }

    [Fact]
    public async Task TableAllowsRepeatedSubjectsAndReportsTotalsPerShift()
    {
        var seed = await SeedAsync();
        var cards = new StageCardsService(seed.Store, TimeProvider.System);
        await cards.SetSectionCountAsync(seed.Year.Id, seed.First.Id, new SetSectionCountCommand(2, seed.Morning.Id, null), default);
        seed.Store.Add(Section.Create(seed.First.Id, seed.Evening.Id, "ج", null)); // a dual-shift stage
        await seed.Store.SaveChangesAsync(default);
        var curriculum = new CurriculumService(seed.Store, TimeProvider.System);

        await curriculum.SetCellAsync(seed.Year.Id, new SetCurriculumCellCommand(seed.First.Id, seed.Arabic.Id, null, 15, null, null), default);
        var table = (await curriculum.SetCellAsync(seed.Year.Id, new SetCurriculumCellCommand(seed.First.Id, seed.Arabic.Id, "أدب", 15, null, null), default)).Value!;
        Assert.Equal(2, seed.Store.Query<CurriculumEntry>().Count(entry => entry.SubjectId == seed.Arabic.Id)); // repeated subject
        Assert.Equal([null, "أدب"], table.Rows.Where(row => row.SubjectId == seed.Arabic.Id).Select(row => row.Label));
        var stage = table.Stages.Single(item => item.Id == seed.First.Id);
        Assert.Equal(30, stage.PlannedLessons);
        Assert.Equal([("الدوام الصباحي", "under", 5), ("الدوام المسائي", "equal", 0)], stage.Totals.Select(total => (total.ShiftName, total.Status, total.Difference)));

        var cell = table.Rows.First(row => row.SubjectId == seed.Arabic.Id).Cells.Single(item => item.StageId == seed.First.Id);
        Assert.True((await curriculum.SetCellAsync(seed.Year.Id, new SetCurriculumCellCommand(seed.First.Id, seed.Arabic.Id, null, 14, cell.EntryId, cell.Version), default)).Succeeded);
        Assert.Equal(ErrorCodes.Conflict, (await curriculum.SetCellAsync(seed.Year.Id, new SetCurriculumCellCommand(seed.First.Id, seed.Arabic.Id, null, 4, cell.EntryId, cell.Version), default)).ErrorCode);
        var over = (await curriculum.SetCellAsync(seed.Year.Id, new SetCurriculumCellCommand(seed.First.Id, seed.Arabic.Id, "نحو", 4, null, null), default)).Value!;
        Assert.Equal([("under", 2), ("over", -3)], over.Stages.Single(item => item.Id == seed.First.Id).Totals.Select(total => (total.Status, total.Difference)));
        Assert.Contains((await curriculum.SetCellAsync(seed.Year.Id, new SetCurriculumCellCommand(seed.First.Id, seed.Maths.Id, null, 16, null, null), default)).FieldErrors, error => error.Field == "WeeklyLessons");
        Assert.Equal(ErrorCodes.InvalidOption, (await curriculum.SetCellAsync(seed.Year.Id, new SetCurriculumCellCommand(seed.First.Id, 999, null, 4, null, null), default)).FieldErrors.Single().Code);
        Assert.Equal(ErrorCodes.NotFound, (await curriculum.SetCellAsync(seed.Year.Id, new SetCurriculumCellCommand(999, seed.Arabic.Id, null, 4, null, null), default)).ErrorCode);

        var cleared = (await curriculum.SetCellAsync(seed.Year.Id, new SetCurriculumCellCommand(seed.First.Id, seed.Arabic.Id, null, null, cell.EntryId, cell.Version + 1), default)).Value!;
        Assert.Equal(19, cleared.Stages.Single(item => item.Id == seed.First.Id).PlannedLessons);

        var entry = seed.Store.Query<CurriculumEntry>().Single(line => line.Label == "أدب");
        var updated = (await curriculum.UpdateEntryAsync(entry.Id, new SaveCurriculumEntryCommand(6, "أدب", true, "قراءة", entry.Version), default)).Value!;
        Assert.Equal((6, true, "قراءة"), (updated.WeeklyLessons, updated.NeedsDoublePeriod, updated.Notes));
        Assert.Equal(ErrorCodes.Conflict, (await curriculum.UpdateEntryAsync(entry.Id, new SaveCurriculumEntryCommand(6, null, false, null, 1), default)).ErrorCode);
        var archived = (await curriculum.SetArchivedAsync(entry.Id, updated.Version, true, default)).Value!;
        Assert.True(archived.IsArchived);
        Assert.Equal(4, (await curriculum.GetTableAsync(seed.Year.Id, default)).Stages.Single(item => item.Id == seed.First.Id).PlannedLessons);
        var restored = (await curriculum.SetArchivedAsync(entry.Id, archived.Version, false, default)).Value!;
        Assert.Equal(ErrorCodes.Conflict, (await curriculum.DeleteEntryAsync(entry.Id, 1, default)).ErrorCode);
        Assert.True((await curriculum.DeleteEntryAsync(entry.Id, restored.Version, default)).Value);
        Assert.Equal(ErrorCodes.NotFound, (await curriculum.DeleteEntryAsync(entry.Id, 1, default)).ErrorCode);
    }

    [Fact]
    public async Task CopyAndSetAcrossPreviewFirstAndChangeNothingTheSecondTime()
    {
        var seed = await SeedAsync();
        seed.Store.Add(CurriculumEntry.Create(seed.First.Id, seed.Arabic.Id, 5, null, false, null));
        seed.Store.Add(CurriculumEntry.Create(seed.First.Id, seed.Arabic.Id, 2, "أدب", false, null));
        seed.Store.Add(CurriculumEntry.Create(seed.First.Id, seed.Maths.Id, 6, null, false, null));
        seed.Store.Add(CurriculumEntry.Create(seed.Second.Id, seed.Maths.Id, 4, null, false, null));
        await seed.Store.SaveChangesAsync(default);
        var helpers = new CurriculumHelpersService(seed.Store, TimeProvider.System);
        var copy = new CopyCurriculumCommand(seed.First.Id, [seed.Second.Id, seed.First.Id]);

        var preview = (await helpers.CopyAsync(seed.Year.Id, copy, false, default)).Value!;
        Assert.Equal(2, preview.Changes);
        Assert.Equal(4, seed.Store.Query<CurriculumEntry>().Count()); // preview saves nothing
        Assert.Contains(preview.Lines, line => line is { SubjectName: "الرياضيات", StageId: var id, Action: "exists" } && id == seed.Second.Id);
        Assert.Contains(preview.Lines, line => line.Action == "notApplicable"); // copying onto itself
        Assert.Equal(2, (await helpers.CopyAsync(seed.Year.Id, copy, true, default)).Value!.Changes);
        Assert.Equal(4, seed.Store.Query<CurriculumEntry>().Single(entry => entry.StageId == seed.Second.Id && entry.SubjectId == seed.Maths.Id).WeeklyLessons); // never overwritten
        Assert.Equal(0, (await helpers.CopyAsync(seed.Year.Id, copy, true, default)).Value!.Changes);
        Assert.Equal(ErrorCodes.NotFound, (await helpers.CopyAsync(seed.Year.Id, new CopyCurriculumCommand(999, null), false, default)).ErrorCode);

        var across = new SetLessonsAcrossCommand(seed.Maths.Id, null, 5, [seed.First.Id, seed.Second.Id, 999]);
        var plan = (await helpers.SetAcrossAsync(seed.Year.Id, across, true, default)).Value!;
        Assert.Equal(["update", "update", "notApplicable"], plan.Lines.Select(line => line.Action));
        Assert.Equal(["unchanged", "unchanged", "notApplicable"], (await helpers.SetAcrossAsync(seed.Year.Id, across, true, default)).Value!.Lines.Select(line => line.Action));
        seed.Store.Add(CurriculumEntry.Create(seed.Second.Id, seed.Maths.Id, 1, null, false, null));
        await seed.Store.SaveChangesAsync(default);
        Assert.Equal("ambiguous", (await helpers.SetAcrossAsync(seed.Year.Id, across, false, default)).Value!.Lines[1].Action);
        Assert.Equal("create", (await helpers.SetAcrossAsync(seed.Year.Id, across with { Label = "هندسة" }, false, default)).Value!.Lines[0].Action);
        Assert.Equal("WeeklyLessons", (await helpers.SetAcrossAsync(seed.Year.Id, across with { WeeklyLessons = 0 }, false, default)).FieldErrors.Single().Field);
        Assert.Equal("SubjectId", (await helpers.SetAcrossAsync(seed.Year.Id, across with { SubjectId = 999 }, false, default)).FieldErrors.Single().Field);
    }

    [Fact]
    public async Task CopyingAYearCarriesTheActiveCurriculumLines()
    {
        var seed = await SeedAsync();
        seed.Store.Add(CurriculumEntry.Create(seed.First.Id, seed.Maths.Id, 6, null, false, null));
        var archived = CurriculumEntry.Create(seed.First.Id, seed.Arabic.Id, 4, null, false, null);
        archived.Archive(DateTimeOffset.UnixEpoch);
        seed.Store.Add(archived);
        var next = AcademicYear.Create("2027-2028", new DateOnly(2027, 9, 1), new DateOnly(2028, 6, 30));
        seed.Store.Add(next);
        await seed.Store.SaveChangesAsync(default);
        await new YearStructureService(seed.Store).CopyAsync(seed.Year.Id, next.Id, default);
        await seed.Store.SaveChangesAsync(default);
        var copiedStage = seed.Store.Query<Stage>().Single(stage => stage.AcademicYearId == next.Id && stage.TemplateKey == "intermediate-1");
        Assert.Equal((seed.Maths.Id, 6), seed.Store.Query<CurriculumEntry>().Where(entry => entry.StageId == copiedStage.Id).AsEnumerable().Select(entry => (entry.SubjectId, entry.WeeklyLessons)).Single());
    }

    [Fact]
    public async Task CurriculumLinesProtectTheirStageAndSubject()
    {
        var seed = await SeedAsync();
        seed.Store.Add(CurriculumEntry.Create(seed.Second.Id, seed.Maths.Id, 4, null, false, null));
        await seed.Store.SaveChangesAsync(default);
        var stages = new StagesSectionsService(seed.Store, TimeProvider.System);
        var subjects = new SubjectsService(seed.Store, TimeProvider.System);
        Assert.Equal(ErrorCodes.CurriculumInUse, (await stages.SetStageArchivedAsync(seed.Year.Id, seed.Second.Id, new ArchiveCommand(seed.Second.Version), true, default)).ErrorCode);
        Assert.Equal(ErrorCodes.CurriculumInUse, (await stages.DeleteStageAsync(seed.Year.Id, seed.Second.Id, seed.Second.Version, default)).ErrorCode);
        Assert.Equal(ErrorCodes.CurriculumInUse, (await subjects.SetArchivedAsync(seed.Maths.Id, seed.Maths.Version, true, default)).ErrorCode);
        Assert.Equal(ErrorCodes.CurriculumInUse, (await subjects.DeleteAsync(seed.Maths.Id, seed.Maths.Version, default)).ErrorCode);

        seed.Store.Query<CurriculumEntry>().Single().Archive(DateTimeOffset.UnixEpoch); // archived lines allow archiving, not deleting
        Assert.True((await stages.SetStageArchivedAsync(seed.Year.Id, seed.Second.Id, new ArchiveCommand(seed.Second.Version), true, default)).Succeeded);
        Assert.True((await subjects.SetArchivedAsync(seed.Maths.Id, seed.Maths.Version, true, default)).Succeeded);
        Assert.Equal(ErrorCodes.CurriculumInUse, (await subjects.DeleteAsync(seed.Maths.Id, seed.Maths.Version, default)).ErrorCode);
    }

    [Fact]
    public async Task TemplatesCreateOnlyWhatIsMissingAndNeverLowerSections()
    {
        var seed = await SeedAsync();
        var stages = new StagesSectionsService(seed.Store, TimeProvider.System);
        var cards = new StageCardsService(seed.Store, TimeProvider.System);
        var subjects = new SubjectsService(seed.Store, TimeProvider.System);
        var templates = new SetupTemplatesService(seed.Store, stages, cards, subjects);
        await cards.SetSectionCountAsync(seed.Year.Id, seed.First.Id, new SetSectionCountCommand(4, seed.Morning.Id, null), default);
        StageTemplateGradeInput Grade(string key, int sections, params string[] branches) => new(key, branches, sections, seed.Morning.Id, null);
        var command = new StageTemplateCommand("secondary",
            [Grade("intermediate-1", 2), Grade("intermediate-3", 3), Grade("preparatory-4", 2, "scientific", "literary"), Grade("primary-1", 1), Grade("preparatory-5", 1, "arts")]);

        var preview = (await templates.StagesAsync(seed.Year.Id, command, false, default)).Value!;
        Assert.Equal(
            [("intermediate-1", "exists", 0), ("intermediate-3", "create", 3), ("preparatory-4-scientific", "create", 2), ("preparatory-4-literary", "create", 2), ("primary-1", "notApplicable", 0), ("preparatory-5", "notApplicable", 0)],
            preview.Lines.Select(line => (line.Key, line.Action, line.SectionsToAdd)));
        Assert.Equal(3, preview.Changes);
        Assert.Equal(2, seed.Store.Query<Stage>().Count());

        Assert.Equal(3, (await templates.StagesAsync(seed.Year.Id, command, true, default)).Value!.Changes);
        var created = seed.Store.Query<Stage>().Single(stage => stage.TemplateKey == "preparatory-4-literary");
        Assert.Equal(("الرابع الأدبي", 41), (created.Name, created.DisplayOrder));
        Assert.Equal(4, seed.Store.Query<Section>().Count(section => section.StageId == seed.First.Id)); // never lowered
        var again = (await templates.StagesAsync(seed.Year.Id, command, true, default)).Value!;
        Assert.Equal(0, again.Changes);
        Assert.Equal(5, seed.Store.Query<Stage>().Count());

        Assert.Equal("SchoolType", (await templates.StagesAsync(seed.Year.Id, command with { SchoolType = "college" }, false, default)).FieldErrors.Single().Field);
        Assert.Equal(ErrorCodes.NotFound, (await templates.StagesAsync(999, command, false, default)).ErrorCode);
        var suggested = await templates.SuggestedSubjectsAsync(seed.Year.Id, default);
        Assert.Contains("الرياضيات", suggested);
        Assert.Equal(suggested.Count, suggested.Distinct().Count());

        var names = new SubjectTemplateCommand(["الرياضيات", "الفيزياء", " الفيزياء ", "", "الكيمياء"]);
        var subjectPlan = (await templates.SubjectsAsync(names, false, default)).Value!;
        Assert.Equal([("الرياضيات", "exists"), ("الفيزياء", "create"), ("الكيمياء", "create")], subjectPlan.Lines.Select(line => (line.Name, line.Action)));
        Assert.Equal(2, (await templates.SubjectsAsync(names, true, default)).Value!.Changes);
        Assert.Equal(0, (await templates.SubjectsAsync(names, true, default)).Value!.Changes);
        Assert.Equal(4, seed.Store.Query<Subject>().Count());
        Assert.NotEmpty(SetupTemplatesService.Catalog().PeriodPresets);
    }

    [Fact]
    public async Task RoutesExposeTheTableAndRollBackAFailedTemplate()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/templates/")).StatusCode);
        var (token, _) = await SetupOwnerAsync(host);
        var year = await AcademicYearApiTests.CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");
        var mode = await ReadAsync<ShiftModeDto>(await host.PutAsync("/api/v1/shift-mode/", new { mode = "morning", version = 1 }, token));
        var shiftId = mode.Shifts.Single().Id;
        var lessons = Enumerable.Range(0, 7).Select(index => new { kind = "lesson", startTime = $"{8 + index:00}:00", endTime = $"{8 + index:00}:45", startBell = true, endBell = true }).ToArray();
        Assert.Equal(HttpStatusCode.OK, (await host.PutAsync($"/api/v1/academic-years/{year.Id}/shifts/{shiftId}/periods", new { periods = lessons, version = mode.Shifts.Single().Version }, token)).StatusCode);
        var root = $"/api/v1/academic-years/{year.Id}";
        Assert.NotEmpty((await ReadAsync<TemplateCatalogDto>(await host.Client.GetAsync("/api/v1/templates/"))).Grades);
        var preset = (await ReadAsync<TemplateCatalogDto>(await host.Client.GetAsync("/api/v1/templates/"))).PeriodPresets.First(item => item.Breaks.Count > 1);
        var generated = await ReadAsync<GeneratedPeriodsDto>(await host.PostAsync($"/api/v1/academic-years/{year.Id}/shifts/generate-periods",
            new { firstStartTime = preset.FirstStart, lessonMinutes = preset.LessonMinutes, lessonCount = preset.LessonCount, breakMinutes = 0, breaks = preset.Breaks }, token));
        Assert.Equal(preset.Breaks.Count, generated.Periods.Count(period => period.Kind == "break"));

        // The second grade's shift does not exist: the first grade's stage must be rolled back with it.
        var failing = new { schoolType = "intermediate", grades = new object[] { new { gradeKey = "intermediate-1", sections = 2, shiftId }, new { gradeKey = "intermediate-2", sections = 2, shiftId = 999 } } };
        await AssertApiErrorAsync(await host.PostAsync($"{root}/templates/stages", failing, token), "VALIDATION_FAILED");
        Assert.Empty(await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards")));

        var good = new { schoolType = "intermediate", grades = new object[] { new { gradeKey = "intermediate-1", sections = 2, shiftId } } };
        Assert.Equal(1, (await ReadAsync<StagePlanDto>(await host.PostAsync($"{root}/templates/stages/preview", good, token))).Changes);
        Assert.Equal(1, (await ReadAsync<StagePlanDto>(await host.PostAsync($"{root}/templates/stages", good, token))).Changes);
        var card = (await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards"))).Single();
        Assert.Equal(["أ", "ب"], card.Sections.Select(section => section.Label));
        var stepped = await ReadAsync<StageCardDto>(await host.PutAsync($"{root}/stage-cards/{card.Stage.Id}/section-count", new { count = 3, shiftId }, token));
        Assert.Equal(3, stepped.Sections.Count);
        Assert.Contains("اللغة العربية", await ReadAsync<List<string>>(await host.Client.GetAsync($"{root}/templates/suggested-subjects")));

        Assert.Equal(1, (await ReadAsync<SubjectPlanDto>(await host.PostAsync("/api/v1/templates/subjects/preview", new { names = MathsOnly }, token))).Changes);
        await ReadAsync<SubjectPlanDto>(await host.PostAsync("/api/v1/templates/subjects", new { names = MathsOnly }, token));
        var table = await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum"));
        var subjectId = table.Rows.Single().SubjectId;
        table = await ReadAsync<CurriculumTableDto>(await host.PutAsync($"{root}/curriculum/cell", new { stageId = card.Stage.Id, subjectId, weeklyLessons = 15 }, token));
        Assert.Equal(("under", 20), (table.Stages.Single().Totals.Single().Status, table.Stages.Single().Totals.Single().Difference));
        await ReadAsync<CurriculumTableDto>(await host.PutAsync($"{root}/curriculum/cell", new { stageId = card.Stage.Id, subjectId, label = "هندسة", weeklyLessons = 2 }, token));
        var invalid = await host.PutAsync($"{root}/curriculum/cell", new { stageId = card.Stage.Id, subjectId, weeklyLessons = 0 }, token);
        Assert.Contains((await AssertApiErrorAsync(invalid, "VALIDATION_FAILED")).Errors, issue => issue.Field == "WeeklyLessons");

        var across = new { subjectId, weeklyLessons = 4, stageIds = new[] { card.Stage.Id } };
        Assert.Equal(1, (await ReadAsync<CurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/set-across/preview", across, token))).Changes);
        Assert.Equal(1, (await ReadAsync<CurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/set-across", across, token))).Changes);
        var copy = new { fromStageId = card.Stage.Id, toStageIds = new[] { card.Stage.Id } };
        Assert.Equal(0, (await ReadAsync<CurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/copy/preview", copy, token))).Changes);
        Assert.Equal(0, (await ReadAsync<CurriculumPlanDto>(await host.PostAsync($"{root}/curriculum/copy", copy, token))).Changes);

        table = await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum"));
        var cell = table.Rows.First(row => row.Label is null).Cells.Single();
        var entryPath = $"/api/v1/curriculum-entries/{cell.EntryId}";
        var entry = await ReadAsync<CurriculumEntryDto>(await host.PutAsync(entryPath, new { weeklyLessons = 5, needsDoublePeriod = true, version = cell.Version }, token));
        var archived = await ReadAsync<CurriculumEntryDto>(await host.PostAsync($"{entryPath}/archive", new { version = entry.Version }, token));
        var restored = await ReadAsync<CurriculumEntryDto>(await host.PostAsync($"{entryPath}/restore", new { version = archived.Version }, token));
        Assert.Equal(HttpStatusCode.Conflict, (await host.SendJsonAsync(HttpMethod.Delete, $"{entryPath}?version=1", new { }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendJsonAsync(HttpMethod.Delete, $"{entryPath}?version={restored.Version}", new { }, token)).StatusCode);

        // The remaining labelled line still blocks deleting the stage.
        await AssertApiErrorAsync(await host.SendJsonAsync(HttpMethod.Delete, $"{root}/stages/{card.Stage.Id}?version={stepped.Stage.Version}", new { }, token), "RECORD_IN_USE");
    }
}
