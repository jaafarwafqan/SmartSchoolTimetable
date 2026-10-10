using System.Net;
using System.Text.Json;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Tests.Phase4;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;
using static SmartSchoolTimetable.Tests.Phase4.GenerationApiTests;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>
/// M1 on a real host: the version lifecycle (drafts, approval archives the previous approved version, immutable archived
/// versions, only valid transitions), rollback as a NEW version, comparing versions, and the audit entry of every action.
/// </summary>
[Collection(SerialSolverRuns.Name)]
public sealed class LifecycleApiTests
{
    internal sealed record Fixture(TestHost Host, string Token, string Root, long YearId, long StageId, long FirstId, List<GridLesson> Lessons);

    private static GridLesson ToLesson(JsonElement item) => new(item.GetProperty("sectionId").GetInt64(), item.GetProperty("lineId").GetInt64(),
        item.GetProperty("subjectId").GetInt64(), item.GetProperty("teacherId").GetInt64(), item.GetProperty("day").GetInt32(), item.GetProperty("lesson").GetInt32());

    /// <summary>A ready school with one generated version (id, lessons).</summary>
    internal static async Task<Fixture> GeneratedAsync(TestHost host)
    {
        var (school, yearId) = await ReadySchoolAsync(host);
        var started = await host.PostAsync($"{school.Root}/generation/runs", new { timeLimitSeconds = 10, deterministic = true, seed = 42 }, school.Token);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        var run = await WaitForEndAsync(host, (await JsonAsync(started)).GetProperty("id").GetInt64());
        Assert.Equal("completed", run.GetProperty("status").GetString());
        var versionId = run.GetProperty("timetableVersionId").GetInt64();
        var timetable = await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{versionId}"));
        return new Fixture(host, school.Token, school.Root, yearId, school.Stage.Id, versionId, timetable.GetProperty("lessons").EnumerateArray().Select(ToLesson).ToList());
    }

    internal static async Task<JsonElement> SummaryAsync(TestHost host, long id) =>
        (await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{id}"))).GetProperty("summary");

    internal static async Task<long> SaveEditAsync(Fixture fixture, long parentId, IEnumerable<GridLesson> lessons, string? note = null)
    {
        var saved = await fixture.Host.PostAsync($"/api/v1/timetables/{parentId}/edits", new { lessons, note }, fixture.Token);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        return (await JsonAsync(saved)).GetProperty("id").GetInt64();
    }

    /// <summary>Finds a lesson move the verifier accepts (this school has six lessons a day, so one exists).</summary>
    internal static async Task<(GridLesson From, GridLesson To)> ValidMoveAsync(Fixture fixture, long versionId)
    {
        foreach (var lesson in fixture.Lessons)
        {
            for (var day = 1; day <= 7; day++)
            {
                for (var number = 1; number <= 6; number++)
                {
                    if ((day, number) == (lesson.Day, lesson.Lesson))
                        continue;
                    var moved = lesson with { Day = day, Lesson = number };
                    var edited = fixture.Lessons.Select(item => item == lesson ? moved : item).ToArray();
                    var check = await JsonAsync(await fixture.Host.PostAsync($"/api/v1/timetables/{versionId}/check", new { lessons = edited }, fixture.Token));
                    if (check.GetProperty("violations").GetArrayLength() == 0)
                        return (lesson, moved);
                }
            }
        }
        throw new InvalidOperationException("No valid move exists in the test school.");
    }

    private static Task<HttpResponseMessage> Approve(Fixture fixture, long id, int version) => fixture.Host.PostAsync($"/api/v1/timetables/{id}/approve", new { version }, fixture.Token);

    private static Task<HttpResponseMessage> Archive(Fixture fixture, long id, int version) => fixture.Host.PostAsync($"/api/v1/timetables/{id}/archive", new { version }, fixture.Token);

    private static async Task<(string Status, bool Approved, int Version)> StateAsync(TestHost host, long id)
    {
        var summary = await SummaryAsync(host, id);
        return (summary.GetProperty("status").GetString()!, summary.GetProperty("isApproved").GetBoolean(), summary.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task OnlyValidTransitionsSucceedAndApprovingArchivesThePreviousApprovedVersion()
    {
        await using var host = new TestHost();
        var fixture = await GeneratedAsync(host);
        var first = fixture.FirstId;
        var second = await SaveEditAsync(fixture, first, fixture.Lessons);
        var third = await SaveEditAsync(fixture, first, fixture.Lessons);
        Assert.Equal("draft", (await StateAsync(host, first)).Status);

        var approvedFirst = await JsonAsync(await Approve(fixture, first, (await StateAsync(host, first)).Version));
        Assert.Equal(("approved", true), (approvedFirst.GetProperty("status").GetString(), approvedFirst.GetProperty("isApproved").GetBoolean()));

        // Approving the same version again is not a transition.
        await AssertApiErrorAsync(await Approve(fixture, first, (await StateAsync(host, first)).Version), ErrorCodes.TimetableInvalidTransition);

        // Approving another version archives the first one; exactly one version stays approved.
        var firstVersionBefore = (await StateAsync(host, first)).Version;
        Assert.Equal("approved", (await JsonAsync(await Approve(fixture, second, (await StateAsync(host, second)).Version))).GetProperty("status").GetString());
        var archived = await StateAsync(host, first);
        Assert.Equal(("archived", false), (archived.Status, archived.Approved));
        Assert.Equal(firstVersionBefore + 1, archived.Version);
        var all = await JsonAsync(await host.Client.GetAsync($"{fixture.Root}/timetables"));
        Assert.Single(all.EnumerateArray(), item => item.GetProperty("isApproved").GetBoolean());
        Assert.NotNull((await SummaryAsync(host, first)).GetProperty("archivedAt").GetString());

        // An archived version can neither be approved nor archived again; a stale token is a conflict.
        await AssertApiErrorAsync(await Approve(fixture, first, archived.Version), ErrorCodes.TimetableInvalidTransition);
        await AssertApiErrorAsync(await Archive(fixture, first, archived.Version), ErrorCodes.TimetableInvalidTransition);
        await AssertApiErrorAsync(await Archive(fixture, first, 99), ErrorCodes.Conflict);
        await AssertApiErrorAsync(await Archive(fixture, 999_999, 0), ErrorCodes.NotFound);

        // A draft can be archived (retired) and then never approved; an approved version can be archived explicitly.
        Assert.Equal("archived", (await JsonAsync(await Archive(fixture, third, (await StateAsync(host, third)).Version))).GetProperty("status").GetString());
        await AssertApiErrorAsync(await Approve(fixture, third, (await StateAsync(host, third)).Version), ErrorCodes.TimetableInvalidTransition);
        Assert.Equal("archived", (await JsonAsync(await Archive(fixture, second, (await StateAsync(host, second)).Version))).GetProperty("status").GetString());
        all = await JsonAsync(await host.Client.GetAsync($"{fixture.Root}/timetables"));
        Assert.DoesNotContain(all.EnumerateArray(), item => item.GetProperty("isApproved").GetBoolean());
    }

    [Fact]
    public async Task RollbackCreatesANewDraftVersionAndNeverChangesTheOlderOneOrTheApprovedOne()
    {
        await using var host = new TestHost();
        var fixture = await GeneratedAsync(host);
        var (from, to) = await ValidMoveAsync(fixture, fixture.FirstId);
        var edited = await SaveEditAsync(fixture, fixture.FirstId, fixture.Lessons.Select(item => item == from ? to : item), "نقل حصة");
        Assert.Equal("approved", (await JsonAsync(await Approve(fixture, edited, (await StateAsync(host, edited)).Version))).GetProperty("status").GetString());
        var firstBefore = await StateAsync(host, fixture.FirstId);

        var rolled = await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/rollback", new { version = firstBefore.Version, note = "العودة" }, fixture.Token);
        Assert.Equal(HttpStatusCode.Created, rolled.StatusCode);
        var created = await JsonAsync(rolled);
        Assert.Equal(("rolledBack", fixture.FirstId, 3, "draft", false), (created.GetProperty("source").GetString(), created.GetProperty("parentVersionId").GetInt64(),
            created.GetProperty("number").GetInt32(), created.GetProperty("status").GetString(), created.GetProperty("isApproved").GetBoolean()));
        Assert.Equal("العودة", created.GetProperty("note").GetString());

        // The restored timetable has the older version's lessons; the older version and the approved one are untouched.
        var restored = await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{created.GetProperty("id").GetInt64()}"));
        Assert.Equal(fixture.Lessons.Order(Comparer<GridLesson>.Create(Compare)), restored.GetProperty("lessons").EnumerateArray().Select(ToLesson).Order(Comparer<GridLesson>.Create(Compare)));
        Assert.Equal(firstBefore, await StateAsync(host, fixture.FirstId));
        Assert.Equal("approved", (await StateAsync(host, edited)).Status);

        // An archived version can be restored too; a stale token or an unknown version is refused.
        Assert.Equal("archived", (await JsonAsync(await Archive(fixture, fixture.FirstId, firstBefore.Version))).GetProperty("status").GetString());
        var again = await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/rollback", new { version = firstBefore.Version + 1 }, fixture.Token);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        await AssertApiErrorAsync(await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/rollback", new { version = 0 }, fixture.Token), ErrorCodes.Conflict);
        await AssertApiErrorAsync(await host.PostAsync("/api/v1/timetables/999999/rollback", new { version = 0 }, fixture.Token), ErrorCodes.NotFound);
        await AssertApiErrorAsync(await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/rollback", new { version = firstBefore.Version + 1, note = new string('م', 201) }, fixture.Token),
            ErrorCodes.ValidationFailed);
    }

    private static int Compare(GridLesson left, GridLesson right) =>
        (left.SectionId, left.Day, left.Lesson, left.LineId).CompareTo((right.SectionId, right.Day, right.Lesson, right.LineId));

    [Fact]
    public async Task ComparingVersionsReportsTheMovedLessonPerSectionAndPerTeacher()
    {
        await using var host = new TestHost();
        var fixture = await GeneratedAsync(host);
        var (from, to) = await ValidMoveAsync(fixture, fixture.FirstId);
        var edited = await SaveEditAsync(fixture, fixture.FirstId, fixture.Lessons.Select(item => item == from ? to : item));

        var comparison = await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{fixture.FirstId}/compare/{edited}"));
        var totals = comparison.GetProperty("totals");
        Assert.Equal((0, 0, 1, 0, fixture.Lessons.Count - 1), (totals.GetProperty("added").GetInt32(), totals.GetProperty("removed").GetInt32(), totals.GetProperty("moved").GetInt32(),
            totals.GetProperty("reassigned").GetInt32(), totals.GetProperty("unchanged").GetInt32()));
        var change = comparison.GetProperty("changes").EnumerateArray().Single();
        Assert.Equal(("moved", from.SectionId, from.LineId, from.SubjectId, from.TeacherId), (change.GetProperty("kind").GetString(), change.GetProperty("sectionId").GetInt64(),
            change.GetProperty("lineId").GetInt64(), change.GetProperty("subjectId").GetInt64(), change.GetProperty("fromTeacherId").GetInt64()));
        Assert.Equal((from.Day, from.Lesson, to.Day, to.Lesson), (change.GetProperty("fromDay").GetInt32(), change.GetProperty("fromLesson").GetInt32(),
            change.GetProperty("toDay").GetInt32(), change.GetProperty("toLesson").GetInt32()));
        Assert.Equal(1, comparison.GetProperty("sections").EnumerateArray().Single().GetProperty("moved").GetInt32());
        Assert.Equal(from.TeacherId, comparison.GetProperty("teachers").EnumerateArray().Single().GetProperty("teacherId").GetInt64());
        Assert.Equal((1, 2), (comparison.GetProperty("fromNumber").GetInt32(), comparison.GetProperty("toNumber").GetInt32()));

        // The other direction is the inverse move; the same version has no changes; an unknown version is not found.
        var reverse = (await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{edited}/compare/{fixture.FirstId}"))).GetProperty("changes").EnumerateArray().Single();
        Assert.Equal((to.Day, to.Lesson, from.Day, from.Lesson), (reverse.GetProperty("fromDay").GetInt32(), reverse.GetProperty("fromLesson").GetInt32(),
            reverse.GetProperty("toDay").GetInt32(), reverse.GetProperty("toLesson").GetInt32()));
        Assert.Equal(0, (await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{edited}/compare/{edited}"))).GetProperty("changes").GetArrayLength());
        await AssertApiErrorAsync(await host.Client.GetAsync($"/api/v1/timetables/{edited}/compare/999999"), ErrorCodes.NotFound);
    }

    [Fact]
    public async Task EveryLifecycleActionWritesAnAuditEntryWithItsParametersNewestFirst()
    {
        await using var host = new TestHost();
        var fixture = await GeneratedAsync(host);
        var edited = await SaveEditAsync(fixture, fixture.FirstId, fixture.Lessons);
        await Approve(fixture, fixture.FirstId, (await StateAsync(host, fixture.FirstId)).Version);
        await Approve(fixture, edited, (await StateAsync(host, edited)).Version);
        await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/rollback", new { version = (await StateAsync(host, fixture.FirstId)).Version }, fixture.Token);

        var page = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=timetable&pageSize=100"));
        var entries = page.GetProperty("items").EnumerateArray().ToArray();
        string[] types = [.. entries.Select(entry => entry.GetProperty("eventType").GetString()!)];
        // Newest first: rollback, archive of the first version by the second approval, approvals, edit, generation.
        Assert.Equal(["TimetableRolledBack", "TimetableApproved", "TimetableArchived", "TimetableApproved", "TimetableEdited", "TimetableGenerated"], types);
        Assert.All(entries, entry => Assert.Equal("timetable", entry.GetProperty("category").GetString()));
        Assert.Equal(3, entries[0].GetProperty("params").GetProperty("number").GetInt32());
        Assert.Equal(1, entries[0].GetProperty("params").GetProperty("from").GetInt32());
        Assert.True(entries[2].GetProperty("params").GetProperty("byApproval").GetBoolean());
        Assert.Equal(2, entries[4].GetProperty("params").GetProperty("number").GetInt32());
        var ids = entries.Select(entry => entry.GetProperty("id").GetInt64()).ToArray();
        Assert.Equal(ids.OrderByDescending(id => id), ids);

        // A generation run is audited under its own group, with the real status.
        var generation = await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=generation"));
        Assert.Equal(["GenerationFinished", "GenerationStarted"], generation.GetProperty("items").EnumerateArray().Select(entry => entry.GetProperty("eventType").GetString()));
        Assert.Equal("completed", generation.GetProperty("items")[0].GetProperty("params").GetProperty("status").GetString());
    }
}
