using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using SmartSchoolTimetable.Application;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;
using static SmartSchoolTimetable.Tests.Phase4.GenerationApiTests;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// R3 «دوام مزدوج» on a real host with a temporary database: the owner's example (semester 1 = Sunday and Monday
/// morning, Tuesday to Thursday evening; semester 2 reversed), ONE generated timetable whose clock times follow the
/// day's session in each semester (API, Excel), guards that keep every session at the same lesson count, and a
/// single-session school that behaves exactly as before.
/// </summary>
[Collection(SerialSolverRuns.Name)]
public sealed partial class SessionPlanApiTests
{
    private static readonly int[] Week = [7, 1, 2, 3, 4];

    [GeneratedRegex(@"\b(1[3-9]|2[0-3]):\d\d\b")]
    private static partial Regex TwentyFourHour();

    /// <summary>Lessons of 40 minutes from <paramref name="start"/> ("HH:mm"), with an optional 10-minute break.</summary>
    private static object[] Rows(int lessons, int start, int? breakAfter = null)
    {
        var rows = new List<object>();
        var time = start;
        static string Clock(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";
        for (var lesson = 1; lesson <= lessons; lesson++)
        {
            rows.Add(new { kind = "lesson", startTime = Clock(time), endTime = Clock(time + 40) });
            time += 40;
            if (breakAfter == lesson)
            {
                rows.Add(new { kind = "break", startTime = Clock(time), endTime = Clock(time + 10) });
                time += 10;
            }
        }
        return rows.ToArray();
    }

    private static object[] OwnerExample(bool reversedFirst = false) =>
        Week.SelectMany(day =>
        {
            var morningFirst = day is 7 or 1;
            if (reversedFirst)
                morningFirst = !morningFirst;
            return new object[]
            {
                new { term = 1, day, session = morningFirst ? "morning" : "evening" },
                new { term = 2, day, session = morningFirst ? "evening" : "morning" },
            };
        }).ToArray();

    private static Task<HttpResponseMessage> SaveAsync(TestHost host, string token, string system, object[] evening, object[] days, int version) =>
        host.PutAsync("/api/v1/session-plan/", new { system, timings = new[] { new { session = "evening", periods = evening } }, days, version }, token);

    private static async Task<XLWorkbook> ExcelAsync(TestHost host, long versionId, int term)
    {
        var response = await host.Client.GetAsync($"/api/v1/timetables/{versionId}/export.xlsx?term={term}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new XLWorkbook(await response.Content.ReadAsStreamAsync());
    }

    [Fact]
    public async Task ADoubleShiftSchoolHasOneTimetableWhoseTimesFollowTheSemester()
    {
        await using var host = new TestHost();
        var (school, _) = await ReadySchoolAsync(host);
        var token = school.Token;

        // A single-session school: no plan, no extra step.
        var plan = await JsonAsync(await host.Client.GetAsync("/api/v1/session-plan/"));
        Assert.Equal("oneSession", plan.GetProperty("system").GetString());
        Assert.True(plan.GetProperty("available").GetBoolean());
        Assert.Equal(6, plan.GetProperty("lessonCount").GetInt32());
        Assert.Equal(0, plan.GetProperty("version").GetInt32());

        // Every session has the same number of lessons (Arabic message for SESSION_LESSON_COUNT_MISMATCH).
        var unequal = await AssertApiErrorAsync(await SaveAsync(host, token, "twoSessions", Rows(5, 13 * 60), OwnerExample(), 0), ErrorCodes.ValidationFailed);
        Assert.Contains(unequal.Errors, error => error.Code == ErrorCodes.SessionLessonCountMismatch);
        var unmapped = await AssertApiErrorAsync(await SaveAsync(host, token, "twoSessions", Rows(6, 13 * 60), OwnerExample().Take(9).ToArray(), 0), ErrorCodes.ValidationFailed);
        Assert.Contains(unmapped.Errors, error => error.Field == "Days" && error.Code == ErrorCodes.Required);

        var saved = await JsonAsync(await SaveAsync(host, token, "twoSessions", Rows(6, 13 * 60), OwnerExample(), 0));
        Assert.Equal("twoSessions", saved.GetProperty("system").GetString());
        Assert.Equal(2, saved.GetProperty("timings").GetArrayLength());
        Assert.Equal(10, saved.GetProperty("days").GetArrayLength());
        var planVersion = saved.GetProperty("version").GetInt32();

        // Guards: the shift keeps the sessions' lesson count, and the sessions need the year's single shift.
        var periods = await host.PutAsync($"{school.Root}/shifts/{school.Shift.Id}/periods", new { periods = Rows(5, 8 * 60), version = school.Shift.Version }, token);
        Assert.Contains((await AssertApiErrorAsync(periods, ErrorCodes.ValidationFailed)).Errors, error => error.Code == ErrorCodes.SessionLessonCountMismatch);
        await AssertApiErrorAsync(await host.PostAsync($"{school.Root}/shifts/", new { name = "دوام إضافي", displayOrder = 3, version = 0 }, token), ErrorCodes.SessionsNeedOneShift);

        // Generate ONCE.
        var started = await host.PostAsync($"{school.Root}/generation/runs", new { timeLimitSeconds = 10, deterministic = true, seed = 42 }, token);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        var run = await WaitForEndAsync(host, (await JsonAsync(started)).GetProperty("id").GetInt64());
        Assert.Equal("completed", run.GetProperty("status").GetString());
        var versionId = run.GetProperty("timetableVersionId").GetInt64();

        var timetable = await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{versionId}"));
        var sessions = timetable.GetProperty("sessions");
        Assert.Equal("twoSessions", sessions.GetProperty("system").GetString());
        var timings = sessions.GetProperty("timings").EnumerateArray().ToDictionary(item => item.GetProperty("session").GetString()!, item => item.GetProperty("lessons"));
        Assert.Equal(8 * 60, timings["morning"][0].GetProperty("startMinute").GetInt32());
        Assert.Equal(13 * 60, timings["evening"][0].GetProperty("startMinute").GetInt32());
        string SessionOf(int term, int day) => sessions.GetProperty("days").EnumerateArray()
            .Single(item => item.GetProperty("term").GetInt32() == term && item.GetProperty("day").GetInt32() == day).GetProperty("session").GetString()!;
        Assert.Equal("morning", SessionOf(1, 7));
        Assert.Equal("evening", SessionOf(2, 7));
        Assert.Equal("evening", SessionOf(1, 4));
        Assert.Equal("morning", SessionOf(2, 4));

        // Excel: the same lessons, the semester in the header, the clock of each day's session in 12-hour form.
        using (var first = await ExcelAsync(host, versionId, 1))
        using (var second = await ExcelAsync(host, versionId, 2))
        {
            foreach (var book in new[] { first, second })
                Assert.DoesNotContain(book.Worksheets.SelectMany(sheet => sheet.CellsUsed()), cell => TwentyFourHour().IsMatch(cell.GetString()));
            Assert.Contains("الفصل الدراسي الأول", first.Worksheets.First().Cell(2, 1).GetString(), StringComparison.Ordinal);
            Assert.Contains("الفصل الدراسي الثاني", second.Worksheets.First().Cell(2, 1).GetString(), StringComparison.Ordinal);

            var section1 = first.Worksheets.Skip(1).First();
            var section2 = second.Worksheets.Skip(1).First();
            Assert.Equal("الحصة ١", section1.Cell(5, 2).GetString());
            Assert.Equal("الدوام الصباحي", section1.Cell(6, 1).GetString());
            Assert.Equal("٨:٠٠ ص – ٨:٤٠ ص", section1.Cell(6, 2).GetString());
            Assert.Equal("الدوام المسائي", section1.Cell(7, 1).GetString());
            Assert.Equal("١:٠٠ م – ١:٤٠ م", section1.Cell(7, 2).GetString());
            Assert.Equal("الأحد\nصباحي", section1.Cell(8, 1).GetString());
            Assert.Equal("الأحد\nمسائي", section2.Cell(8, 1).GetString());
            Assert.Equal("الخميس\nمسائي", section1.Cell(12, 1).GetString());
            Assert.Equal("الخميس\nصباحي", section2.Cell(12, 1).GetString());
            // The grid itself does not change with the semester.
            for (var row = 8; row <= 12; row++)
            {
                for (var column = 2; column <= 7; column++)
                    Assert.Equal(section1.Cell(row, column).GetString(), section2.Cell(row, column).GetString());
            }

            var master1 = first.Worksheets.First();
            var master2 = second.Worksheets.First();
            Assert.Equal("الأحد — صباحي", master1.Cell(5, 2).GetString());
            Assert.Equal("١\n٨:٠٠ ص – ٨:٤٠ ص", master1.Cell(6, 2).GetString());
            Assert.Equal("الأحد — مسائي", master2.Cell(5, 2).GetString());
            Assert.Equal("١\n١:٠٠ م – ١:٤٠ م", master2.Cell(6, 2).GetString());
        }
        await AssertApiErrorAsync(await host.Client.GetAsync($"/api/v1/timetables/{versionId}/export.xlsx?term=3"), ErrorCodes.ValidationFailed);

        // Times and the mapping change no lesson: the version stays current. A break in the evening (a new gap
        // between lesson numbers) can split a double lesson, so it makes the version out of date.
        var remapped = await JsonAsync(await SaveAsync(host, token, "twoSessions", Rows(6, 14 * 60), OwnerExample(reversedFirst: true), planVersion));
        Assert.False((await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{versionId}"))).GetProperty("summary").GetProperty("stale").GetBoolean());
        var withBreak = await JsonAsync(await SaveAsync(host, token, "twoSessions", Rows(6, 14 * 60, breakAfter: 3), OwnerExample(), remapped.GetProperty("version").GetInt32()));
        Assert.True((await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{versionId}"))).GetProperty("summary").GetProperty("stale").GetBoolean());

        // Back to one session: no session data, no semester in Excel, the version is current again.
        var single = await JsonAsync(await host.PutAsync("/api/v1/session-plan/", new { system = "oneSession", timings = Array.Empty<object>(), days = Array.Empty<object>(), version = withBreak.GetProperty("version").GetInt32() }, token));
        Assert.Equal("oneSession", single.GetProperty("system").GetString());
        var plain = await JsonAsync(await host.Client.GetAsync($"/api/v1/timetables/{versionId}"));
        Assert.Equal(JsonValueKind.Null, plain.GetProperty("sessions").ValueKind);
        Assert.False(plain.GetProperty("summary").GetProperty("stale").GetBoolean());
        using var singleBook = await ExcelAsync(host, versionId, 1);
        Assert.DoesNotContain("الفصل", singleBook.Worksheets.First().Cell(2, 1).GetString(), StringComparison.Ordinal);
        Assert.Equal("الحصة ١\n٨:٠٠ ص – ٨:٤٠ ص", singleBook.Worksheets.Skip(1).First().Cell(5, 2).GetString());
    }

    [Fact]
    public async Task OnlyANewBreakGapChangesTheInputHash()
    {
        await using var host = new TestHost();
        var (school, yearId) = await ReadySchoolAsync(host);
        var token = school.Token;
        async Task<string> HashAsync() =>
            (await JsonAsync(await host.Client.GetAsync($"/api/v1/academic-years/{yearId}/readiness/"))).GetProperty("inputHash").GetString()!;

        // The morning (the shift) has a break after lesson 3.
        var shift = await host.PutAsync($"{school.Root}/shifts/{school.Shift.Id}/periods", new { periods = Rows(6, 8 * 60, breakAfter: 3), version = school.Shift.Version }, token);
        Assert.Equal(HttpStatusCode.OK, shift.StatusCode);
        var single = await HashAsync();

        // An evening break in the same gap, other evening times and the mapping change nothing that can be scheduled.
        var sameGap = await JsonAsync(await SaveAsync(host, token, "twoSessions", Rows(6, 13 * 60, breakAfter: 3), OwnerExample(), 0));
        Assert.Equal(single, await HashAsync());
        var otherTimes = await JsonAsync(await SaveAsync(host, token, "twoSessions", Rows(6, 14 * 60 + 30, breakAfter: 3), OwnerExample(reversedFirst: true), sameGap.GetProperty("version").GetInt32()));
        Assert.Equal(single, await HashAsync());

        // A break in a new gap (after lesson 2) can split a double lesson: the hash changes.
        await JsonAsync(await SaveAsync(host, token, "twoSessions", Rows(6, 13 * 60, breakAfter: 2), OwnerExample(), otherTimes.GetProperty("version").GetInt32()));
        Assert.NotEqual(single, await HashAsync());
    }

    [Fact]
    public async Task SessionsNeedExactlyOneShift()
    {
        await using var host = new TestHost();
        var (school, _) = await ReadySchoolAsync(host);
        var second = await host.PostAsync($"{school.Root}/shifts/", new { name = "دوام ثانٍ", displayOrder = 2, version = 0 }, school.Token);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var plan = await JsonAsync(await host.Client.GetAsync("/api/v1/session-plan/"));
        Assert.False(plan.GetProperty("available").GetBoolean());
        Assert.Equal(JsonValueKind.Null, plan.GetProperty("shiftId").ValueKind);
        await AssertApiErrorAsync(await SaveAsync(host, school.Token, "twoSessions", Rows(6, 13 * 60), OwnerExample(), 0), ErrorCodes.SessionsNeedOneShift);
        await AssertApiErrorAsync(await host.PutAsync("/api/v1/session-plan/", new { system = "tripleShift", timings = Array.Empty<object>(), days = Array.Empty<object>(), version = 0 }, school.Token),
            ErrorCodes.ValidationFailed);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClientWithoutCookies().GetAsync("/api/v1/session-plan/")).StatusCode);
    }
}
