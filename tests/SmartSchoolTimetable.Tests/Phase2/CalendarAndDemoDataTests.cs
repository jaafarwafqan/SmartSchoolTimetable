using SmartSchoolTimetable.Domain.Curriculum;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Calendar;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Dashboard;
using SmartSchoolTimetable.Domain.Calendar;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Infrastructure;
using SmartSchoolTimetable.Infrastructure.DemoData;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase2;

public sealed class CalendarAndDemoDataTests
{
    [Fact]
    public void CalendarDayValidatesRangeTitleAndKind()
    {
        var errors = Assert.Throws<DomainValidationException>(() =>
            CalendarDay.Create(" ", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 1), (CalendarDayKind)9, true)).Errors;
        Assert.Equal(["Title", "EndDate", "Kind"], errors.Select(error => error.Field));
        Assert.Contains(Assert.Throws<DomainValidationException>(() =>
            CalendarDay.Create("x", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 5), CalendarDayKind.Exam, true)).Errors, error => error.Code == DomainErrorCode.OutOfRange);

        var day = CalendarDay.Create("عطلة", new DateOnly(2026, 10, 3), null, CalendarDayKind.OfficialHoliday, true);
        Assert.Equal(day.StartDate, day.EndDate);
        Assert.False(day.IsOutside(new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));
        day.Update("امتحانات", new DateOnly(2027, 6, 25), new DateOnly(2027, 7, 5), CalendarDayKind.Exam, false);
        Assert.True(day.IsOutside(new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));
        Assert.Equal(2, day.Version);
    }

    [Fact]
    public async Task CalendarRoutesWarnOutsideTheYearAndFilterByMonth()
    {
        await using var host = new TestHost();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/calendar-days/")).StatusCode);
        var (token, _) = await SetupOwnerAsync(host);
        var noYear = await ReadAsync<CalendarDayDto>(await host.PostAsync("/api/v1/calendar-days/", new { title = "يوم", startDate = "2020-01-01", kind = "specialDay", version = 0 }, token));
        Assert.False(noYear.OutsideCurrentYear); // nothing to compare with yet
        await AcademicYearApiTests.CreateYearAsync(host, token, "2026-2027", "2026-09-01", "2027-06-30");

        var invalid = await host.PostAsync("/api/v1/calendar-days/", new { title = "", startDate = "2026-13-01", kind = "party", version = 0 }, token);
        var fields = (await AssertApiErrorAsync(invalid, "VALIDATION_FAILED")).Errors.Select(issue => (issue.Field, issue.Code)).ToArray();
        Assert.Contains(("StartDate", "INVALID_DATE"), fields);
        Assert.Contains(("Kind", "INVALID_OPTION"), fields);

        var holiday = await ReadAsync<CalendarDayDto>(await host.PostAsync("/api/v1/calendar-days/", new { title = "عطلة نصف السنة", startDate = "2027-01-21", endDate = "2027-02-04", kind = "schoolHoliday", affectsSchedule = true, version = 0 }, token));
        Assert.False(holiday.OutsideCurrentYear);
        var outside = await ReadAsync<CalendarDayDto>(await host.PostAsync("/api/v1/calendar-days/", new { title = "صيف", startDate = "2027-07-01", kind = "schoolHoliday", version = 0 }, token));
        Assert.True(outside.OutsideCurrentYear); // warned, not blocked

        var february = await ReadAsync<PagedResult<CalendarDayDto>>(await host.Client.GetAsync("/api/v1/calendar-days/?from=2027-02-01&to=2027-02-28"));
        Assert.Equal("عطلة نصف السنة", Assert.Single(february.Items).Title);
        await AssertApiErrorAsync(await host.Client.GetAsync("/api/v1/calendar-days/?from=feb"), "VALIDATION_FAILED");
        Assert.Equal(3, (await ReadAsync<PagedResult<CalendarDayDto>>(await host.Client.GetAsync("/api/v1/calendar-days/?sort=-startDate"))).Total);

        var updated = await ReadAsync<CalendarDayDto>(await host.PutAsync($"/api/v1/calendar-days/{outside.Id}", new { title = "صيف", startDate = "2027-06-30", kind = "officialHoliday", version = outside.Version }, token));
        Assert.Equal(("officialHoliday", false), (updated.Kind, updated.OutsideCurrentYear));
        Assert.Equal(HttpStatusCode.Conflict, (await host.PutAsync($"/api/v1/calendar-days/{outside.Id}", new { title = "x", startDate = "2027-06-30", kind = "exam", version = outside.Version }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PutAsync("/api/v1/calendar-days/999", new { title = "x", startDate = "2027-06-30", kind = "exam", version = 1 }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.SendJsonAsync(HttpMethod.Delete, $"/api/v1/calendar-days/{outside.Id}?version={outside.Version}", new { }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendJsonAsync(HttpMethod.Delete, $"/api/v1/calendar-days/{outside.Id}?version={updated.Version}", new { }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.SendJsonAsync(HttpMethod.Delete, $"/api/v1/calendar-days/{outside.Id}?version={updated.Version}", new { }, token)).StatusCode);
    }

    [Fact]
    public void DemoTargetRefusesMissingProtectedAndExistingPaths()
    {
        var directory = Directory.CreateTempSubdirectory("demo-check-");
        try
        {
            var protectedPath = Path.Combine(directory.FullName, "real.db");
            var existing = Path.Combine(directory.FullName, "existing.db");
            File.WriteAllText(existing, "x");
            Assert.Equal(DemoDataSeeder.MissingPathMessage, DemoDataSeeder.CheckTarget(null, []));
            Assert.Equal(DemoDataSeeder.MissingPathMessage, DemoDataSeeder.CheckTarget("--dual-shift", []));
            Assert.Equal(DemoDataSeeder.RefusedProtectedMessage, DemoDataSeeder.CheckTarget(protectedPath.ToUpperInvariant(), [protectedPath]));
            Assert.False(File.Exists(protectedPath)); // the check never creates or touches the protected file
            Assert.Equal(DemoDataSeeder.RefusedExistingMessage, DemoDataSeeder.CheckTarget(existing, [protectedPath]));
            Assert.Null(DemoDataSeeder.CheckTarget(Path.Combine(directory.FullName, "new.db"), [protectedPath]));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task DemoDataCreatesASeparateCompleteSampleSchool()
    {
        var directory = Directory.CreateTempSubdirectory("demo-seed-");
        var target = Path.Combine(directory.FullName, "demo.db");
        try
        {
            using var output = new StringWriter();
            Assert.True(await DemoDataSeeder.SeedAsync(target, [Path.Combine(directory.FullName, "real.db")], dualShift: true, output));
            Assert.Contains(DemoDataSeeder.CompletedMessage, output.ToString(), StringComparison.Ordinal);
            Assert.False(await DemoDataSeeder.SeedAsync(target, [], dualShift: false, output)); // second run refuses the existing file

            var services = new ServiceCollection().AddLocalInfrastructure(target, skipLoginDelay: true);
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IDataStore>();
            Assert.Equal(20, await store.CountAsync(store.Query<Teacher>(), default));
            Assert.Equal(2, await store.CountAsync(store.Query<Teacher>().Where(teacher => teacher.FullyReleased), default));
            Assert.Equal(10, await store.CountAsync(store.Query<Subject>(), default));
            Assert.Equal(5, await store.CountAsync(store.Query<Stage>(), default)); // three intermediate grades + الرابع العلمي/الأدبي
            Assert.Equal(13, await store.CountAsync(store.Query<Section>(), default));
            var entries = await store.ListAsync(store.Query<CurriculumEntry>(), default);
            Assert.Equal(22, entries.Count); // sample lines of the first grade, copied to the second
            Assert.All(entries, entry => Assert.Equal(DemoDataSeeder.CurriculumSampleNote, entry.Notes)); // marked as demo numbers
            Assert.Contains(entries, entry => entry.Label == "أدب"); // a repeated subject
            Assert.Equal(8, await store.CountAsync(store.Query<CalendarDay>(), default));
            var shifts = await store.ListAsync(store.Query<Shift>(), default);
            Assert.Equal([7, 7], shifts.Select(shift => shift.LessonCount));
            Assert.All(shifts, shift => Assert.Equal(8, shift.Periods.Count)); // 7 lessons + 1 break
            var summary = await new DashboardService(store).GetSummaryAsync(default);
            Assert.All(summary.Checklist, item => Assert.True(item.Done, item.Key));
            Assert.Contains(summary.Counts, count => count is { Key: "capacityGaps", Value: 0 });
            Assert.Equal(("equal", 30), (summary.Curriculum[0].Totals.Single().Status, summary.Curriculum[0].Totals.Single().WeeklyCapacity)); // own 6 lessons a day
            Assert.Equal(["under", "under"], summary.Curriculum[1].Totals.Select(total => total.Status).Concat(summary.Curriculum[3].Totals.Select(total => total.Status)).Take(2));
            Assert.Equal(["evening"], shifts.Where(shift => shift.Id == store.Query<Section>().First(section => section.Label == "أ" && section.StageId == summary.Curriculum[3].Id).ShiftId).Select(shift => shift.Kind == ShiftKind.Evening ? "evening" : "other"));

            var morningOnly = Path.Combine(directory.FullName, "morning.db");
            Assert.True(await DemoDataSeeder.SeedAsync(morningOnly, [], dualShift: false, output));
            var single = new ServiceCollection().AddLocalInfrastructure(morningOnly, skipLoginDelay: true);
            await using var singleProvider = single.BuildServiceProvider();
            await using var singleScope = singleProvider.CreateAsyncScope();
            var singleStore = singleScope.ServiceProvider.GetRequiredService<IDataStore>();
            Assert.Equal([ShiftKind.Morning], (await singleStore.ListAsync(singleStore.Query<Shift>(), default)).Select(shift => shift.Kind));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            directory.Delete(recursive: true);
        }
    }
}
