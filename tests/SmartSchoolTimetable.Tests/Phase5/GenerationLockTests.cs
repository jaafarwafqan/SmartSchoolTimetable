using System.Net;
using System.Text.Json;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Tests.Phase4;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;
using static SmartSchoolTimetable.Tests.Phase4.GenerationApiTests;
using static SmartSchoolTimetable.Tests.Phase5.LifecycleApiTests;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>
/// M1: regenerating over manual edits. «إبقاء تعديلاتي» keeps the lessons the owner moved by hand exactly where they are;
/// leaving the option out discards them (a normal generation); a lock that no longer fits is dropped and reported.
/// </summary>
[Collection(SerialSolverRuns.Name)]
public sealed class GenerationLockTests
{
    private static async Task<JsonElement> RunAsync(Fixture fixture, object body)
    {
        var started = await fixture.Host.PostAsync($"{fixture.Root}/generation/runs", body, fixture.Token);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        return await WaitForEndAsync(fixture.Host, (await JsonAsync(started)).GetProperty("id").GetInt64());
    }

    private static async Task<GridLesson[]> LessonsOfAsync(TestHost host, long versionId) =>
        [.. (await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{versionId}"))).GetProperty("lessons").EnumerateArray().Select(item => new GridLesson(
            item.GetProperty("sectionId").GetInt64(), item.GetProperty("lineId").GetInt64(), item.GetProperty("subjectId").GetInt64(),
            item.GetProperty("teacherId").GetInt64(), item.GetProperty("day").GetInt32(), item.GetProperty("lesson").GetInt32()))
            .OrderBy(lesson => (lesson.SectionId, lesson.Day, lesson.Lesson, lesson.LineId))];

    [Fact]
    public async Task OnlyAManualEditHasEditsToKeepAndItIsReportedForTheLatestVersion()
    {
        await using var host = new TestHost();
        var fixture = await GeneratedAsync(host);
        var none = await JsonAsync(await host.Client.GetAsync($"{fixture.Root}/generation/manual-edits"));
        Assert.Equal(JsonValueKind.Null, none.GetProperty("edits").ValueKind);

        var (from, to) = await ValidMoveAsync(fixture, fixture.FirstId);
        var edited = await SaveEditAsync(fixture, fixture.FirstId, fixture.Lessons.Select(item => item == from ? to : item));
        var edits = (await JsonAsync(await host.Client.GetAsync($"{fixture.Root}/generation/manual-edits"))).GetProperty("edits");
        Assert.Equal((edited, 2, 1), (edits.GetProperty("versionId").GetInt64(), edits.GetProperty("number").GetInt32(), edits.GetProperty("lessons").GetInt32()));

        // Restoring the generated version is not a manual edit: nothing to keep any more.
        var rolled = await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/rollback", new { version = (await StateOf(host, fixture.FirstId)) }, fixture.Token);
        Assert.Equal(HttpStatusCode.Created, rolled.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await JsonAsync(await host.Client.GetAsync($"{fixture.Root}/generation/manual-edits"))).GetProperty("edits").ValueKind);
    }

    private static async Task<int> StateOf(TestHost host, long id) => (await SummaryAsync(host, id)).GetProperty("version").GetInt32();

    [Fact]
    public async Task KeepingTheEditsPlacesTheMovedLessonWhereTheOwnerPutItAndDiscardingRestoresTheNormalResult()
    {
        await using var host = new TestHost();
        var fixture = await GeneratedAsync(host);
        var (from, to) = await ValidMoveAsync(fixture, fixture.FirstId);
        var edited = await SaveEditAsync(fixture, fixture.FirstId, fixture.Lessons.Select(item => item == from ? to : item));

        // Keep: the new version has the lesson at the moved slot, and the run says how many were locked.
        var kept = await RunAsync(fixture, new { timeLimitSeconds = 10, deterministic = true, seed = 42, lockFromVersionId = edited });
        Assert.Equal("completed", kept.GetProperty("status").GetString());
        Assert.Equal((edited, 1, 0), (kept.GetProperty("lockedFromVersionId").GetInt64(), kept.GetProperty("lockedLessons").GetInt32(), kept.GetProperty("locksDropped").GetInt32()));
        var keptLessons = await LessonsOfAsync(host, kept.GetProperty("timetableVersionId").GetInt64());
        Assert.Contains(keptLessons, lesson => lesson == to);
        Assert.Equal(fixture.Lessons.Count, keptLessons.Length);
        Assert.Equal(0, (await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{kept.GetProperty("timetableVersionId").GetInt64()}"))).GetProperty("violations").GetInt32());

        // Discard: the same seed and input give the first timetable again; the manual lesson is not kept.
        var discarded = await RunAsync(fixture, new { timeLimitSeconds = 10, deterministic = true, seed = 42 });
        Assert.Equal((0, 0), (discarded.GetProperty("lockedLessons").GetInt32(), discarded.GetProperty("locksDropped").GetInt32()));
        Assert.Equal(JsonValueKind.Null, discarded.GetProperty("lockedFromVersionId").ValueKind);
        var discardedLessons = await LessonsOfAsync(host, discarded.GetProperty("timetableVersionId").GetInt64());
        Assert.Equal(fixture.Lessons.OrderBy(lesson => (lesson.SectionId, lesson.Day, lesson.Lesson, lesson.LineId)), discardedLessons);

        // The start is audited with the number of kept lessons.
        var started = (await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?eventType=GenerationStarted"))).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal([0, 1, 0], started.Select(entry => entry.GetProperty("params").GetProperty("locked").GetInt32()).ToArray());
    }

    [Fact]
    public async Task AnUnknownOrForeignVersionToLockFromIsRefused()
    {
        await using var host = new TestHost();
        var fixture = await GeneratedAsync(host);
        var refused = await host.PostAsync($"{fixture.Root}/generation/runs", new { timeLimitSeconds = 10, lockFromVersionId = 999_999 }, fixture.Token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var error = await JsonAsync(refused);
        Assert.Equal("lockFromVersionId", error.GetProperty("errors")[0].GetProperty("field").GetString());
    }

    [Fact]
    public async Task AManualLessonWhoseAssignmentChangedIsDroppedAndReported()
    {
        await using var host = new TestHost();
        var fixture = await GeneratedAsync(host);
        var (from, to) = await ValidMoveAsync(fixture, fixture.FirstId);
        var edited = await SaveEditAsync(fixture, fixture.FirstId, fixture.Lessons.Select(item => item == from ? to : item));

        // Give the moved lesson's line to the other teacher: the locked (section, line, teacher) no longer exists.
        var created = await host.PostAsync("/api/v1/teachers/", new
        {
            fullName = "عباس حسين كاظم", shortName = "عباس", offDays = Array.Empty<int>(), blockedPeriods = Array.Empty<object>(), fullyReleased = false,
            releaseReason = (string?)null, releaseFrom = (string?)null, releaseTo = (string?)null, maxLessonsPerDay = (int?)null, maxLessonsPerWeek = (int?)null,
            notes = (string?)null, version = 0, specializationIds = new[] { from.SubjectId },
        }, fixture.Token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var other = (await JsonAsync(created)).GetProperty("id").GetInt64();
        var matrix = await JsonAsync(await host.Client.GetAsync($"{fixture.Root}/workload/matrix?stageId={fixture.StageId}"));
        var current = matrix.GetProperty("stage").GetProperty("sections").EnumerateArray().Single(row => row.GetProperty("sectionId").GetInt64() == from.SectionId)
            .GetProperty("cells").EnumerateArray().Single(cell => cell.GetProperty("entryId").GetInt64() == from.LineId);
        var cell = await host.PutAsync($"{fixture.Root}/workload/cell", new
        {
            sectionId = from.SectionId, entryId = from.LineId, teacherId = other,
            assignmentId = current.GetProperty("assignmentId").GetInt64(), version = current.GetProperty("version").GetInt32(),
        }, fixture.Token);
        Assert.True(cell.StatusCode == HttpStatusCode.OK, $"{(int)cell.StatusCode}: {await cell.Content.ReadAsStringAsync()}");

        var run = await RunAsync(fixture, new { timeLimitSeconds = 10, deterministic = true, seed = 42, lockFromVersionId = edited });
        Assert.Equal("completed", run.GetProperty("status").GetString());
        Assert.Equal((1, 1), (run.GetProperty("lockedLessons").GetInt32(), run.GetProperty("locksDropped").GetInt32()));
    }
}
