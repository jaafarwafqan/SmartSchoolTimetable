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
/// MF11 on a real host: a saved version is checked against TODAY's school data without changing it; what changed is listed;
/// approval is blocked on conflicts; «إصلاح بأقل تغيير» moves only the lessons that conflict; «استبدال المعلم» swaps a new teacher
/// into the same slots when nothing breaks; both create a new draft and an audit entry.
/// </summary>
[Collection(SerialSolverRuns.Name)]
public sealed class CurrentDataTests
{
    private static async Task<JsonElement> CheckAsync(Fixture fixture, long versionId) =>
        await JsonAsync(await fixture.Host.Client.GetAsync($"/api/v1/timetables/{versionId}/current-check"));

    private static string[] Codes(JsonElement check) =>
        check.GetProperty("findings").EnumerateArray().Select(item => item.GetProperty("code").GetString()!).ToArray();

    private static async Task<JsonElement> GetTeacherAsync(TestHost host, long id) =>
        (await JsonAsync(await host.Client.GetAsync("/api/v1/teachers/?pageSize=100"))).GetProperty("items").EnumerateArray().First(item => item.GetProperty("id").GetInt64() == id);

    private static async Task UpdateTeacherAsync(TestHost host, string token, long id, int[] offDays)
    {
        var teacher = await GetTeacherAsync(host, id);
        var subject = (await JsonAsync(await host.Client.GetAsync("/api/v1/subjects/"))).GetProperty("items").EnumerateArray().First().GetProperty("id").GetInt64();
        var response = await host.PutAsync($"/api/v1/teachers/{id}", new
        {
            fullName = teacher.GetProperty("fullName").GetString(), shortName = teacher.GetProperty("shortName").GetString(), offDays,
            blockedPeriods = teacher.GetProperty("blockedPeriods"), fullyReleased = false, releaseReason = (string?)null, releaseFrom = (string?)null, releaseTo = (string?)null,
            maxLessonsPerDay = (int?)null, maxLessonsPerWeek = (int?)null, notes = (string?)null, version = teacher.GetProperty("version").GetInt32(),
            specializationIds = new[] { subject },
        }, token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<long> AddTeacherAsync(Fixture fixture, string name, int[] offDays)
    {
        var subject = (await JsonAsync(await fixture.Host.Client.GetAsync("/api/v1/subjects/"))).GetProperty("items").EnumerateArray().First().GetProperty("id").GetInt64();
        var created = await fixture.Host.PostAsync("/api/v1/teachers/", new
        {
            fullName = name, shortName = name.Split(' ')[0], offDays, blockedPeriods = Array.Empty<object>(), fullyReleased = false, releaseReason = (string?)null,
            releaseFrom = (string?)null, releaseTo = (string?)null, maxLessonsPerDay = (int?)null, maxLessonsPerWeek = (int?)null, notes = (string?)null, version = 0,
            specializationIds = new[] { subject },
        }, fixture.Token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await JsonAsync(created)).GetProperty("id").GetInt64();
    }

    private static async Task ReassignAsync(Fixture fixture, long sectionId, long teacherId)
    {
        var matrix = await JsonAsync(await fixture.Host.Client.GetAsync($"{fixture.Root}/workload/matrix?stageId={fixture.StageId}"));
        var row = matrix.GetProperty("stage").GetProperty("sections").EnumerateArray().First(item => item.GetProperty("sectionId").GetInt64() == sectionId);
        var cell = row.GetProperty("cells").EnumerateArray().First();
        var response = await fixture.Host.PutAsync($"{fixture.Root}/workload/cell", new
        {
            sectionId, entryId = cell.GetProperty("entryId").GetInt64(), teacherId, assignmentId = cell.GetProperty("assignmentId").GetInt64(), version = cell.GetProperty("version").GetInt32(),
        }, fixture.Token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<long> RepairAsync(Fixture fixture, long versionId)
    {
        var started = await fixture.Host.PostAsync($"{fixture.Root}/generation/runs", new { repairFromVersionId = versionId, timeLimitSeconds = 10, deterministic = true, seed = 7 }, fixture.Token);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        var run = await WaitForEndAsync(fixture.Host, (await JsonAsync(started)).GetProperty("id").GetInt64());
        Assert.Equal("completed", run.GetProperty("status").GetString());
        Assert.True(run.GetProperty("isRepair").GetBoolean());
        Assert.Equal(versionId, run.GetProperty("lockedFromVersionId").GetInt64());
        return run.GetProperty("timetableVersionId").GetInt64();
    }

    private static async Task<List<GridLesson>> LessonsOfAsync(TestHost host, long versionId) =>
        (await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{versionId}"))).GetProperty("lessons").EnumerateArray()
            .Select(item => new GridLesson(item.GetProperty("sectionId").GetInt64(), item.GetProperty("lineId").GetInt64(), item.GetProperty("subjectId").GetInt64(),
                item.GetProperty("teacherId").GetInt64(), item.GetProperty("day").GetInt32(), item.GetProperty("lesson").GetInt32())).ToList();

    private static async Task<string[]> AuditEventsAsync(TestHost host) =>
        (await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=timetable"))).GetProperty("items").EnumerateArray()
            .Select(entry => entry.GetProperty("eventType").GetString()!).ToArray();

    [Fact]
    public async Task UnchangedDataShowsNoWarningAndThereIsNothingToRepairOrReplace()
    {
        await using var host = new TestHost();
        var fixture = await GeneratedAsync(host);
        var check = await CheckAsync(fixture, fixture.FirstId);
        Assert.False(check.GetProperty("stale").GetBoolean());
        Assert.Empty(check.GetProperty("findings").EnumerateArray());
        Assert.Empty(check.GetProperty("changes").EnumerateArray());
        Assert.False(check.GetProperty("canRepair").GetBoolean());
        Assert.False(check.GetProperty("canReplaceTeachers").GetBoolean());
        Assert.NotEmpty(check.GetProperty("names").GetProperty("teachers").EnumerateArray());

        await AssertApiErrorAsync(await host.PostAsync($"{fixture.Root}/generation/runs", new { repairFromVersionId = fixture.FirstId }, fixture.Token), ErrorCodes.TimetableNothingToRepair);
        var version = (await SummaryAsync(host, fixture.FirstId)).GetProperty("version").GetInt32();
        await AssertApiErrorAsync(await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/replace-teachers", new { version }, fixture.Token), ErrorCodes.TimetableNothingToReplace);
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/approve", new { version }, fixture.Token)).StatusCode);
        await AssertApiErrorAsync(await host.PostAsync($"{fixture.Root}/generation/runs", new { repairFromVersionId = 99999 }, fixture.Token), ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task ABlockedDayIsShownAsConflictsBlocksApprovalAndTheRepairMovesOnlyThoseLessons()
    {
        await using var host = new TestHost();
        var fixture = await GeneratedAsync(host);
        var blocked = fixture.Lessons.GroupBy(lesson => (lesson.TeacherId, lesson.Day)).OrderByDescending(group => group.Count()).First();
        var (teacherId, day) = blocked.Key;
        var affected = blocked.ToHashSet();
        await UpdateTeacherAsync(host, fixture.Token, teacherId, [day]);

        // The saved version is untouched, its own (snapshot) check still passes, and today's data says otherwise.
        var saved = await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{fixture.FirstId}"));
        Assert.Equal(0, saved.GetProperty("violations").GetInt32());
        Assert.True(saved.GetProperty("summary").GetProperty("stale").GetBoolean());
        var check = await CheckAsync(fixture, fixture.FirstId);
        Assert.True(check.GetProperty("stale").GetBoolean());
        var findings = check.GetProperty("findings").EnumerateArray().ToArray();
        Assert.Equal(affected.Count, findings.Length);
        Assert.All(findings, item =>
        {
            Assert.Equal(ViolationCodes.TeacherUnavailable, item.GetProperty("code").GetString());
            Assert.Equal((teacherId, day), (item.GetProperty("teacherId").GetInt64(), item.GetProperty("day").GetInt32()));
        });
        var change = Assert.Single(check.GetProperty("changes").EnumerateArray());
        Assert.Equal((InputChangeCodes.TeacherOffDaysChanged, teacherId), (change.GetProperty("code").GetString(), change.GetProperty("teacherId").GetInt64()));
        Assert.Equal([day], change.GetProperty("days").EnumerateArray().Select(item => item.GetInt32()));
        Assert.True(check.GetProperty("canRepair").GetBoolean());
        Assert.False(check.GetProperty("canReplaceTeachers").GetBoolean());

        // Approval is blocked with the reason; the version stays a draft.
        var version = (await SummaryAsync(host, fixture.FirstId)).GetProperty("version").GetInt32();
        await AssertApiErrorAsync(await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/approve", new { version }, fixture.Token), ErrorCodes.TimetableConflictsWithCurrentData);
        Assert.Equal("draft", (await SummaryAsync(host, fixture.FirstId)).GetProperty("status").GetString());

        // The repair keeps every lesson that is not in conflict exactly where it was, and nothing conflicts afterwards.
        var repairedId = await RepairAsync(fixture, fixture.FirstId);
        var repaired = await LessonsOfAsync(host, repairedId);
        Assert.Equal(fixture.Lessons.Count, repaired.Count);
        var unchanged = fixture.Lessons.Where(lesson => !affected.Contains(lesson)).ToHashSet();
        Assert.True(unchanged.IsSubsetOf(repaired.ToHashSet()), "a lesson that did not conflict was moved");
        Assert.DoesNotContain(repaired, lesson => lesson.TeacherId == teacherId && lesson.Day == day);
        var summary = await SummaryAsync(host, repairedId);
        Assert.Equal(("repaired", "draft"), (summary.GetProperty("source").GetString(), summary.GetProperty("status").GetString()));
        Assert.Equal(fixture.FirstId, summary.GetProperty("parentVersionId").GetInt64());
        var after = await CheckAsync(fixture, repairedId);
        Assert.Empty(after.GetProperty("findings").EnumerateArray());
        Assert.False(after.GetProperty("stale").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/v1/timetables/{repairedId}/approve", new { version = summary.GetProperty("version").GetInt32() }, fixture.Token)).StatusCode);
        Assert.Equal(fixture.Lessons, await LessonsOfAsync(host, fixture.FirstId)); // the saved version never changed
        Assert.Contains("TimetableRepaired", await AuditEventsAsync(host));
    }

    [Fact]
    public async Task AReassignedSubjectIsReplacedDirectlyWhenNothingBreaksAndFallsBackToTheRepairWhenItDoes()
    {
        await using var host = new TestHost();
        var fixture = await GeneratedAsync(host);
        var section = fixture.Lessons[0].SectionId;
        var theirLessons = fixture.Lessons.Where(lesson => lesson.SectionId == section).ToArray();
        var newTeacher = await AddTeacherAsync(fixture, "حسين رعد سالم", []);
        await ReassignAsync(fixture, section, newTeacher);

        var check = await CheckAsync(fixture, fixture.FirstId);
        var findings = check.GetProperty("findings").EnumerateArray().ToArray();
        Assert.Equal(theirLessons.Length, findings.Length);
        Assert.All(findings, item =>
        {
            Assert.Equal(CurrentFindingCodes.TeacherReassigned, item.GetProperty("code").GetString());
            Assert.Equal(newTeacher, item.GetProperty("currentTeacherId").GetInt64());
        });
        Assert.Contains(check.GetProperty("changes").EnumerateArray(), item => item.GetProperty("code").GetString() == InputChangeCodes.AssignmentTeacherChanged
            && item.GetProperty("toTeacherId").GetInt64() == newTeacher);
        Assert.True(check.GetProperty("canReplaceTeachers").GetBoolean());
        var version = (await SummaryAsync(host, fixture.FirstId)).GetProperty("version").GetInt32();
        await AssertApiErrorAsync(await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/approve", new { version }, fixture.Token), ErrorCodes.TimetableConflictsWithCurrentData);

        // Direct replacement: same slots, new teacher, a new draft; the saved version is untouched.
        var replaced = await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/replace-teachers", new { version }, fixture.Token);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var replacedId = (await JsonAsync(replaced)).GetProperty("id").GetInt64();
        var lessons = await LessonsOfAsync(host, replacedId);
        Assert.Equal(fixture.Lessons.Count, lessons.Count);
        Assert.Equal(theirLessons.Select(lesson => (lesson.Day, lesson.Lesson)).Order(), lessons.Where(lesson => lesson.SectionId == section).Select(lesson => (lesson.Day, lesson.Lesson)).Order());
        Assert.All(lessons.Where(lesson => lesson.SectionId == section), lesson => Assert.Equal(newTeacher, lesson.TeacherId));
        Assert.Equal(fixture.Lessons.Where(lesson => lesson.SectionId != section).ToHashSet(), lessons.Where(lesson => lesson.SectionId != section).ToHashSet());
        var summary = await SummaryAsync(host, replacedId);
        Assert.Equal(("teacherReplaced", "draft"), (summary.GetProperty("source").GetString(), summary.GetProperty("status").GetString()));
        Assert.Empty((await CheckAsync(fixture, replacedId)).GetProperty("findings").EnumerateArray());
        Assert.Equal(fixture.Lessons, await LessonsOfAsync(host, fixture.FirstId));
        Assert.Contains("TimetableTeacherReplaced", await AuditEventsAsync(host));

        // Now a new teacher who is off on a day the section has lessons: replacing would break a rule, so it is refused and the repair does it.
        var day = theirLessons[0].Day;
        await UpdateTeacherAsync(host, fixture.Token, newTeacher, [day]);
        var conflict = await CheckAsync(fixture, fixture.FirstId);
        Assert.False(conflict.GetProperty("canReplaceTeachers").GetBoolean());
        Assert.Contains(ViolationCodes.TeacherUnavailable, Codes(conflict));
        await AssertApiErrorAsync(await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/replace-teachers", new { version }, fixture.Token), ErrorCodes.TimetableReplaceConflict);
        var repairedId = await RepairAsync(fixture, fixture.FirstId);
        var repaired = await LessonsOfAsync(host, repairedId);
        Assert.All(repaired.Where(lesson => lesson.SectionId == section), lesson =>
        {
            Assert.Equal(newTeacher, lesson.TeacherId);
            Assert.NotEqual(day, lesson.Day);
        });
        Assert.Equal(fixture.Lessons.Where(lesson => lesson.SectionId != section).ToHashSet(), repaired.Where(lesson => lesson.SectionId != section).ToHashSet());
        Assert.Empty((await CheckAsync(fixture, repairedId)).GetProperty("findings").EnumerateArray());
    }

    [Fact]
    public void EveryCurrentDataCodeHasAnArabicSentenceOnTheScreen()
    {
        var source = File.ReadAllText(Path.Combine(TestPaths.FindRepositoryRoot(), "frontend", "src", "i18n", "ar", "currentCheck.ts"));
        var codes = ViolationCodes.All.Where(code => code != ViolationCodes.UnknownLesson).Concat(CurrentFindingCodes.All).Concat(InputChangeCodes.All).Distinct(StringComparer.Ordinal);
        Assert.All(codes, code => Assert.Contains($"{code}:", source));
        Assert.Equal(InputChangeCodes.All.Count, InputChangeCodes.All.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TheAnalyzerFreesTheConflictingLessonFirstThenItsSectionDayThenTheWholeSection()
    {
        // One section, two days of four lessons, two subjects (teachers 1 and 2); teacher 2 is now blocked on day 1, lesson 2.
        var shift = new Application.Scheduling.ShiftInput(1, "ص", [new Application.Scheduling.DayLessons(1, 4), new Application.Scheduling.DayLessons(2, 4)], 480, 720);
        var section = new Application.Scheduling.SectionInput(1, 1, 1, "س", "أ", [new Application.Scheduling.DayLessons(1, 4), new Application.Scheduling.DayLessons(2, 4)]);
        Application.Scheduling.SubjectInput Subject(long id) => new(id, "م", 0, false, false, false, false, null, []);
        Application.Scheduling.TeacherInput Teacher(long id, params Application.Scheduling.SlotRef[] blocked) => new(id, "م", false, false, false, [], blocked, null, null, [id]);
        var input = new Application.Scheduling.SchedulingInput(1, 1, [1, 2], [shift], [section], [Subject(1), Subject(2)],
            [new Application.Scheduling.LineInput(10, 1, 1, null, 3, false), new Application.Scheduling.LineInput(11, 1, 2, null, 3, false)],
            [new Application.Scheduling.AssignmentInput(1, 1, 10, 1), new Application.Scheduling.AssignmentInput(2, 1, 11, 2)],
            [Teacher(1), Teacher(2, new Application.Scheduling.SlotRef(1, 2))], [], new Application.Scheduling.ProfileInput(1, []));
        IReadOnlyList<Application.Generation.PlacedLesson> stored =
        [
            new(1, 10, 1, 1, 1), new(1, 11, 2, 1, 2), new(1, 10, 1, 1, 3), new(1, 11, 2, 1, 4),
            new(1, 10, 1, 2, 1), new(1, 11, 2, 2, 2),
        ];
        var analysis = CurrentDataAnalyzer.Analyze(input, stored, false);

        var finding = Assert.Single(analysis.Findings);
        Assert.Equal((ViolationCodes.TeacherUnavailable, 2L, 1, 2), (finding.Code, finding.TeacherId, finding.Day, finding.Lesson));
        Assert.Equal(5, analysis.Locked.Count);                  // everything but the conflicting lesson stays
        Assert.DoesNotContain(new Application.Generation.PlacedLesson(1, 11, 2, 1, 2), analysis.Locked);
        Assert.Equal(2, analysis.LockedDay.Count);               // the other lessons of that section and day are free too
        Assert.All(analysis.LockedDay, lesson => Assert.Equal(2, lesson.Day));
        Assert.Empty(analysis.LockedWide);                       // the last try frees the whole section (the only one here)
        Assert.True(analysis.HasConflicts);
        Assert.False(analysis.OnlyReassignments);

        var fits = CurrentDataAnalyzer.Analyze(input with { Teachers = [Teacher(1), Teacher(2)] }, stored, false);
        Assert.Empty(fits.Findings);
        Assert.Equal(stored.Count, fits.Locked.Count);
    }

    [Fact]
    public void TheDifferNamesEveryKindOfChangeAndAnIdenticalInputHasNone()
    {
        var teacher = new Application.Scheduling.TeacherInput(1, "أ", false, false, false, [], [], null, null, [1]);
        var input = new Application.Scheduling.SchedulingInput(1, 1, [1, 2], [], [], [], [new Application.Scheduling.LineInput(10, 1, 1, null, 4, false)],
            [new Application.Scheduling.AssignmentInput(1, 5, 10, 1)], [teacher], [], new Application.Scheduling.ProfileInput(1, []));
        Assert.Empty(InputDiff.Compare(input, input));
        var changed = input with
        {
            Lines = [new Application.Scheduling.LineInput(10, 1, 1, null, 6, false)],
            Teachers = [teacher with { OffDays = [2], MaxPerDay = 3, MaxPerWeek = 20 }],
            Assignments = [new Application.Scheduling.AssignmentInput(1, 5, 10, 2)],
            Profile = new Application.Scheduling.ProfileInput(2, []),
        };
        var codes = InputDiff.Compare(input, changed).Select(change => change.Code).ToArray();
        Assert.Contains(InputChangeCodes.AssignmentTeacherChanged, codes);
        Assert.Contains(InputChangeCodes.LessonsPerWeekChanged, codes);
        Assert.Contains(InputChangeCodes.TeacherOffDaysChanged, codes);
        Assert.Contains(InputChangeCodes.TeacherDayLimitChanged, codes);
        Assert.Contains(InputChangeCodes.TeacherWeekLimitChanged, codes);
        Assert.Contains(InputChangeCodes.PrioritiesChanged, codes);
        Assert.All(codes, code => Assert.Contains(code, InputChangeCodes.All));
    }
}
