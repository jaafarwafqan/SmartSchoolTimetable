using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Domain.Generation;
using SmartSchoolTimetable.Tests.Phase3;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// Generation, timetable and manual-edit endpoints on a real host with a temporary database (Phase 4 M2–M4):
/// authentication, validation codes, readiness gate, single active run, polling to a verified version, approval,
/// edits refused while a hard rule is broken, and start-up recovery of interrupted runs.
/// </summary>
[Collection(SerialSolverRuns.Name)]
public sealed class GenerationApiTests
{
    private static readonly string[] TeacherNames = ["أحمد علي حسن", "سعد كاظم حسن"];

    /// <summary>
    /// Two sections of one stage with math 5 a week. With one teacher for both the school passes the validator but is
    /// infeasible (both sections must start at lesson 1, H3); with one teacher per section it is timetabled.
    /// </summary>
    internal static async Task<(ReferenceProtectionTests.School School, long YearId)> ReadySchoolAsync(TestHost host, int teachers = 2, bool assign = true)
    {
        var school = await ReferenceProtectionTests.SeedAsync(host);
        var ids = new List<long>();
        foreach (var name in TeacherNames.Take(teachers))
        {
            var teacher = await host.PostAsync("/api/v1/teachers/", new
            {
                fullName = name, shortName = name.Split(' ')[0], offDays = Array.Empty<int>(), blockedPeriods = Array.Empty<object>(), fullyReleased = false,
                releaseReason = (string?)null, releaseFrom = (string?)null, releaseTo = (string?)null, maxLessonsPerDay = (int?)null, maxLessonsPerWeek = (int?)null,
                notes = (string?)null, version = 0, specializationIds = new[] { school.Subject.Id },
            }, school.Token);
            Assert.Equal(HttpStatusCode.Created, teacher.StatusCode);
            ids.Add((await JsonAsync(teacher)).GetProperty("id").GetInt64());
        }
        if (assign)
        {
            var matrix = await JsonAsync(await host.Client.GetAsync($"{school.Root}/workload/matrix?stageId={school.Stage.Id}"));
            var sections = matrix.GetProperty("stage").GetProperty("sections").EnumerateArray().Select(item => item.GetProperty("sectionId").GetInt64()).ToArray();
            for (var index = 0; index < sections.Length; index++)
            {
                var cell = await host.PutAsync($"{school.Root}/workload/cell", new { sectionId = sections[index], entryId = school.EntryId, teacherId = ids[index % ids.Count] }, school.Token);
                Assert.Equal(HttpStatusCode.OK, cell.StatusCode);
            }
        }
        var yearId = long.Parse(school.Root.Split('/')[^1], System.Globalization.CultureInfo.InvariantCulture);
        return (school, yearId);
    }
    internal static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    internal static async Task<JsonElement> WaitForEndAsync(TestHost host, long runId)
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            var run = await JsonAsync(await host.Client.GetAsync($"/api/v1/generation/runs/{runId}"));
            if (run.GetProperty("status").GetString() is not ("queued" or "validating" or "generating"))
                return run;
            await Task.Delay(100);
        }
        throw new TimeoutException("The generation did not finish.");
    }

    [Fact]
    public async Task GenerationEndpointsRequireTheOwnerAndValidateTheSettings()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/generation/engine")).StatusCode);
        var (school, _) = await ReadySchoolAsync(host);
        var engine = await JsonAsync(await host.Client.GetAsync("/api/v1/generation/engine"));
        Assert.True(engine.GetProperty("available").GetBoolean());
        var invalid = await host.PostAsync($"{school.Root}/generation/runs", new { mode = "fast", timeLimitSeconds = 5 }, school.Token);
        var error = await AssertApiErrorAsync(invalid, ErrorCodes.ValidationFailed);
        Assert.Equal(["mode", "timeLimitSeconds"], error.Errors.Select(item => item.Field).Order());
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PostAsync($"{school.Root}/generation/runs", new { }, "wrong-token")).StatusCode);
    }

    [Fact]
    public async Task StartingIsRefusedWhileTheDataHasErrors()
    {
        await using var host = new TestHost();
        var (school, _) = await ReadySchoolAsync(host, assign: false);
        await AssertApiErrorAsync(await host.PostAsync($"{school.Root}/generation/runs", new { }, school.Token), ErrorCodes.GenerationNotReady);
    }

    [Fact]
    public async Task AnImpossibleSchoolEndsInfeasibleWithAnExplanation()
    {
        await using var host = new TestHost();
        var (school, _) = await ReadySchoolAsync(host, teachers: 1);
        var started = await host.PostAsync($"{school.Root}/generation/runs", new { timeLimitSeconds = 10 }, school.Token);
        var run = await WaitForEndAsync(host, (await JsonAsync(started)).GetProperty("id").GetInt64());
        Assert.Equal("infeasible", run.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, run.GetProperty("timetableVersionId").ValueKind);
        var codes = run.GetProperty("diagnostics").GetProperty("findings").EnumerateArray().Select(item => item.GetProperty("code").GetString()).ToArray();
        Assert.NotEmpty(codes);
        Assert.All(codes, code => Assert.Contains(code, DiagnosticCodes.All));
    }

    [Fact]
    public async Task OnlyOneRunIsActiveAndARunLeftActiveIsInterruptedAtStartUp()
    {
        await using var host = new TestHost();
        var (school, yearId) = await ReadySchoolAsync(host);
        long stuck;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IDataStore>();
            var run = GenerationRun.Queue(yearId, GenerationModes.Standard, 60, 1, 1, true, "OR-Tools", "-", "hash", 1, DateTimeOffset.UtcNow);
            store.Add(run);
            await store.SaveChangesAsync(CancellationToken.None);
            stuck = run.Id;
        }
        await AssertApiErrorAsync(await host.PostAsync($"{school.Root}/generation/runs", new { }, school.Token), ErrorCodes.GenerationActive);

        await using (var scope = host.Services.CreateAsyncScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<GenerationService>().RecoverInterruptedAsync(CancellationToken.None));
        var recovered = await JsonAsync(await host.Client.GetAsync($"/api/v1/generation/runs/{stuck}"));
        Assert.Equal("interrupted", recovered.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Accepted, (await host.PostAsync($"{school.Root}/generation/runs", new { timeLimitSeconds = 10 }, school.Token)).StatusCode);
    }

    [Fact]
    public async Task ARunIsPolledToAVerifiedVersionThatCanBeReadApprovedAndEdited()
    {
        await using var host = new TestHost();
        var (school, _) = await ReadySchoolAsync(host);
        var started = await host.PostAsync($"{school.Root}/generation/runs", new { timeLimitSeconds = 10, deterministic = true, seed = 42 }, school.Token);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        var run = await WaitForEndAsync(host, (await JsonAsync(started)).GetProperty("id").GetInt64());
        Assert.Equal("completed", run.GetProperty("status").GetString());
        Assert.Equal(42, run.GetProperty("seed").GetInt32());
        Assert.Equal(10, run.GetProperty("lessonsPlaced").GetInt32());
        Assert.StartsWith("OR-Tools 9.15", run.GetProperty("solverVersion").GetString(), StringComparison.Ordinal);
        Assert.Equal(5, run.GetProperty("score").GetProperty("rules").GetArrayLength());
        var versionId = run.GetProperty("timetableVersionId").GetInt64();

        var current = await JsonAsync(await host.Client.GetAsync($"{school.Root}/generation/current"));
        Assert.Equal(run.GetProperty("id").GetInt64(), current.GetProperty("run").GetProperty("id").GetInt64());
        var timetable = await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{versionId}"));
        Assert.Equal(0, timetable.GetProperty("violations").GetInt32());
        Assert.Equal(10, timetable.GetProperty("lessons").GetArrayLength());
        Assert.False(timetable.GetProperty("summary").GetProperty("stale").GetBoolean());
        Assert.Equal(2, timetable.GetProperty("sections").GetArrayLength());

        var summary = timetable.GetProperty("summary");
        var approved = await JsonAsync(await host.PostAsync($"/api/v1/timetables/{versionId}/approve", new { version = summary.GetProperty("version").GetInt32() }, school.Token));
        Assert.True(approved.GetProperty("isApproved").GetBoolean());
        await AssertApiErrorAsync(await host.PostAsync($"/api/v1/timetables/{versionId}/approve", new { version = 99 }, school.Token), ErrorCodes.Conflict);

        // Excel: the master sheet, one sheet per section and per teacher, right to left, Arabic headers.
        var context = await JsonAsync(await host.Client.GetAsync("/api/v1/school-context"));
        var excel = await host.Client.GetAsync($"/api/v1/timetables/{versionId}/export.xlsx");
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        Assert.Equal(TimetableExportService.ExcelContentType, excel.Content.Headers.ContentType?.MediaType);
        using (var workbook = new ClosedXML.Excel.XLWorkbook(await excel.Content.ReadAsStreamAsync()))
        {
            Assert.Equal(1 + 2 + 2, workbook.Worksheets.Count);
            Assert.Equal("الجدول العام", workbook.Worksheets.First().Name);
            Assert.All(workbook.Worksheets, sheet => Assert.True(sheet.RightToLeft));
            Assert.Equal(context.GetProperty("schoolName").GetString(), workbook.Worksheets.First().Cell(1, 1).GetString());
            var sectionSheet = workbook.Worksheets.Skip(1).First();
            // MF5: school, «جدول الدروس الأسبوعي», year (and semester), the section; the footer signs and numbers the pages.
            Assert.Equal("جدول الدروس الأسبوعي", sectionSheet.Cell(2, 1).GetString());
            Assert.StartsWith("السنة الدراسية 2026-2027", sectionSheet.Cell(3, 1).GetString(), StringComparison.Ordinal);
            Assert.Contains("التوقيع", sectionSheet.PageSetup.Footer.Center.GetText(ClosedXML.Excel.XLHFOccurrence.OddPages), StringComparison.Ordinal);
            Assert.Contains("الختم", sectionSheet.PageSetup.Footer.Center.GetText(ClosedXML.Excel.XLHFOccurrence.OddPages), StringComparison.Ordinal);
            Assert.Equal("اليوم", sectionSheet.Cell(6, 1).GetString());
            // R1: lesson headers carry the 12-hour clock (the seeded shift starts at 08:00 with 40-minute lessons).
            Assert.Equal("الحصة ١\n٨:٠٠ ص – ٨:٤٠ ص", sectionSheet.Cell(6, 2).GetString());
            Assert.DoesNotContain(workbook.Worksheets.SelectMany(sheet => sheet.CellsUsed()), cell => System.Text.RegularExpressions.Regex.IsMatch(cell.GetString(), @"\b(1[3-9]|2[0-3]):\d\d\b"));
            Assert.Equal(5, sectionSheet.CellsUsed(cell => cell.GetString().StartsWith("الرياضيات", StringComparison.Ordinal)).Count());
        }
        await AssertApiErrorAsync(await host.Client.GetAsync("/api/v1/timetables/999999/export.xlsx"), ErrorCodes.NotFound);

        // An edit that puts two lessons of one section in the same slot is reported and refused.
        var lessons = timetable.GetProperty("lessons").EnumerateArray().Select(item => new GridLesson(
            item.GetProperty("sectionId").GetInt64(), item.GetProperty("lineId").GetInt64(), item.GetProperty("subjectId").GetInt64(),
            item.GetProperty("teacherId").GetInt64(), item.GetProperty("day").GetInt32(), item.GetProperty("lesson").GetInt32())).ToList();
        var first = lessons.First(lesson => lesson.SectionId == lessons[0].SectionId);
        var other = lessons.First(lesson => lesson.SectionId == first.SectionId && lesson != first);
        var clash = lessons.Select(lesson => lesson == other ? other with { Day = first.Day, Lesson = first.Lesson } : lesson).ToArray();
        var check = await JsonAsync(await host.PostAsync($"/api/v1/timetables/{versionId}/check", new { lessons = clash }, school.Token));
        Assert.Contains(check.GetProperty("violations").EnumerateArray(), item => item.GetProperty("code").GetString() == ViolationCodes.SectionConflict);
        await AssertApiErrorAsync(await host.PostAsync($"/api/v1/timetables/{versionId}/edits", new { lessons = clash }, school.Token), ErrorCodes.TimetableHasViolations);

        // A valid edit becomes a new version linked to its parent; the parent is unchanged and stays approved.
        var saved = await host.PostAsync($"/api/v1/timetables/{versionId}/edits", new { lessons, note = "تجربة" }, school.Token);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var edited = await JsonAsync(saved);
        Assert.Equal(("edited", versionId, 2), (edited.GetProperty("source").GetString(), edited.GetProperty("parentVersionId").GetInt64(), edited.GetProperty("number").GetInt32()));
        var versions = await JsonAsync(await host.Client.GetAsync($"{school.Root}/timetables"));
        Assert.Equal([2, 1], versions.EnumerateArray().Select(item => item.GetProperty("number").GetInt32()));
        Assert.True(versions.EnumerateArray().Single(item => item.GetProperty("number").GetInt32() == 1).GetProperty("isApproved").GetBoolean());
    }
}
