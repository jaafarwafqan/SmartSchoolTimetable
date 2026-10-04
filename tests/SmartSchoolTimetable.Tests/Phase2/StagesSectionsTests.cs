using System.Net;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase2;

public sealed class StagesSectionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    private static Shift ShiftWithLessons(long yearId, int lessons, string name = "صباحي", int order = 1)
    {
        var shift = Shift.Create(yearId, name, order);
        var drafts = Enumerable.Range(0, lessons)
            .Select(index => new PeriodDraft(PeriodKind.Lesson, new TimeOnly(8, 0).AddMinutes(index * 45), new TimeOnly(8, 45).AddMinutes(index * 45)))
            .Append(new PeriodDraft(PeriodKind.Break, new TimeOnly(8, 0).AddMinutes(lessons * 45), new TimeOnly(8, 10).AddMinutes(lessons * 45)))
            .ToArray();
        shift.ReplacePeriods(drafts);
        return shift;
    }

    [Fact]
    public void WeeklyCapacityIsWorkingDaysTimesLessonsExcludingBreaks()
    {
        var week = WorkingWeek.CreateDefault();
        Assert.Equal(35, Section.WeeklyCapacity(week, ShiftWithLessons(1, 7)));
        week.Update([7, 1, 2, 3, 4, 5], Weekday.Sunday);
        Assert.Equal(42, Section.WeeklyCapacity(week, ShiftWithLessons(1, 7)));
        Assert.Equal(0, Section.WeeklyCapacity(week, null));
        Assert.Equal(0, Section.WeeklyCapacity(null, ShiftWithLessons(1, 7)));
        Assert.Equal(0, Section.WeeklyCapacity(week, Shift.Create(1, "مسائي", 2)));
    }

    [Fact]
    public void StagesAndSectionsValidateArchiveRestoreAndCopy()
    {
        Assert.Contains(Assert.Throws<DomainValidationException>(() => Stage.Create(1, " ", 0)).Errors,
            error => error is { Field: "DisplayOrder", Code: DomainErrorCode.OutOfRange });
        Assert.Contains(Assert.Throws<DomainValidationException>(() => Section.Create(1, 1, new string('x', 41), -1)).Errors,
            error => error is { Field: "StudentCount", Code: DomainErrorCode.OutOfRange });

        var stage = Stage.Create(1, "  الرابع   العلمي ", 4);
        Assert.Equal("الرابع العلمي", stage.Name);
        stage.Archive(Now);
        stage.Archive(Now.AddDays(1));
        Assert.Equal(Now, stage.ArchivedAt);
        var copy = stage.CopyTo(2);
        Assert.True(copy.IsArchived);
        Assert.Equal(2, copy.AcademicYearId);
        var version = stage.Version;
        stage.Restore();
        stage.Restore();
        Assert.Equal(version + 1, stage.Version);
        Assert.Null(stage.ArchivedAt);

        var section = Section.Create(5, 6, "أ", null);
        section.Update(7, "ب", 30);
        section.Archive(Now);
        var sectionCopy = section.CopyTo(8, 9);
        Assert.Equal((8L, 9L, "ب", 30, true), (sectionCopy.StageId, sectionCopy.ShiftId, sectionCopy.Label, sectionCopy.StudentCount!.Value, sectionCopy.IsArchived));
    }

    private static async Task<(StagesSectionsService Service, FakeDataStore Store, AcademicYear Year, Shift Shift)> SeedAsync()
    {
        var store = new FakeDataStore();
        var year = AcademicYear.Create("2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        store.Add(year);
        store.Add(WorkingWeek.CreateDefault());
        await store.SaveChangesAsync(default);
        var shift = ShiftWithLessons(year.Id, 6);
        store.Add(shift);
        await store.SaveChangesAsync(default);
        return (new StagesSectionsService(store, TimeProvider.System), store, year, shift);
    }

    [Fact]
    public async Task ServiceEnforcesArchiveDeleteAndReferenceRules()
    {
        var (service, store, year, shift) = await SeedAsync();
        Assert.Equal(ErrorCodes.NotFound, (await service.CreateStageAsync(999, new SaveStageCommand("x", 1, 0), default)).ErrorCode);
        Assert.Contains((await service.CreateStageAsync(year.Id, new SaveStageCommand("", 0, 0), default)).FieldErrors, error => error.Field == "Name");

        var stage = (await service.CreateStageAsync(year.Id, new SaveStageCommand("الأول المتوسط", 1, 0), default)).Value!;
        var sameOrder = await service.CreateStageAsync(year.Id, new SaveStageCommand("الثاني المتوسط", 1, 0), default);
        Assert.True(sameOrder.Succeeded); // display order is a sort key, ties are allowed
        Assert.Contains((await service.UpdateStageAsync(year.Id, sameOrder.Value!.Id, new SaveStageCommand("الاول المتوسط", 2, sameOrder.Value.Version), default)).FieldErrors,
            error => error is { Field: "Name", Code: ErrorCodes.DuplicateName });

        Assert.Contains((await service.CreateSectionAsync(year.Id, stage.Id, new SaveSectionCommand("أ", 999, null, 0), default)).FieldErrors,
            error => error is { Field: "ShiftId", Code: ErrorCodes.ShiftNotInYear });
        var section = (await service.CreateSectionAsync(year.Id, stage.Id, new SaveSectionCommand("أ", shift.Id, 30, 0), default)).Value!;
        Assert.Equal(30, section.WeeklyCapacity);
        Assert.Contains((await service.CreateSectionAsync(year.Id, stage.Id, new SaveSectionCommand(" أ ", shift.Id, null, 0), default)).FieldErrors,
            error => error is { Field: "Label", Code: ErrorCodes.DuplicateName });

        Assert.Equal(ErrorCodes.RecordInUse, (await service.DeleteStageAsync(year.Id, stage.Id, stage.Version, default)).ErrorCode);
        Assert.Equal(ErrorCodes.RecordInUse, (await service.SetStageArchivedAsync(year.Id, stage.Id, new ArchiveCommand(stage.Version), true, default)).ErrorCode);
        var archivedSection = (await service.SetSectionArchivedAsync(year.Id, stage.Id, section.Id, new ArchiveCommand(section.Version), true, default)).Value!;
        var archivedStage = (await service.SetStageArchivedAsync(year.Id, stage.Id, new ArchiveCommand(stage.Version), true, default)).Value!;
        Assert.Equal(ErrorCodes.StageArchived, (await service.SetSectionArchivedAsync(year.Id, stage.Id, section.Id, new ArchiveCommand(archivedSection.Version), false, default)).ErrorCode);
        Assert.Equal(ErrorCodes.StageArchived, (await service.CreateSectionAsync(year.Id, stage.Id, new SaveSectionCommand("ب", shift.Id, null, 0), default)).ErrorCode);
        Assert.DoesNotContain((await service.ListStagesAsync(year.Id, new ListQuery(null, null, null, null, null), default)).Items, item => item.Id == stage.Id);

        Assert.Equal(ErrorCodes.Conflict, (await service.DeleteSectionAsync(year.Id, stage.Id, section.Id, section.Version, default)).ErrorCode);
        Assert.True((await service.DeleteSectionAsync(year.Id, stage.Id, section.Id, archivedSection.Version, default)).Succeeded);
        Assert.True((await service.DeleteStageAsync(year.Id, stage.Id, archivedStage.Version, default)).Succeeded);
        Assert.Equal(ErrorCodes.NotFound, (await service.DeleteStageAsync(year.Id, stage.Id, archivedStage.Version, default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.DeleteSectionAsync(year.Id, stage.Id, section.Id, 1, default)).ErrorCode);
    }

    [Fact]
    public async Task SaveFailuresAndListsAreMappedConsistently()
    {
        var (service, store, year, shift) = await SeedAsync();
        store.NextSaveFailure = new DataConflictException();
        Assert.Contains((await service.CreateStageAsync(year.Id, new SaveStageCommand("الأول", 1, 0), default)).FieldErrors,
            error => error is { Field: "Name", Code: ErrorCodes.DuplicateName });
        var stage = (await service.CreateStageAsync(year.Id, new SaveStageCommand("الثاني", 1, 0), default)).Value!;
        store.NextSaveFailure = new ConcurrencyConflictException();
        Assert.Equal(ErrorCodes.Conflict, (await service.UpdateStageAsync(year.Id, stage.Id, new SaveStageCommand("الثاني", 2, stage.Version), default)).ErrorCode);

        foreach (var label in new[] { "ج", "أ", "ب" })
            await service.CreateSectionAsync(year.Id, stage.Id, new SaveSectionCommand(label, shift.Id, null, 0), default);
        var page = await service.ListSectionsAsync(year.Id, stage.Id, new ListQuery(null, "-label", 1, 2, null), default);
        Assert.Equal(3, page.Total);
        Assert.Equal(["ج", "ب"], page.Items.Select(item => item.Label));
        Assert.Single((await service.ListSectionsAsync(year.Id, stage.Id, new ListQuery("ا", null, null, null, null), default)).Items);
        Assert.Empty((await service.ListSectionsAsync(year.Id, 999, new ListQuery(null, null, null, null, null), default)).Items);
        Assert.Single((await service.ListStagesAsync(year.Id, new ListQuery("ثاني", "-name", null, null, true), default)).Items);
        Assert.Equal(ErrorCodes.NotFound, (await service.UpdateSectionAsync(year.Id, stage.Id, 999, new SaveSectionCommand("x", shift.Id, null, 1), default)).ErrorCode);
        var first = page.Items[0];
        Assert.Equal(ErrorCodes.Conflict, (await service.UpdateSectionAsync(year.Id, stage.Id, first.Id, new SaveSectionCommand("د", shift.Id, null, 99), default)).ErrorCode);
        Assert.Equal("د", (await service.UpdateSectionAsync(year.Id, stage.Id, first.Id, new SaveSectionCommand("د", shift.Id, null, first.Version), default)).Value!.Label);
    }

    [Fact]
    public async Task NewYearCopiesShiftsStagesAndSectionsLinkedToTheirCopies()
    {
        var (service, store, year, shift) = await SeedAsync();
        var evening = ShiftWithLessons(year.Id, 5, "مسائي", 1); // same display order as the morning shift
        store.Add(evening);
        await store.SaveChangesAsync(default);
        var stage = (await service.CreateStageAsync(year.Id, new SaveStageCommand("الأول", 1, 0), default)).Value!;
        await service.CreateSectionAsync(year.Id, stage.Id, new SaveSectionCommand("أ", evening.Id, null, 0), default);

        var structure = new YearStructureService(store);
        var next = AcademicYear.Create("2027-2028", new DateOnly(2027, 9, 1), new DateOnly(2028, 6, 30));
        store.Add(next);
        await store.SaveChangesAsync(default);
        await structure.CopyAsync(year.Id, next.Id, default);
        await store.SaveChangesAsync(default);

        var copiedStage = Assert.Single(store.Query<Stage>().Where(row => row.AcademicYearId == next.Id));
        var copiedSection = Assert.Single(store.Query<Section>().Where(row => row.StageId == copiedStage.Id));
        var copiedShift = store.Query<Shift>().Single(row => row.Id == copiedSection.ShiftId);
        Assert.Equal((next.Id, "مسائي", 5), (copiedShift.AcademicYearId, copiedShift.Name, copiedShift.LessonCount));
        Assert.Equal(ErrorCodes.YearStructureInUse, await structure.DeletionBlockAsync(next.Id, default));
        Assert.Null(await structure.DeletionBlockAsync(999, default));
        Assert.NotEqual(shift.Id, copiedShift.Id);
    }

    [Fact]
    public async Task ShiftsInUseCannotBeDeletedAndUnusedOnesCan()
    {
        var (_, store, year, shift) = await SeedAsync();
        var structureService = new TimetableStructureService(store, TimeProvider.System);
        var stages = new StagesSectionsService(store, TimeProvider.System);
        var stage = (await stages.CreateStageAsync(year.Id, new SaveStageCommand("الأول", 1, 0), default)).Value!;
        await stages.CreateSectionAsync(year.Id, stage.Id, new SaveSectionCommand("أ", shift.Id, null, 0), default);
        Assert.Equal(ErrorCodes.RecordInUse, (await structureService.DeleteShiftAsync(year.Id, shift.Id, shift.Version, default)).ErrorCode);
        Assert.Equal(ErrorCodes.Conflict, (await structureService.DeleteShiftAsync(year.Id, shift.Id, 99, default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await structureService.DeleteShiftAsync(year.Id, 999, 1, default)).ErrorCode);
        var spare = (await structureService.CreateShiftAsync(year.Id, new SaveShiftCommand("مسائي", 1, 0), default)).Value!;
        Assert.True((await structureService.DeleteShiftAsync(year.Id, spare.Id, spare.Version, default)).Succeeded);
    }

    [Fact]
    public async Task StageAndSectionRoutesAreProtectedAndSupportDelete()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var year = await AcademicYearApiTests.CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");
        var stagesPath = $"/api/v1/academic-years/{year.Id}/stages/";

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClientWithoutCookies().GetAsync(stagesPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PostAsync(stagesPath, new { name = "x", displayOrder = 1, version = 0 }, "wrong-token")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PostAsync(stagesPath, new { name = "x", displayOrder = 1, version = 0 }, token, "http://evil.example")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PostWithoutTokenAsync(stagesPath, new { name = "x", displayOrder = 1, version = 0 })).StatusCode);

        var stage = await ReadAsync<StageDto>(await host.PostAsync(stagesPath, new { name = "الأول", displayOrder = 1, version = 0 }, token));
        var shift = await ReadAsync<ShiftDto>(await host.PostAsync($"/api/v1/academic-years/{year.Id}/shifts/", new { name = "صباحي", displayOrder = 1, version = 0 }, token));
        var section = await ReadAsync<SectionDto>(await host.PostAsync($"{stagesPath}{stage.Id}/sections", new { label = "أ", shiftId = shift.Id, studentCount = (int?)null, version = 0 }, token));
        Assert.Equal(0, section.WeeklyCapacity); // the shift has no lessons yet

        await AssertApiErrorAsync(await host.SendJsonAsync(HttpMethod.Delete, $"{stagesPath}{stage.Id}?version={stage.Version}", new { }, token), "RECORD_IN_USE");
        await AssertApiErrorAsync(await host.SendJsonAsync(HttpMethod.Delete, $"/api/v1/academic-years/{year.Id}/shifts/{shift.Id}?version={shift.Version}", new { }, token), "RECORD_IN_USE");
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendJsonAsync(HttpMethod.Delete, $"{stagesPath}{stage.Id}/sections/{section.Id}?version={section.Version}", new { }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendJsonAsync(HttpMethod.Delete, $"{stagesPath}{stage.Id}?version={stage.Version}", new { }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendJsonAsync(HttpMethod.Delete, $"/api/v1/academic-years/{year.Id}/shifts/{shift.Id}?version={shift.Version}", new { }, token)).StatusCode);
        Assert.Empty((await ReadAsync<PagedResult<StageDto>>(await host.Client.GetAsync($"{stagesPath}?includeArchived=true"))).Items);
    }
}
