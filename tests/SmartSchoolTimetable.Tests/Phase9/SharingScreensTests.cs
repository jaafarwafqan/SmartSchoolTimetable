using System.Net;
using System.Text.Json;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Sharing;
using SmartSchoolTimetable.Infrastructure.Sharing;
using SmartSchoolTimetable.Tests.Phase4;
using SmartSchoolTimetable.Tests.Phase5;
using ZXing.Common;
using ZXing.QrCode.Internal;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;
using static SmartSchoolTimetable.Tests.Phase4.GenerationApiTests;

namespace SmartSchoolTimetable.Tests.Phase9;

/// <summary>
/// 9a-2 on a real host: the QR codes really decode (ZXing) to packages the reference reader accepts, the teacher cards, the
/// «حمّل التطبيق» link, «شاشة العرض» with its own key, and the day resolution every screen and the phone share.
/// </summary>
[Collection(SerialSolverRuns.Name)]
public sealed class SharingScreensTests
{
    private static readonly DateOnly Today = new(2026, 10, 3);
    private static readonly long[] UnknownTeacher = [999_999L];

    /// <summary>Decodes a QR from its module rows (quiet zone included) with ZXing, as a phone camera would after locating it.</summary>
    internal static string Decode(JsonElement qr)
    {
        var rows = qr.GetProperty("rows").EnumerateArray().Select(row => row.GetString()!).ToArray();
        var size = rows.Length - 8;
        var bits = new BitMatrix(size);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                if (rows[y + 4][x + 4] == '1')
                    bits[x, y] = true;
        return new Decoder().decode(bits, null).Text;
    }

    private static async Task<LifecycleApiTests.Fixture> ApprovedAsync(TestHost host)
    {
        var fixture = await LifecycleApiTests.GeneratedAsync(host);
        var version = (await LifecycleApiTests.SummaryAsync(host, fixture.FirstId)).GetProperty("version").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/approve", new { version }, fixture.Token)).StatusCode);
        return fixture;
    }

    private static async Task<string[]> SharingEventsAsync(TestHost host) =>
        (await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=sharing"))).GetProperty("items").EnumerateArray()
            .Select(entry => entry.GetProperty("eventType").GetString()!).ToArray();

    [Fact]
    public async Task ThePairingAndTeacherQrCodesDecodeToPackagesThePhoneAccepts()
    {
        await using var host = new TestHost();
        var fixture = await ApprovedAsync(host);
        var pairingDto = await JsonAsync(await host.PostAsync("/api/v1/sharing/pairing-qr", new { }, fixture.Token));
        var pairing = PhonePackageReader.Read(PhonePackageFormat.FromQrText(Decode(pairingDto.GetProperty("code").GetProperty("qr"))), null, Today);
        Assert.Equal(PackageReadStatus.Valid, pairing.Status);
        Assert.Equal(SharingService.FingerprintOf(pairing.Package!.PublicKey!), pairingDto.GetProperty("fingerprint").GetString());
        var trusted = new TrustedSchool(pairing.Package.Header.SchoolId, pairing.Package.PublicKey!);

        var teacherId = fixture.Lessons[0].TeacherId;
        var dto = await JsonAsync(await host.PostAsync($"/api/v1/sharing/teachers/{teacherId}/qr", new { }, fixture.Token));
        var qr = dto.GetProperty("code").GetProperty("qr");
        var version = qr.GetProperty("version").GetInt32();
        Assert.InRange(version, 1, SharingService.MaxQrVersion);
        Assert.Equal(4 * version + 17 + 8, qr.GetProperty("size").GetInt32());
        var text = Decode(qr);
        Assert.Equal(dto.GetProperty("code").GetProperty("characters").GetInt32(), text.Length);
        var teacher = PhonePackageReader.Read(PhonePackageFormat.FromQrText(text), trusted, Today);
        Assert.Equal(PackageReadStatus.Valid, teacher.Status);
        Assert.Equal(dto.GetProperty("lessons").GetInt32(), teacher.Package!.Timetable!.Lessons.Count);
        Assert.Equal(["SharingTeacherQrShown", "SharingPairingShown", "SharingKeyCreated"], await SharingEventsAsync(host));
    }

    [Fact]
    public void ATextTooDenseForTheChosenVersionGivesNoQr()
    {
        var encoder = new QrEncoder();
        Assert.Null(encoder.Encode(new string('A', 2000), SharingService.MaxQrVersion));
        Assert.Null(encoder.Encode(new string('A', 10_000), 40));
        var small = encoder.Encode("https://t.me/example", SharingService.MaxLinkQrVersion)!;
        Assert.True(small.Version <= 3);
        Assert.All(small.Rows, row => Assert.Equal(small.Size, row.Length));
    }

    [Fact]
    public async Task TeacherCardsCarryEveryTeachersCodeThePairingCodeAndTheAppLink()
    {
        await using var host = new TestHost();
        var fixture = await ApprovedAsync(host);
        var settings = await JsonAsync(await host.Client.GetAsync("/api/v1/sharing/settings"));
        Assert.Equal((JsonValueKind.Null, 0), (settings.GetProperty("appLink").ValueKind, settings.GetProperty("version").GetInt32()));
        foreach (var bad in new[] { "http://t.me/x", "t.me/x", "https://", "https://user:pw@t.me/x", "https://t.me/a b" })
        {
            var refused = await AssertApiErrorAsync(await host.PutAsync("/api/v1/sharing/settings", new { appLink = bad, version = 0 }, fixture.Token), ErrorCodes.ValidationFailed);
            Assert.Equal("AppLink", Assert.Single(refused.Errors).Field);
        }

        var cards = await JsonAsync(await host.PostAsync("/api/v1/sharing/cards", new { teacherIds = (long[]?)null }, fixture.Token));
        Assert.Equal(JsonValueKind.Null, cards.GetProperty("appQr").ValueKind);
        var all = cards.GetProperty("cards").EnumerateArray().ToArray();
        Assert.Equal(fixture.Lessons.Select(lesson => lesson.TeacherId).Distinct().Count(), all.Length);
        var pairing = PhonePackageReader.Read(PhonePackageFormat.FromQrText(Decode(cards.GetProperty("pairing").GetProperty("qr"))), null, Today).Package!;
        var trusted = new TrustedSchool(pairing.Header.SchoolId, pairing.PublicKey!);
        Assert.All(all, card => Assert.Equal(PackageReadStatus.Valid,
            PhonePackageReader.Read(PhonePackageFormat.FromQrText(Decode(card.GetProperty("code").GetProperty("qr"))), trusted, Today).Status));

        var saved = await JsonAsync(await host.PutAsync("/api/v1/sharing/settings", new { appLink = " https://t.me/smart_school_app ", version = 0 }, fixture.Token));
        Assert.Equal("https://t.me/smart_school_app", saved.GetProperty("appLink").GetString());
        var one = await JsonAsync(await host.PostAsync("/api/v1/sharing/cards", new { teacherIds = new[] { all[0].GetProperty("teacherId").GetInt64() } }, fixture.Token));
        Assert.Single(one.GetProperty("cards").EnumerateArray());
        Assert.Equal("https://t.me/smart_school_app", Decode(one.GetProperty("appQr")));
        await AssertApiErrorAsync(await host.PostAsync("/api/v1/sharing/cards", new { teacherIds = UnknownTeacher }, fixture.Token), ErrorCodes.ValidationFailed);
        await AssertApiErrorAsync(await host.PutAsync("/api/v1/sharing/settings", new { appLink = (string?)null, version = 0 }, fixture.Token), ErrorCodes.Conflict);

        var events = await SharingEventsAsync(host);
        Assert.Equal(2, events.Count(name => name == "SharingCardsPrinted"));
        Assert.Contains("SharingSettingsUpdated", events);
    }

    [Fact]
    public async Task TheKioskReadsTodayWithItsOwnKeyWithoutTheOwnerSessionAndStopsWhenAsked()
    {
        await using var host = new TestHost();
        var fixture = await ApprovedAsync(host);
        using var screen = host.CreateClientWithoutCookies();
        async Task<HttpResponseMessage> TodayAsync(string? key)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/kiosk/today");
            if (key is not null)
                request.Headers.Add("X-Kiosk-Key", key);
            return await screen.SendAsync(request);
        }

        await AssertApiErrorAsync(await TodayAsync(null), ErrorCodes.KioskKeyInvalid);
        // Starting the screen is the owner's action: no launch token (and no session) is refused.
        Assert.Equal(HttpStatusCode.Forbidden, (await screen.PostAsync("/api/v1/sharing/kiosk/start", null)).StatusCode);
        var first = (await JsonAsync(await host.PostAsync("/api/v1/sharing/kiosk/start", new { }, fixture.Token))).GetProperty("key").GetString()!;
        await AssertApiErrorAsync(await TodayAsync("AAAA"), ErrorCodes.KioskKeyInvalid);
        Assert.Equal(HttpStatusCode.OK, (await TodayAsync(first)).StatusCode);
        // Stopping ends the key; opening again gives a new one and the old one stays invalid.
        Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsync("/api/v1/sharing/kiosk/stop", new { }, fixture.Token)).StatusCode);
        await AssertApiErrorAsync(await TodayAsync(first), ErrorCodes.KioskKeyInvalid);
        var key = (await JsonAsync(await host.PostAsync("/api/v1/sharing/kiosk/start", new { }, fixture.Token))).GetProperty("key").GetString()!;
        Assert.NotEqual(first, key);
        await AssertApiErrorAsync(await TodayAsync(first), ErrorCodes.KioskKeyInvalid);
        Assert.Equal(HttpStatusCode.Created, (await host.PostAsync("/api/v1/calendar-days/", new { title = "يوم تجريبي", startDate = "2026-10-05", kind = "schoolHoliday", affectsSchedule = true, version = 0 }, fixture.Token)).StatusCode);
        Assert.Equal(2, (await SharingEventsAsync(host)).Count(name => name == "SharingKioskStarted"));

        // 2026-10-03 (a Saturday) is also Iraq's National Day from the holiday template: a holiday is named before "no school today".
        var saturday = await JsonAsync(await TodayAsync(key));
        Assert.Equal(("2026-10-03", "dayOff"), (saturday.GetProperty("date").GetString(), saturday.GetProperty("status").GetString()));
        Assert.False(string.IsNullOrEmpty(saturday.GetProperty("dayOffTitle").GetString()));
        Assert.Empty(saturday.GetProperty("lessons").EnumerateArray());

        // From here the owner's session expires (a day passes); the screen keeps working with its own key.
        host.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/sharing/")).StatusCode);
        var sunday = await JsonAsync(await TodayAsync(key));
        Assert.Equal("school", sunday.GetProperty("status").GetString());
        var lessons = sunday.GetProperty("lessons").EnumerateArray().ToArray();
        Assert.Equal(fixture.Lessons.Count(lesson => lesson.Day == 7), lessons.Length);
        Assert.Equal(2, sunday.GetProperty("sections").GetArrayLength());
        var periods = sunday.GetProperty("periods").EnumerateArray().ToArray();
        Assert.Equal(Enumerable.Range(1, periods.Count(period => period.GetProperty("number").ValueKind == JsonValueKind.Number)),
            periods.Where(period => period.GetProperty("number").ValueKind == JsonValueKind.Number).Select(period => period.GetProperty("number").GetInt32()));
        Assert.All(lessons, lesson => Assert.False(string.IsNullOrEmpty(lesson.GetProperty("teacher").GetString())));

        // Monday 2026-10-05 is the holiday added above: «عطلة: …».
        host.Clock.Advance(TimeSpan.FromDays(1));
        var holiday = await JsonAsync(await TodayAsync(key));
        Assert.Equal(("dayOff", "يوم تجريبي"), (holiday.GetProperty("status").GetString(), holiday.GetProperty("dayOffTitle").GetString()));
        // Friday 2026-10-09 is not a working day.
        host.Clock.Advance(TimeSpan.FromDays(4));
        Assert.Equal("notAWorkingDay", (await JsonAsync(await TodayAsync(key))).GetProperty("status").GetString());
    }

    [Fact]
    public void ADayResolvesToItsSemesterSessionAndClockOrToWhyThereIsNoSchool()
    {
        var content = SstVectors.PrincipalContent();
        // Sunday 2026-11-01, semester 1: morning (session 1) from 08:00.
        var sunday = PackageCalendar.Resolve(content, new DateOnly(2026, 11, 1));
        Assert.Equal((SchoolDayStatus.School, 7, 1, 1), (sunday.Status, sunday.Day, sunday.Semester, sunday.Session));
        Assert.Equal(480, sunday.Lessons()[0].Period.StartMinute);
        Assert.Equal(6, sunday.Lessons().Count);
        Assert.Equal(4, sunday.Lessons()[3].Number);
        // Tuesday in semester 1 is evening; the same Tuesday rule in semester 2 is morning.
        Assert.Equal((1, 3), (PackageCalendar.Resolve(content, new DateOnly(2026, 11, 3)).Semester, PackageCalendar.Resolve(content, new DateOnly(2026, 11, 3)).Session));
        Assert.Equal((2, 1), (PackageCalendar.Resolve(content, new DateOnly(2027, 2, 2)).Semester, PackageCalendar.Resolve(content, new DateOnly(2027, 2, 2)).Session));
        // Between the semesters (2027-01-16..31 is also a holiday) and on Friday, outside the year.
        Assert.Equal(SchoolDayStatus.DayOff, PackageCalendar.Resolve(content, new DateOnly(2027, 1, 20)).Status);
        Assert.Equal(SchoolDayStatus.DayOff, PackageCalendar.Resolve(content, new DateOnly(2027, 1, 5)).Status);
        Assert.Equal(PackageDayKind.Exam, PackageCalendar.Resolve(content, new DateOnly(2027, 1, 5)).DayOff!.Kind);
        Assert.Equal(SchoolDayStatus.NotAWorkingDay, PackageCalendar.Resolve(content, new DateOnly(2026, 11, 6)).Status);
        Assert.Equal(SchoolDayStatus.OutsideYear, PackageCalendar.Resolve(content, new DateOnly(2027, 7, 4)).Status);
        Assert.Equal(SchoolDayStatus.OutsideYear, PackageCalendar.Resolve(content, new DateOnly(2026, 8, 30)).Status);
    }
}
