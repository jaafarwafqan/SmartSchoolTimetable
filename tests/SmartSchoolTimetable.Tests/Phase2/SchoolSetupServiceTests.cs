using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Dashboard;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Tests.Phase2;

public sealed class SchoolSetupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private sealed class RecordingStructure(bool hasStructure) : IYearStructure
    {
        public List<(long Source, long Target)> Copies { get; } = [];

        public Task<bool> HasStructureAsync(long yearId, CancellationToken cancellationToken) => Task.FromResult(hasStructure);

        public Task CopyAsync(long sourceYearId, long targetYearId, CancellationToken cancellationToken)
        {
            Copies.Add((sourceYearId, targetYearId));
            return Task.CompletedTask;
        }
    }

    private static (AcademicYearService Service, FakeDataStore Store, RecordingStructure Structure) Years(bool hasStructure = false)
    {
        var store = new FakeDataStore();
        var structure = new RecordingStructure(hasStructure);
        return (new AcademicYearService(store, TimeProvider.System, structure), store, structure);
    }

    private static SaveAcademicYearCommand YearCommand(string label = "2026-2027", int version = 0, long? copyFrom = null) =>
        new(label, "2026-09-01", "2027-06-30", version, copyFrom);

    [Fact]
    public async Task StagesSectionsUseNormalizedUniquenessArchiveRulesAndWeeklyCapacity()
    {
        var store = new FakeDataStore();
        var year = AcademicYear.Create("2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        store.Add(year);
        var shift = Shift.Create(1, "صباحي", 1);
        shift.ReplacePeriods([new PeriodDraft(PeriodKind.Lesson, new TimeOnly(8, 0), new TimeOnly(8, 45))]);
        store.Add(shift);
        store.Add(WorkingWeek.CreateDefault());
        await store.SaveChangesAsync(default);
        var service = new StagesSectionsService(store, TimeProvider.System);
        var stage = (await service.CreateStageAsync(year.Id, new SaveStageCommand("الأول المتوسط", 1, 0), default)).Value!;
        var duplicate = await service.CreateStageAsync(year.Id, new SaveStageCommand("الاول المتوسط", 2, 0), default);
        Assert.Contains(duplicate.FieldErrors, error => error.Code == ErrorCodes.DuplicateName);
        var section = (await service.CreateSectionAsync(year.Id, stage.Id, new SaveSectionCommand("أ", shift.Id, 31, 0), default)).Value!;
        Assert.Equal(5, section.WeeklyCapacity);
        Assert.Equal(ErrorCodes.RecordInUse, (await service.SetStageArchivedAsync(year.Id, stage.Id, new ArchiveCommand(stage.Version), true, default)).ErrorCode);
        var archived = (await service.SetSectionArchivedAsync(year.Id, stage.Id, section.Id, new ArchiveCommand(section.Version), true, default)).Value!;
        var archivedStage = await service.SetStageArchivedAsync(year.Id, stage.Id, new ArchiveCommand(stage.Version), true, default);
        Assert.True(archived.IsArchived);
        Assert.True(archivedStage.Value!.IsArchived);
        Assert.Equal(ErrorCodes.Conflict, (await service.SetSectionArchivedAsync(year.Id, stage.Id, section.Id, new ArchiveCommand(section.Version), false, default)).ErrorCode);
    }

    [Fact]
    public async Task YearOperationsReportNotFoundStaleVersionsAndInUseRecords()
    {
        var (service, store, _) = Years(hasStructure: true);
        var year = (await service.CreateAsync(YearCommand(), default)).Value!;

        Assert.Equal(ErrorCodes.NotFound, (await service.GetAsync(999, default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.UpdateAsync(999, YearCommand(version: 1), default)).ErrorCode);
        Assert.Equal(ErrorCodes.Conflict, (await service.UpdateAsync(year.Id, YearCommand(version: 99), default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.DeleteAsync(999, 1, default)).ErrorCode);
        Assert.Equal(ErrorCodes.Conflict, (await service.DeleteAsync(year.Id, 99, default)).ErrorCode);
        Assert.Equal(ErrorCodes.RecordInUse, (await service.DeleteAsync(year.Id, year.Version, default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.SetCurrentAsync(999, 1, default)).ErrorCode);
        Assert.Equal(ErrorCodes.Conflict, (await service.SetCurrentAsync(year.Id, 99, default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.AddTermAsync(999, new SaveTermCommand("x", "2026-09-01", "2026-12-01", 1), default)).ErrorCode);
        Assert.Equal(ErrorCodes.Conflict, (await service.AddTermAsync(year.Id, new SaveTermCommand("x", "2026-09-01", "2026-12-01", 99), default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.DeleteTermAsync(year.Id, 999, year.Version, default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.SetCurrentTermAsync(year.Id, 999, year.Version, default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.UpdateTermAsync(year.Id, 999, new SaveTermCommand("x", "2026-09-01", "2026-12-01", year.Version), default)).ErrorCode);

        var withTerm = (await service.AddTermAsync(year.Id, new SaveTermCommand("الأول", "2026-09-01", "2026-12-01", year.Version), default)).Value!;
        var termId = withTerm.Terms[0].Id;
        Assert.Equal(ErrorCodes.Conflict, (await service.DeleteTermAsync(year.Id, termId, 99, default)).ErrorCode);
        Assert.Equal(ErrorCodes.Conflict, (await service.SetCurrentTermAsync(year.Id, termId, 99, default)).ErrorCode);
        Assert.Equal(1, await store.CountAsync(store.Query<AcademicYear>(), default));
    }

    [Fact]
    public async Task SaveFailuresBecomeConflictOrDuplicateErrors()
    {
        var (service, store, _) = Years();
        var year = (await service.CreateAsync(YearCommand(), default)).Value!;

        store.NextSaveFailure = new ConcurrencyConflictException();
        Assert.Equal(ErrorCodes.Conflict, (await service.UpdateAsync(year.Id, YearCommand(version: year.Version), default)).ErrorCode);

        var reloaded = (await service.GetAsync(year.Id, default)).Value!;
        store.NextSaveFailure = new DataConflictException();
        var duplicate = await service.UpdateAsync(year.Id, YearCommand("2026/2027", reloaded.Version), default);
        Assert.Contains(duplicate.FieldErrors, error => error is { Field: "Label", Code: ErrorCodes.DuplicateName });

        var current = (await service.GetAsync(year.Id, default)).Value!;
        store.NextSaveFailure = new ConcurrencyConflictException();
        Assert.Equal(ErrorCodes.Conflict, (await service.SetCurrentAsync(year.Id, current.Version, default)).ErrorCode);

        store.NextSaveFailure = new ConcurrencyConflictException();
        Assert.Equal(ErrorCodes.Conflict, (await service.DeleteAsync(year.Id, current.Version, default)).ErrorCode);
    }

    [Fact]
    public async Task TheCurrentYearCannotBeDeletedWhileOtherYearsExist()
    {
        var (service, _, _) = Years();
        var current = (await service.CreateAsync(YearCommand("2025-2026"), default)).Value!;
        var next = (await service.CreateAsync(YearCommand("2026-2027"), default)).Value!;

        Assert.Equal(ErrorCodes.CurrentYearRequired, (await service.DeleteAsync(current.Id, current.Version, default)).ErrorCode);
        Assert.True((await service.DeleteAsync(next.Id, next.Version, default)).Succeeded);
        Assert.True((await service.DeleteAsync(current.Id, current.Version, default)).Succeeded);
    }

    [Fact]
    public async Task CopyingStructureRequiresAnExistingSourceYear()
    {
        var (service, _, structure) = Years();
        var source = (await service.CreateAsync(YearCommand("2025-2026"), default)).Value!;

        var missing = await service.CreateAsync(YearCommand("2026-2027", copyFrom: 999), default);
        Assert.Contains(missing.FieldErrors, error => error.Field == "CopyStructureFromYearId" && error.Code == ErrorCodes.InvalidOption);

        var copied = await service.CreateAsync(YearCommand("2026-2027", copyFrom: source.Id), default);
        Assert.True(copied.Succeeded);
        Assert.Equal([(source.Id, copied.Value!.Id)], structure.Copies);
        Assert.False(copied.Value.IsCurrent); // the first year stays current

        var invalidTerm = await service.AddTermAsync(source.Id, new SaveTermCommand("x", "bad", "", source.Version), default);
        Assert.Contains(invalidTerm.FieldErrors, error => error is { Field: "StartDate", Code: ErrorCodes.InvalidDate });
        Assert.Contains(invalidTerm.FieldErrors, error => error is { Field: "EndDate", Code: ErrorCodes.Required });
    }

    [Fact]
    public async Task ProfileServiceHandlesUnknownKindsMissingAssetsAndSaveConflicts()
    {
        var store = new FakeDataStore();
        store.Add(SchoolProfile.CreateDefault(Now));
        var assets = new FakeAssetStore();
        var service = new SchoolProfileService(store, assets, TimeProvider.System);

        Assert.Equal(ErrorCodes.NotFound, (await service.UploadAssetAsync("banner", TestImages.Png, "image/png", 1, default)).ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, (await service.RemoveAssetAsync("banner", 1, default)).ErrorCode);
        Assert.Null(await service.OpenAssetAsync("banner", default));
        Assert.Null(await service.OpenAssetAsync("logo", default));
        Assert.Equal(ErrorCodes.NotFound, (await service.RemoveAssetAsync("logo", 1, default)).ErrorCode);
        Assert.Equal(ErrorCodes.Conflict, (await service.RemoveAssetAsync("logo", 99, default)).ErrorCode);
        Assert.Equal(ErrorCodes.Conflict, (await service.UploadAssetAsync("logo", TestImages.Png, "image/png", 99, default)).ErrorCode);

        // A save conflict during upload removes the file that was just written.
        store.NextSaveFailure = new ConcurrencyConflictException();
        var conflicted = await service.UploadAssetAsync("logo", TestImages.Png, "image/png", 1, default);
        Assert.Equal(ErrorCodes.Conflict, conflicted.ErrorCode);
        Assert.Empty(assets.Files);

        var profile = (await service.GetAsync(default));
        var uploaded = await service.UploadAssetAsync("logo", TestImages.Jpeg, "image/jpeg", profile.Version, default);
        Assert.True(uploaded.Value!.HasLogo);
        var replaced = await service.UploadAssetAsync("logo", TestImages.Png, null, uploaded.Value.Version, default);
        Assert.Single(assets.Files);
        Assert.Single(assets.Deleted, name => name.EndsWith(".jpg", StringComparison.Ordinal));
        var opened = await service.OpenAssetAsync("logo", default);
        Assert.Equal("image/png", opened!.ContentType);
        await opened.Content.DisposeAsync();

        var invalidOptions = await service.UpdateAsync(new UpdateSchoolProfileCommand("x", null, "bad", null, null, "UTC", "western", "hijri", replaced.Value!.Version), default);
        Assert.Contains(invalidOptions.FieldErrors, error => error is { Field: "SchoolType", Code: ErrorCodes.Required });
        Assert.Contains(invalidOptions.FieldErrors, error => error is { Field: "StudyType", Code: ErrorCodes.InvalidOption });
        store.NextSaveFailure = new ConcurrencyConflictException();
        Assert.Equal(ErrorCodes.Conflict, (await service.UpdateAsync(new UpdateSchoolProfileCommand("اسم", "primary", "morning", null, null, "UTC", "western", "hijri", replaced.Value.Version), default)).ErrorCode);
    }

    [Fact]
    public async Task DashboardAndContextWorkBeforeAnyData()
    {
        var store = new FakeDataStore();
        store.Add(SchoolProfile.CreateDefault(Now));
        var summary = await new DashboardService(store).GetSummaryAsync(default);
        Assert.All(summary.Checklist, item => Assert.False(item.Done));
        var context = await new SchoolContextService(store).GetAsync(default);
        Assert.Null(context.CurrentYear);
        Assert.Equal("arabicIndic", context.NumeralSystem);
        Assert.False(await new YearStructureService(store).HasStructureAsync(1, default));
        await new YearStructureService(store).CopyAsync(1, 2, default);
    }

    [Fact]
    public void InputHelpersParseAndReportFormats()
    {
        var errors = new InputErrors();
        Assert.Equal(new TimeOnly(8, 5), errors.Time("08:05", "Start"));
        Assert.Equal(default, errors.Time("8 am", "End"));
        Assert.Equal(default, errors.Time(null, "Missing"));
        Assert.Null(errors.OptionalDate(" ", "From"));
        Assert.Equal(new DateOnly(2026, 1, 2), errors.OptionalDate("2026-01-02", "To"));
        Assert.Null(errors.OptionalDate("02/01/2026", "Bad"));
        Assert.Equal(
            [("End", ErrorCodes.InvalidTime), ("Missing", ErrorCodes.Required), ("Bad", ErrorCodes.InvalidDate)],
            errors.Errors.Select(error => (error.Field, error.Code)));
        Assert.Equal("08:05", InputParsing.Format(new TimeOnly(8, 5)));
        Assert.Null(InputParsing.Format((DateOnly?)null));
        Assert.Equal("2026-01-02", InputParsing.Format((DateOnly?)new DateOnly(2026, 1, 2)));

        Assert.Throws<InvalidOperationException>(() => OperationResult.Success(1).Cast<string>());
        var cast = OperationResult.Invalid<int>("Name", ErrorCodes.Required).Cast<string>();
        Assert.Equal(ErrorCodes.ValidationFailed, cast.ErrorCode);
        Assert.Single(cast.FieldErrors);
    }

    [Fact]
    public void ExceptionsExposeStandardConstructors()
    {
        var inner = new InvalidOperationException("inner");
        Assert.Same(inner, new ConcurrencyConflictException("m", inner).InnerException);
        Assert.Equal("m", new ConcurrencyConflictException("m").Message);
        Assert.NotNull(new ConcurrencyConflictException());
        Assert.Same(inner, new DataConflictException("m", inner).InnerException);
        Assert.Equal("m", new DataConflictException("m").Message);
        Assert.NotNull(new DataConflictException());
        Assert.Empty(new DomainValidationException().Errors);
        Assert.Empty(new DomainValidationException("m").Errors);
        Assert.Same(inner, new DomainValidationException("m", inner).InnerException);
        Assert.Single(new DomainValidationException("Name", DomainErrorCode.Required).Errors);
    }
}
