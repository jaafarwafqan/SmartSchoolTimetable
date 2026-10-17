using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Sharing;
using SmartSchoolTimetable.Infrastructure.Sharing;
using SmartSchoolTimetable.Tests.Phase4;
using SmartSchoolTimetable.Tests.Phase5;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;
using static SmartSchoolTimetable.Tests.Phase4.GenerationApiTests;

namespace SmartSchoolTimetable.Tests.Phase9;

/// <summary>
/// «مشاركة الجدول مع الهاتف» on a real host (ADR 0056): the school key is made on first use, only the APPROVED version of the
/// current year is exported, a conflict with today's data blocks every export, every export is audited, and the packages
/// read back as valid with the reference reader once the pairing code is trusted.
/// </summary>
[Collection(SerialSolverRuns.Name)]
public sealed class SharingApiTests
{
    private static readonly DateOnly Today = new(2026, 10, 3);

    private static async Task<byte[]> BytesAsync(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static async Task<TrustedSchool> PairAsync(TestHost host, string token)
    {
        var pairing = PhonePackageReader.Read(await BytesAsync(await host.PostAsync("/api/v1/sharing/pairing.sst", new { }, token)), null, Today);
        Assert.Equal(PackageReadStatus.Valid, pairing.Status);
        return new TrustedSchool(pairing.Package!.Header.SchoolId, pairing.Package.PublicKey!);
    }

    private static async Task<JsonElement> StatusAsync(TestHost host) => await JsonAsync(await host.Client.GetAsync("/api/v1/sharing/"));

    private static async Task<string[]> SharingEventsAsync(TestHost host) =>
        (await JsonAsync(await host.Client.GetAsync("/api/v1/audit/?category=sharing"))).GetProperty("items").EnumerateArray()
            .Select(entry => entry.GetProperty("eventType").GetString()!).ToArray();

    private static async Task<LifecycleApiTests.Fixture> ApprovedAsync(TestHost host)
    {
        var fixture = await LifecycleApiTests.GeneratedAsync(host);
        var version = (await LifecycleApiTests.SummaryAsync(host, fixture.FirstId)).GetProperty("version").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/v1/timetables/{fixture.FirstId}/approve", new { version }, fixture.Token)).StatusCode);
        return fixture;
    }

    [Fact]
    public async Task WithoutAnApprovedTimetableOnlyThePairingCodeIsAvailableAndItMakesTheKeyOnce()
    {
        await using var host = new TestHost();
        var (school, _) = await ReadySchoolAsync(host);
        var status = await StatusAsync(host);
        Assert.Equal(JsonValueKind.Null, status.GetProperty("key").ValueKind);
        Assert.Equal(JsonValueKind.Null, status.GetProperty("approved").ValueKind);
        foreach (var path in new[] { "/api/v1/sharing/teachers.zip", "/api/v1/sharing/principal.sst", "/api/v1/sharing/teachers/1/package.sst" })
            await AssertApiErrorAsync(await host.PostAsync(path, new { }, school.Token), ErrorCodes.ShareNoApprovedTimetable);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PostAsync("/api/v1/sharing/pairing.sst", new { }, "wrong-token")).StatusCode);

        var first = await PairAsync(host, school.Token);
        var second = await PairAsync(host, school.Token);
        Assert.Equal(first.SchoolId, second.SchoolId);
        Assert.Equal(first.PublicKey, second.PublicKey);
        var key = (await StatusAsync(host)).GetProperty("key");
        Assert.Equal(first.SchoolId, key.GetProperty("schoolId").GetGuid());
        Assert.Equal(SharingService.FingerprintOf(first.PublicKey), key.GetProperty("fingerprint").GetString());
        Assert.Matches("^[0-9A-F]{4}(-[0-9A-F]{4}){3}$", key.GetProperty("fingerprint").GetString());
        Assert.True(key.GetProperty("readable").GetBoolean());
        Assert.Equal(1, key.GetProperty("generation").GetInt32());
        Assert.Equal(["SharingPairingExported", "SharingPairingExported", "SharingKeyCreated"], await SharingEventsAsync(host));
    }

    [Fact]
    public async Task TheApprovedVersionExportsSignedTeacherPrincipalAndZipPackagesThatReadBackValid()
    {
        await using var host = new TestHost();
        var fixture = await ApprovedAsync(host);
        var trusted = await PairAsync(host, fixture.Token);
        var status = await StatusAsync(host);
        var approved = status.GetProperty("approved");
        Assert.False(approved.GetProperty("conflicts").GetBoolean());
        Assert.Equal("2027-06-30", approved.GetProperty("expiresOn").GetString());
        var teachers = status.GetProperty("teachers").EnumerateArray().ToArray();
        Assert.Equal(2, teachers.Length);
        Assert.Equal(fixture.Lessons.Count, teachers.Sum(teacher => teacher.GetProperty("lessons").GetInt32()));
        // The status never carries the private key or its protected form.
        var raw = await (await host.Client.GetAsync("/api/v1/sharing/")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("rotected", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("seed", raw, StringComparison.OrdinalIgnoreCase);

        var teacherId = teachers[0].GetProperty("id").GetInt64();
        var response = await host.PostAsync($"/api/v1/sharing/teachers/{teacherId}/package.sst", new { }, fixture.Token);
        Assert.EndsWith(".sst", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName, StringComparison.Ordinal);
        var teacher = PhonePackageReader.Read(await BytesAsync(response), trusted, Today);
        Assert.Equal(PackageReadStatus.Valid, teacher.Status);
        var content = teacher.Package!.Timetable!;
        Assert.Equal(PhonePackageKind.Teacher, teacher.Package.Header.Kind);
        Assert.Equal(teachers[0].GetProperty("name").GetString(), Assert.Single(content.Teachers).Name);
        Assert.Equal(teachers[0].GetProperty("lessons").GetInt32(), content.Lessons.Count);
        Assert.Equal(approved.GetProperty("number").GetInt32(), content.TimetableVersion);
        Assert.Equal("2026-2027", content.AcademicYear);
        Assert.Equal(new DateOnly(2027, 6, 30), content.ExpiresOn);
        Assert.Equal("Asia/Baghdad", content.TimeZone);
        Assert.Single(content.Sessions);
        Assert.Empty(content.SessionDays);
        Assert.Contains(content.Sessions[0].Periods, period => !period.IsBreak);
        Assert.All(content.Lessons, lesson => Assert.InRange(lesson.Lesson, 1, content.Sessions[0].Periods.Count(period => !period.IsBreak)));

        // One scan (DECISIONS_LOG #111): the same file on a phone that was never paired is valid and proposes exactly the school of the pairing code.
        var firstScan = PhonePackageReader.Read(await BytesAsync(await host.PostAsync($"/api/v1/sharing/teachers/{teacherId}/package.sst", new { }, fixture.Token)), null, Today);
        Assert.Equal(PackageReadStatus.Valid, firstScan.Status);
        Assert.Equal(trusted.SchoolId, firstScan.NewTrust!.SchoolId);
        Assert.Equal(trusted.PublicKey, firstScan.NewTrust.PublicKey);

        var principal = PhonePackageReader.Read(await BytesAsync(await host.PostAsync("/api/v1/sharing/principal.sst", new { }, fixture.Token)), trusted, Today);
        Assert.Equal(PackageReadStatus.Valid, principal.Status);
        Assert.Equal(PhonePackageKind.Principal, principal.Package!.Header.Kind);
        Assert.Equal(fixture.Lessons.Count, principal.Package.Timetable!.Lessons.Count);
        Assert.Equal(2, principal.Package.Timetable.Sections.Count);
        Assert.Equal(2, principal.Package.Timetable.Teachers.Count);

        using var zip = new ZipArchive(new MemoryStream(await BytesAsync(await host.PostAsync("/api/v1/sharing/teachers.zip", new { }, fixture.Token))));
        Assert.Equal(2, zip.Entries.Count);
        foreach (var entry in zip.Entries)
        {
            Assert.EndsWith($"-v{content.TimetableVersion}.sst", entry.FullName, StringComparison.Ordinal);
            await using var stream = entry.Open();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            Assert.Equal(PackageReadStatus.Valid, PhonePackageReader.Read(buffer.ToArray(), trusted, Today).Status);
        }
        Assert.Contains(zip.Entries, entry => entry.FullName.StartsWith(SharingService.SafeFileName(teachers[0].GetProperty("name").GetString()!), StringComparison.Ordinal));

        await AssertApiErrorAsync(await host.PostAsync("/api/v1/sharing/teachers/999999/package.sst", new { }, fixture.Token), ErrorCodes.NotFound);
        Assert.Equal(["SharingAllTeachersExported", "SharingPrincipalExported", "SharingTeacherExported", "SharingTeacherExported", "SharingPairingExported", "SharingKeyCreated"],
            await SharingEventsAsync(host));
    }

    [Fact]
    public async Task ADoubleShiftSchoolSendsBothTimingsTheSemesterMappingAndOnlyItsActiveHolidays()
    {
        await using var host = new TestHost();
        var (school, _) = await ReadySchoolAsync(host);
        static string Clock(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";
        var evening = Enumerable.Range(0, 6).SelectMany(index => index == 3
            ? new object[] { new { kind = "break", startTime = Clock(13 * 60 + 120), endTime = Clock(13 * 60 + 130) }, new { kind = "lesson", startTime = Clock(13 * 60 + 130), endTime = Clock(13 * 60 + 170) } }
            : [new { kind = "lesson", startTime = Clock(13 * 60 + index * 40 + (index > 3 ? 10 : 0)), endTime = Clock(13 * 60 + index * 40 + 40 + (index > 3 ? 10 : 0)) }]).ToArray();
        int[] week = [7, 1, 2, 3, 4];
        var days = week.SelectMany(day => new object[]
        {
            new { term = 1, day, session = day is 7 or 1 ? "morning" : "evening" },
            new { term = 2, day, session = day is 7 or 1 ? "evening" : "morning" },
        }).ToArray();
        var plan = await host.PutAsync("/api/v1/session-plan/", new { system = "twoSessions", timings = new[] { new { session = "evening", periods = evening } }, days, version = 0 }, school.Token);
        Assert.True(plan.StatusCode == HttpStatusCode.OK, await plan.Content.ReadAsStringAsync());
        foreach (var (title, kind, affects) in new[] { ("عطلة رسمية", "officialHoliday", true), ("امتحانات", "exam", true), ("عطلة لا تؤثر", "schoolHoliday", false) })
            Assert.Equal(HttpStatusCode.Created, (await host.PostAsync("/api/v1/calendar-days/", new { title, startDate = "2026-11-10", kind, affectsSchedule = affects, version = 0 }, school.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await host.PostAsync("/api/v1/calendar-days/", new { title = "خارج السنة", startDate = "2027-08-01", kind = "officialHoliday", affectsSchedule = true, version = 0 }, school.Token)).StatusCode);

        var fixture = await ApprovedFromAsync(host, school.Token, school.Root);
        var trusted = await PairAsync(host, school.Token);
        var content = PhonePackageReader.Read(await BytesAsync(await host.PostAsync("/api/v1/sharing/principal.sst", new { }, school.Token)), trusted, Today).Package!.Timetable!;
        Assert.Equal([1, 3], content.Sessions.Select(session => session.Session));
        var eveningRows = content.Sessions[1].Periods;
        Assert.Equal(7, eveningRows.Count);
        Assert.True(eveningRows[3].IsBreak);
        Assert.Equal(13 * 60, eveningRows[0].StartMinute);
        Assert.Equal(10, content.SessionDays.Count);
        Assert.Equal(1, content.SessionDays.Single(day => day.Semester == 1 && day.Day == 7).Session);
        Assert.Equal(3, content.SessionDays.Single(day => day.Semester == 2 && day.Day == 7).Session);
        Assert.Equal(week, content.WorkingDays);
        Assert.Equal(7, content.WeekStart);
        // The year also has the Iraqi template holidays (some approximate). DECISIONS_LOG #107: every enabled entry that affects the timetable is sent
        // with its kind (an exam day too); entries that do not affect it and dates outside the year are left out.
        Assert.Contains(content.Holidays, holiday => (holiday.Title, holiday.Start, holiday.Kind) == ("عطلة رسمية", new DateOnly(2026, 11, 10), PackageDayKind.OfficialHoliday));
        Assert.Contains(content.Holidays, holiday => (holiday.Title, holiday.Kind) == ("امتحانات", PackageDayKind.Exam));
        Assert.Contains(content.Holidays, holiday => holiday.Approximate);
        Assert.DoesNotContain(content.Holidays, holiday => holiday.Title is "عطلة لا تؤثر" or "خارج السنة");
        Assert.All(content.Holidays, holiday => Assert.True(holiday.End >= content.YearStart && holiday.Start <= content.YearEnd));
        Assert.Equal(content.Holidays.OrderBy(holiday => holiday.Start), content.Holidays);
        Assert.Equal(fixture, content.TimetableVersion);
    }

    /// <summary>Generates and approves a version of an already prepared school; returns its number.</summary>
    private static async Task<int> ApprovedFromAsync(TestHost host, string token, string root)
    {
        var started = await host.PostAsync($"{root}/generation/runs", new { timeLimitSeconds = 10, deterministic = true, seed = 42 }, token);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        var run = await WaitForEndAsync(host, (await JsonAsync(started)).GetProperty("id").GetInt64());
        Assert.Equal("completed", run.GetProperty("status").GetString());
        var id = run.GetProperty("timetableVersionId").GetInt64();
        var summary = await LifecycleApiTests.SummaryAsync(host, id);
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/v1/timetables/{id}/approve", new { version = summary.GetProperty("version").GetInt32() }, token)).StatusCode);
        return summary.GetProperty("number").GetInt32();
    }

    [Fact]
    public async Task AConflictWithTodaysDataBlocksEveryTimetableExportWithItsReason()
    {
        await using var host = new TestHost();
        var fixture = await ApprovedAsync(host);
        var busiest = fixture.Lessons.GroupBy(lesson => (lesson.TeacherId, lesson.Day)).OrderByDescending(group => group.Count()).First().Key;
        await CurrentDataTests.UpdateTeacherAsync(host, fixture.Token, busiest.TeacherId, [busiest.Day]);

        Assert.True((await StatusAsync(host)).GetProperty("approved").GetProperty("conflicts").GetBoolean());
        foreach (var path in new[] { "/api/v1/sharing/teachers.zip", "/api/v1/sharing/principal.sst", $"/api/v1/sharing/teachers/{busiest.TeacherId}/package.sst" })
            await AssertApiErrorAsync(await host.PostAsync(path, new { }, fixture.Token), ErrorCodes.ShareTimetableConflicts);
        Assert.DoesNotContain(await SharingEventsAsync(host), name => name.EndsWith("Exported", StringComparison.Ordinal) && name != "SharingPairingExported");
    }

    [Fact]
    public async Task AnEndedYearCannotBeShared()
    {
        await using var host = new TestHost();
        var fixture = await ApprovedAsync(host);
        var year = await JsonAsync(await host.Client.GetAsync($"/api/v1/academic-years/{fixture.YearId}"));
        var updated = await host.PutAsync($"/api/v1/academic-years/{fixture.YearId}", new
        {
            label = year.GetProperty("label").GetString(), startDate = "2026-01-01", endDate = "2026-10-02", version = year.GetProperty("version").GetInt32(),
        }, fixture.Token);
        Assert.True(updated.StatusCode == HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        Assert.True((await StatusAsync(host)).GetProperty("approved").GetProperty("yearEnded").GetBoolean());
        await AssertApiErrorAsync(await host.PostAsync("/api/v1/sharing/principal.sst", new { }, fixture.Token), ErrorCodes.ShareYearEnded);
    }

    [Fact]
    public async Task RecreatingTheKeyNeedsBothConfirmationsKeepsTheSchoolIdAndOldPhonesSeeKeyChanged()
    {
        await using var host = new TestHost();
        var fixture = await ApprovedAsync(host);
        var trusted = await PairAsync(host, fixture.Token);
        await AssertApiErrorAsync(await host.PostAsync("/api/v1/sharing/key/regenerate", new { confirm = true, confirmRepair = false }, fixture.Token), ErrorCodes.ShareKeyConfirmationRequired);
        await AssertApiErrorAsync(await host.PostAsync("/api/v1/sharing/key/regenerate", new { confirm = false, confirmRepair = true }, fixture.Token), ErrorCodes.ShareKeyConfirmationRequired);

        var recreated = await JsonAsync(await host.PostAsync("/api/v1/sharing/key/regenerate", new { confirm = true, confirmRepair = true }, fixture.Token));
        Assert.Equal(2, recreated.GetProperty("generation").GetInt32());
        Assert.Equal(trusted.SchoolId, recreated.GetProperty("schoolId").GetGuid());
        Assert.NotEqual(SharingService.FingerprintOf(trusted.PublicKey), recreated.GetProperty("fingerprint").GetString());

        var principal = await BytesAsync(await host.PostAsync("/api/v1/sharing/principal.sst", new { }, fixture.Token));
        Assert.Equal(PackageReadStatus.KeyChanged, PhonePackageReader.Read(principal, trusted, Today).Status);
        Assert.Equal(PackageReadStatus.Valid, PhonePackageReader.Read(principal, await PairAsync(host, fixture.Token), Today).Status);
        Assert.Contains("SharingKeyRegenerated", await SharingEventsAsync(host));
    }

    [Fact]
    public async Task AKeyThisWindowsUserCannotReadIsReportedAndCanBeReplaced()
    {
        var unreadable = new UnreadableAfterFirstUse();
        await using var host = new TestHost(configureServices: services =>
        {
            services.RemoveAll<ISchoolKeyCrypto>();
            services.AddSingleton<ISchoolKeyCrypto>(unreadable);
        });
        var fixture = await ApprovedAsync(host);
        await PairAsync(host, fixture.Token);
        unreadable.Broken = true;
        Assert.False((await StatusAsync(host)).GetProperty("key").GetProperty("readable").GetBoolean());
        await AssertApiErrorAsync(await host.PostAsync("/api/v1/sharing/principal.sst", new { }, fixture.Token), ErrorCodes.ShareKeyUnreadable);
        await AssertApiErrorAsync(await host.PostAsync("/api/v1/sharing/pairing.sst", new { }, fixture.Token), ErrorCodes.ShareKeyUnreadable);

        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync("/api/v1/sharing/key/regenerate", new { confirm = true, confirmRepair = true }, fixture.Token)).StatusCode);
        unreadable.Broken = false;
        Assert.Equal(PackageReadStatus.Valid, PhonePackageReader.Read(await BytesAsync(await host.PostAsync("/api/v1/sharing/principal.sst", new { }, fixture.Token)),
            await PairAsync(host, fixture.Token), Today).Status);
    }

    private const string KeyPassword = "كلمة مرور المفتاح 2026";

    private static async Task<byte[]> BackupKeyAsync(TestHost host, string token) =>
        await BytesAsync(await host.PostAsync("/api/v1/sharing/key/backup.sstkey", new { password = KeyPassword, confirmPassword = KeyPassword }, token));

    private static Task<HttpResponseMessage> ImportKeyAsync(TestHost host, string token, byte[] file, string password = KeyPassword, bool confirmReplace = false) =>
        host.PostAsync("/api/v1/sharing/key/import", new { file = Convert.ToBase64String(file), password, confirmReplace }, token);

    [Fact]
    public async Task ASavedKeyMovesToANewComputerWithTheSameSchoolIdSoPairedPhonesKeepWorking()
    {
        await using var oldComputer = new TestHost();
        var fixture = await ApprovedAsync(oldComputer);
        var trusted = await PairAsync(oldComputer, fixture.Token);
        Assert.Equal(JsonValueKind.Null, (await StatusAsync(oldComputer)).GetProperty("key").GetProperty("backedUpAt").ValueKind);

        // Password rules are the account's: required, 8 or more characters, typed twice the same.
        var weak = await AssertApiErrorAsync(await oldComputer.PostAsync("/api/v1/sharing/key/backup.sstkey", new { password = "قصيرة", confirmPassword = "قصيرة" }, fixture.Token), ErrorCodes.ValidationFailed);
        Assert.Equal(ErrorCodes.PasswordTooShort, Assert.Single(weak.Errors).Code);
        var mismatch = await AssertApiErrorAsync(await oldComputer.PostAsync("/api/v1/sharing/key/backup.sstkey", new { password = KeyPassword, confirmPassword = KeyPassword + "!" }, fixture.Token), ErrorCodes.ValidationFailed);
        Assert.Equal((nameof(BackupSchoolKeyCommand.ConfirmPassword), ErrorCodes.PasswordMismatch), (Assert.Single(mismatch.Errors).Field, mismatch.Errors[0].Code));

        var response = await oldComputer.PostAsync("/api/v1/sharing/key/backup.sstkey", new { password = KeyPassword, confirmPassword = KeyPassword }, fixture.Token);
        var name = response.Content.Headers.ContentDisposition!.FileName!.Trim('"');
        Assert.Equal($"school-key-{SharingService.FingerprintOf(trusted.PublicKey)}.sstkey", name);
        var keyFile = await BytesAsync(response);
        Assert.NotEqual(JsonValueKind.Null, (await StatusAsync(oldComputer)).GetProperty("key").GetProperty("backedUpAt").ValueKind);

        await using var newComputer = new TestHost();
        var school = await ApprovedAsync(newComputer);
        await AssertApiErrorAsync(await ImportKeyAsync(newComputer, school.Token, keyFile, "خطأ في كلمة المرور"), ErrorCodes.ShareKeyFileWrongPassword);
        var tampered = keyFile.ToArray();
        tampered[100] ^= 0x01;
        await AssertApiErrorAsync(await ImportKeyAsync(newComputer, school.Token, tampered), ErrorCodes.ShareKeyFileTampered);
        await AssertApiErrorAsync(await ImportKeyAsync(newComputer, school.Token, [1, 2, 3]), ErrorCodes.ShareKeyFileInvalid);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await newComputer.PostAsync("/api/v1/sharing/key/import", new { file = "not base64!", password = KeyPassword, confirmReplace = false }, school.Token)).StatusCode);

        var imported = await JsonAsync(await ImportKeyAsync(newComputer, school.Token, keyFile));
        Assert.True(imported.GetProperty("changed").GetBoolean());
        Assert.Equal(trusted.SchoolId, imported.GetProperty("key").GetProperty("schoolId").GetGuid());
        Assert.Equal(SharingService.FingerprintOf(trusted.PublicKey), imported.GetProperty("key").GetProperty("fingerprint").GetString());
        // A phone paired with the OLD computer accepts what the new one exports.
        var principal = await BytesAsync(await newComputer.PostAsync("/api/v1/sharing/principal.sst", new { }, school.Token));
        Assert.Equal(PackageReadStatus.Valid, PhonePackageReader.Read(principal, trusted, Today).Status);
        // Importing the same file again changes nothing.
        Assert.False((await JsonAsync(await ImportKeyAsync(newComputer, school.Token, keyFile))).GetProperty("changed").GetBoolean());
        Assert.Contains("SharingKeyImported", await SharingEventsAsync(newComputer));
        Assert.Contains("SharingKeyBackedUp", await SharingEventsAsync(oldComputer));

        // Recreating the key forgets that it was saved: the page asks again.
        await oldComputer.PostAsync("/api/v1/sharing/key/regenerate", new { confirm = true, confirmRepair = true }, fixture.Token);
        Assert.Equal(JsonValueKind.Null, (await StatusAsync(oldComputer)).GetProperty("key").GetProperty("backedUpAt").ValueKind);
    }

    [Fact]
    public async Task ImportingOverAKeyThatStillWorksNeedsAConfirmationButAnUnreadableOneIsReplacedDirectly()
    {
        await using var source = new TestHost();
        var origin = await ApprovedAsync(source);
        var keyFile = await BackupKeyAsync(source, origin.Token);
        var trusted = await PairAsync(source, origin.Token);

        var unreadable = new UnreadableAfterFirstUse();
        await using var target = new TestHost(configureServices: services =>
        {
            services.RemoveAll<ISchoolKeyCrypto>();
            services.AddSingleton<ISchoolKeyCrypto>(unreadable);
        });
        var school = await ApprovedAsync(target);
        await PairAsync(target, school.Token);

        // A different key that works here: replacing it would break the phones paired with it.
        await AssertApiErrorAsync(await ImportKeyAsync(target, school.Token, keyFile), ErrorCodes.ShareKeyImportConfirmationRequired);

        // After a restore DPAPI cannot read, the import is the way back; another school id still asks first.
        unreadable.Broken = true;
        await AssertApiErrorAsync(await ImportKeyAsync(target, school.Token, keyFile), ErrorCodes.ShareKeyImportConfirmationRequired);
        Assert.Equal(HttpStatusCode.OK, (await ImportKeyAsync(target, school.Token, keyFile, confirmReplace: true)).StatusCode);
        unreadable.Broken = false;
        Assert.Equal(PackageReadStatus.Valid, PhonePackageReader.Read(await BytesAsync(await target.PostAsync("/api/v1/sharing/principal.sst", new { }, school.Token)), trusted, Today).Status);

        // The same school id but unreadable here (a restored backup): replaced without a question.
        unreadable.Broken = true;
        var same = await JsonAsync(await ImportKeyAsync(target, school.Token, keyFile));
        Assert.True(same.GetProperty("changed").GetBoolean());
    }

    [Fact]
    public void TheRealProtectorRoundTripsAndRejectsAForeignBlob()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var crypto = new DpapiSchoolKeyCrypto();
        var (publicKey, seed) = crypto.Generate();
        Assert.Equal(publicKey, Ed25519.PublicKeyOf(seed));
        var blob = crypto.Protect(seed);
        Assert.False(blob.AsSpan().IndexOf(seed) >= 0);
        Assert.Equal(seed, crypto.Unprotect(blob));
        Assert.Null(crypto.Unprotect([.. blob[..^1], (byte)(blob[^1] ^ 0xFF)]));
        Assert.Null(crypto.Unprotect([1, 2, 3]));
    }

    /// <summary>A protector that stops reading its blobs on demand (a backup restored under another Windows account).</summary>
    private sealed class UnreadableAfterFirstUse : ISchoolKeyCrypto
    {
        public bool Broken { get; set; }

        public (byte[] PublicKey, byte[] PrivateSeed) Generate()
        {
            var seed = System.Security.Cryptography.RandomNumberGenerator.GetBytes(Ed25519.SeedLength);
            return (Ed25519.PublicKeyOf(seed), seed);
        }

        public byte[] Protect(byte[] privateSeed) => [.. privateSeed];

        public byte[]? Unprotect(byte[] protectedSeed) => Broken ? null : [.. protectedSeed];
    }
}
