using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Setup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Tests.Phase2;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase25;

/// <summary>
/// The stages a school type may hold (fix 3): ابتدائية, متوسطة, إعدادية and ثانوية (متوسطة + إعدادية). The school's
/// type in its profile is the only source; a template request with a stage outside it is refused, and changing the
/// type never deletes a stage.
/// </summary>
public sealed class SchoolTypeStagesTests
{
    private static readonly string[] Primary = ["الأول الابتدائي", "الثاني الابتدائي", "الثالث الابتدائي", "الرابع الابتدائي", "الخامس الابتدائي", "السادس الابتدائي"];
    private static readonly string[] Intermediate = ["الأول المتوسط", "الثاني المتوسط", "الثالث المتوسط"];
    private static readonly string[] Preparatory = ["الرابع العلمي", "الرابع الأدبي", "الخامس العلمي", "الخامس الأدبي", "السادس العلمي", "السادس الأدبي"];
    private static readonly int[] WorkingDays = [7, 1, 2, 3, 4];
    private static readonly string[] NoBranches = [];
    private static readonly string[] UnknownBranch = ["arts"];

    public static TheoryData<string, string[]> Expected() => new()
    {
        { "primary", Primary },
        { "intermediate", Intermediate },
        { "preparatory", Preparatory },
        { "secondary", [.. Intermediate, .. Preparatory] },
    };

    /// <summary>Local views of the API shapes, so the test reads the JSON contract rather than the C# types.</summary>
    private sealed record BranchTemplateView(string Key, string Name);

    private sealed record GradeTemplateView(string Key, string Name, string? BranchStem, List<string> SchoolTypes);

    private sealed record Catalog(List<BranchTemplateView> Branches, List<GradeTemplateView> Grades);

    private sealed record AllowedStage(string Key, string Name, string GradeKey, string? BranchKey);

    private sealed record OutOfType(long Id, string Name, int Version);

    private sealed record School(TestHost Host, string Token, string Root, long ShiftId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Host.DisposeAsync();
    }

    /// <summary>An empty database with the owner, the school step (type), the year and one morning shift.</summary>
    private static async Task<School> SchoolAsync(string schoolType)
    {
        var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-wizard/school", new { name = "مدرسة اختبار 01", schoolType, shiftMode = "morning" }, token));
        await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-wizard/year", new { label = "2026-2027", startDate = "2026-09-01", endDate = "2027-06-30" }, token));
        await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-wizard/timing", new
        {
            days = WorkingDays,
            weekStartDay = 7,
            shifts = new[] { new { kind = "morning", firstStartTime = "08:00", lessonMinutes = 45, lessonCount = 6 } },
        }, token));
        var year = (await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync("/api/v1/academic-years/"))).Items.Single();
        var root = $"/api/v1/academic-years/{year.Id}";
        var shift = (await ReadAsync<PagedResult<ShiftDto>>(await host.Client.GetAsync($"{root}/shifts/?pageSize=100"))).Items.Single();
        return new School(host, token, root, shift.Id);
    }

    /// <summary>The request the stage panel sends: grades grouped from the chosen stages of the allowed list.</summary>
    private static AllowedStage[] StagesFor(Catalog catalog, string schoolType) =>
        catalog.Grades
            .Where(grade => grade.SchoolTypes.Contains(schoolType))
            .SelectMany(grade => grade.BranchStem is null
                ? [new AllowedStage(grade.Key, grade.Name, grade.Key, null)]
                : catalog.Branches.Select(branch => new AllowedStage(
                    $"{grade.Key}-{branch.Key}",
                    $"{grade.BranchStem} {branch.Name}",
                    grade.Key,
                    branch.Key)))
            .ToArray();

    private static object Request(string? schoolType, IEnumerable<AllowedStage> stages, long shiftId, int sections = 1) => new
    {
        schoolType,
        grades = stages.GroupBy(stage => stage.GradeKey).Select(group => new
        {
            gradeKey = group.Key,
            branches = group.Where(stage => stage.BranchKey is not null).Select(stage => stage.BranchKey).ToArray(),
            sections,
            shiftId,
        }).ToArray(),
    };

    private static async Task<string[]> StageNamesAsync(School school) =>
        (await ReadAsync<List<StageCardDto>>(await school.Host.Client.GetAsync($"{school.Root}/stage-cards"))).Select(card => card.Stage.Name).ToArray();

    [Theory]
    [MemberData(nameof(Expected))]
    public async Task ApplyingTheTemplateOnAnEmptyDatabaseCreatesOnlyTheTypesStages(string schoolType, string[] expected)
    {
        await using var school = await SchoolAsync(schoolType);
        var catalog = await ReadAsync<Catalog>(await school.Host.Client.GetAsync("/api/v1/templates/"));
        var stages = StagesFor(catalog, schoolType);
        Assert.Equal(expected, stages.Select(stage => stage.Name)); // the list the UI shows

        var plan = await ReadAsync<StagePlanDto>(await school.Host.PostAsync($"{school.Root}/templates/stages", Request(schoolType, stages, school.ShiftId), school.Token));
        Assert.Equal(expected, plan.Lines.Select(line => line.Name));
        Assert.All(plan.Lines, line => Assert.Equal("create", line.Action));
        Assert.Equal(expected, await StageNamesAsync(school));
    }

    [Theory]
    [InlineData("preparatory", "intermediate-1")]
    [InlineData("preparatory", "primary-1")]
    [InlineData("primary", "preparatory-4")]
    [InlineData("intermediate", "preparatory-6")]
    [InlineData("secondary", "primary-6")]
    public async Task AStageOutsideTheSchoolTypeIsRefusedAndNothingIsCreated(string schoolType, string gradeKey)
    {
        await using var school = await SchoolAsync(schoolType);
        var request = new { schoolType, grades = new object[] { new { gradeKey, branches = new[] { "scientific" }, sections = 1, shiftId = school.ShiftId } } };
        await AssertApiErrorAsync(await school.Host.PostAsync($"{school.Root}/templates/stages/preview", request, school.Token), "STAGE_NOT_IN_SCHOOL_TYPE");
        await AssertApiErrorAsync(await school.Host.PostAsync($"{school.Root}/templates/stages", request, school.Token), "STAGE_NOT_IN_SCHOOL_TYPE");
        Assert.Empty(await StageNamesAsync(school));
    }

    [Fact]
    public async Task ABranchedGradeNeedsAKnownBranch()
    {
        await using var school = await SchoolAsync("preparatory");
        foreach (var branches in new[] { NoBranches, UnknownBranch })
        {
            var request = new { grades = new object[] { new { gradeKey = "preparatory-4", branches, sections = 1, shiftId = school.ShiftId } } };
            await AssertApiErrorAsync(await school.Host.PostAsync($"{school.Root}/templates/stages", request, school.Token), "STAGE_NOT_IN_SCHOOL_TYPE");
        }
        Assert.Empty(await StageNamesAsync(school));
    }

    /// <summary>
    /// The owner's report: school step = إعدادية, then the stage template applied. A request whose own school type
    /// disagrees with the profile (the panel's separate selector, or its «متوسطة» fallback) used to create
    /// الأول/الثاني/الثالث المتوسط; the profile's type now decides, so only preparatory stages can exist.
    /// </summary>
    [Fact]
    public async Task OwnerScenarioPreparatorySchoolEndsWithPreparatoryStagesOnly()
    {
        await using var school = await SchoolAsync("preparatory");
        var catalog = await ReadAsync<Catalog>(await school.Host.Client.GetAsync("/api/v1/templates/"));

        var stale = Request("intermediate", StagesFor(catalog, "intermediate"), school.ShiftId);
        await AssertApiErrorAsync(await school.Host.PostAsync($"{school.Root}/templates/stages", stale, school.Token), "STAGE_NOT_IN_SCHOOL_TYPE");
        Assert.Empty(await StageNamesAsync(school));

        await ReadAsync<StagePlanDto>(await school.Host.PostAsync($"{school.Root}/templates/stages", Request("preparatory", StagesFor(catalog, "preparatory"), school.ShiftId), school.Token));
        var stageNames = await StageNamesAsync(school);
        Assert.Equal(Preparatory, stageNames);
        Assert.Empty(await ReadAsync<List<OutOfType>>(await school.Host.Client.GetAsync($"{school.Root}/templates/stages/out-of-type")));
    }

    [Fact]
    public async Task ChangingTheSchoolTypeDeletesNothingAndListsTheStagesOutsideIt()
    {
        await using var school = await SchoolAsync("secondary");
        var catalog = await ReadAsync<Catalog>(await school.Host.Client.GetAsync("/api/v1/templates/"));
        var secondaryStages = StagesFor(catalog, "secondary");
        await ReadAsync<StagePlanDto>(await school.Host.PostAsync($"{school.Root}/templates/stages", Request("secondary", secondaryStages, school.ShiftId, sections: 0), school.Token));

        await ReadAsync<SetupProgressDto>(await school.Host.PutAsync("/api/v1/setup-wizard/school", new { name = "مدرسة اختبار 01", schoolType = "preparatory", shiftMode = "morning" }, school.Token));
        var stageNames = await StageNamesAsync(school);
        Assert.Equal([.. Intermediate, .. Preparatory], stageNames); // nothing deleted
        var outside = await ReadAsync<List<OutOfType>>(await school.Host.Client.GetAsync($"{school.Root}/templates/stages/out-of-type"));
        Assert.Equal(Intermediate, outside.Select(stage => stage.Name));

        await ReadAsync<SetupProgressDto>(await school.Host.PutAsync("/api/v1/setup-wizard/school", new { name = "مدرسة اختبار 01", schoolType = "primary", shiftMode = "morning" }, school.Token));
        stageNames = await StageNamesAsync(school);
        Assert.Equal(9, stageNames.Length);
        Assert.Equal(9, (await ReadAsync<List<OutOfType>>(await school.Host.Client.GetAsync($"{school.Root}/templates/stages/out-of-type"))).Count);

        // Archiving (soft, the owner's explicit choice) removes a stage from the list; it is never deleted.
        var first = outside[0];
        await ReadAsync<StageDto>(await school.Host.PostAsync($"{school.Root}/stages/{first.Id}/archive", new { version = first.Version }, school.Token));
        Assert.Equal(8, (await ReadAsync<List<OutOfType>>(await school.Host.Client.GetAsync($"{school.Root}/templates/stages/out-of-type"))).Count);
    }
}
