using System.Net;
using System.Text.Json;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// R2 flexible breaks: a break after any lesson 1..N−1, several (one per gap), any duration 1–60 minutes in the UI
/// (the domain keeps 1–120); duplicates and a break after the last lesson are refused; old plans give the same rows.
/// </summary>
public sealed class FlexibleBreaksTests
{
    private static string[] Rows(IEnumerable<PeriodDraft> drafts) =>
        drafts.Select(row => $"{(row.Kind == PeriodKind.Break ? "B" : "L")} {row.StartTime:HH\\:mm}-{row.EndTime:HH\\:mm}").ToArray();

    [Fact]
    public void ABreakAfterTheFirstLessonAndAfterTheLastButOne()
    {
        var rows = PeriodGenerator.Generate(new PeriodPlan(new TimeOnly(8, 0), 45, 4, [new BreakSlot(1, 10), new BreakSlot(3, 20)]));
        Assert.Equal(["L 08:00-08:45", "B 08:45-08:55", "L 08:55-09:40", "L 09:40-10:25", "B 10:25-10:45", "L 10:45-11:30"], Rows(rows));
    }

    [Fact]
    public void ABreakInEveryGapAndOneMinuteAndSixtyMinuteBreaks()
    {
        var everyGap = Enumerable.Range(1, Shift.MaxLessons - 1).Select(after => new BreakSlot(after, after % 2 == 0 ? 60 : 1)).ToArray();
        var rows = PeriodGenerator.Generate(new PeriodPlan(new TimeOnly(7, 0), 30, Shift.MaxLessons, everyGap));
        Assert.Equal(Shift.MaxLessons * 2 - 1, rows.Count);
        Assert.Equal("B 07:30-07:31", Rows(rows)[1]); // a 1-minute break
        Assert.Equal("B 08:01-09:01", Rows(rows)[3]); // a 60-minute break
        Assert.Equal(new TimeOnly(7, 31), rows[2].StartTime); // the next lesson starts when the break ends
        var shift = Shift.Create(1, "صباحي", 1);
        shift.ReplacePeriods(rows); // 23 rows fit (MaxRows = 2 × MaxLessons − 1)
        Assert.Equal(Shift.MaxLessons, shift.LessonCount);
    }

    [Fact]
    public void DuplicateGapsAndABreakAfterTheLastLessonAreRefused()
    {
        var duplicate = Assert.Throws<DomainValidationException>(() =>
            PeriodGenerator.Generate(new PeriodPlan(new TimeOnly(8, 0), 45, 5, [new BreakSlot(2, 10), new BreakSlot(2, 15)])));
        Assert.Contains(duplicate.Errors, error => error.Field == "BreakAfterLesson" && error.Code == DomainErrorCode.Duplicate);
        var last = Assert.Throws<DomainValidationException>(() =>
            PeriodGenerator.Generate(new PeriodPlan(new TimeOnly(8, 0), 45, 5, [new BreakSlot(5, 10)])));
        Assert.Contains(last.Errors, error => error.Field == "BreakAfterLesson" && error.Code == DomainErrorCode.OutOfRange);
        var zero = Assert.Throws<DomainValidationException>(() =>
            PeriodGenerator.Generate(new PeriodPlan(new TimeOnly(8, 0), 45, 5, [new BreakSlot(2, 0)])));
        Assert.Contains(zero.Errors, error => error.Field == "BreakMinutes");
    }

    [Fact]
    public void OldPlansGiveExactlyTheSameRows()
    {
        // The Phase 2.5 presets and the one-break form, as saved before R2.
        Assert.Equal(["L 08:00-08:45", "L 08:45-09:30", "L 09:30-10:15", "B 10:15-10:30", "L 10:30-11:15"],
            Rows(PeriodGenerator.Generate(new PeriodPlan(new TimeOnly(8, 0), 45, 4, breakMinutes: 15, breakAfterLesson: 3))));
        Assert.Equal(["L 08:00-08:40", "B 08:40-08:45", "L 08:45-09:25", "B 09:25-09:30", "L 09:30-10:10"],
            Rows(PeriodGenerator.Generate(new PeriodPlan(new TimeOnly(8, 0), 40, 3, [new BreakSlot(1, 5), new BreakSlot(2, 5)]))));
        Assert.Equal(["L 08:00-08:45", "L 08:45-09:30"], Rows(PeriodGenerator.Generate(new PeriodPlan(new TimeOnly(8, 0), 45, 2, []))));
    }

    [Fact]
    public async Task TheGeneratorEndpointAcceptsABreakInEveryGap()
    {
        await using var host = new TestHost();
        var school = await Phase3.ReferenceProtectionTests.SeedAsync(host);
        var breaks = Enumerable.Range(1, 6).Select(after => new { afterLesson = after, minutes = after }).ToArray();
        var response = await host.PostAsync($"{school.Root}/shifts/generate-periods",
            new { firstStartTime = "08:00", lessonMinutes = 40, lessonCount = 7, breakMinutes = 0, breakAfterLesson = (int?)null, breaks, gapMinutes = 0 }, school.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = json.RootElement.GetProperty("periods").EnumerateArray().ToArray();
        Assert.Equal(13, rows.Length);
        Assert.Equal(("break", "08:40", "08:41"), (rows[1].GetProperty("kind").GetString(), rows[1].GetProperty("startTime").GetString(), rows[1].GetProperty("endTime").GetString()));
        var refused = await host.PostAsync($"{school.Root}/shifts/generate-periods",
            new { firstStartTime = "08:00", lessonMinutes = 40, lessonCount = 7, breakMinutes = 0, breakAfterLesson = (int?)null, breaks = new[] { new { afterLesson = 7, minutes = 10 } }, gapMinutes = 0 }, school.Token);
        Assert.Contains(await AssertApiErrorAsync(refused, Application.ErrorCodes.ValidationFailed) is { } error ? error.Errors : [], item => item.Field == "BreakAfterLesson");
    }
}
