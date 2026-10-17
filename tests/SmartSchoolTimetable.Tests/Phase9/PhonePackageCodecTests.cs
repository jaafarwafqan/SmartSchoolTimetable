using System.IO.Compression;
using SmartSchoolTimetable.Application.Sharing;
using SmartSchoolTimetable.Infrastructure.Sharing;

namespace SmartSchoolTimetable.Tests.Phase9;

/// <summary>The .sst envelope and payload (ADR 0056): round trip, and every way a package is refused.</summary>
public sealed class PhonePackageCodecTests
{
    private static readonly PhonePackageWriter Writer = new();

    private static byte[] ValidTeacher() => Writer.Write(SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA), SstVectors.SeedA);

    private static PackageReadStatus Read(byte[] bytes) => PhonePackageReader.Read(bytes, SstVectors.TrustedA, SstVectors.Today).Status;

    [Fact]
    public void APackageRoundTripsAndTheSameInputGivesTheSameBytes()
    {
        var bytes = ValidTeacher();
        Assert.Equal(bytes, ValidTeacher());
        Assert.Equal("SST"u8.ToArray(), bytes[..3]);
        Assert.Equal(1, bytes[3]);
        var result = PhonePackageReader.Read(bytes, SstVectors.TrustedA, SstVectors.Today);
        Assert.Equal(PackageReadStatus.Valid, result.Status);
        Assert.Equal(SstVectors.ToJson(SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA)), SstVectors.ToJson(result.Package!));
        Assert.Equal(bytes, PhonePackageFormat.FromQrText(PhonePackageFormat.ToQrText(bytes)));
    }

    [Fact]
    public void ChangingAnySingleByteAfterTheEnvelopeHeadIsDetected()
    {
        var bytes = ValidTeacher();
        for (var index = 4; index < bytes.Length; index++)
        {
            var copy = bytes.ToArray();
            copy[index] ^= 0x01;
            // A flip inside the compressed body may even decode to another school id or key id; it is never accepted.
            Assert.NotEqual(PackageReadStatus.Valid, Read(copy));
            Assert.NotEqual(PackageReadStatus.Expired, Read(copy));
        }
    }

    [Fact]
    public void NotAPackageNewerEnvelopeAndOversizedInputAreRefusedBeforeAnyParsing()
    {
        var bytes = ValidTeacher();
        Assert.Equal(PackageReadStatus.Malformed, Read([]));
        Assert.Equal(PackageReadStatus.Malformed, Read(bytes[..PhonePackageFormat.BodyOffset]));
        Assert.Equal(PackageReadStatus.Malformed, Read([(byte)'X', .. bytes[1..]]));
        Assert.Equal(PackageReadStatus.Malformed, Read([.. bytes[..3], 0, .. bytes[4..]]));
        Assert.Equal(PackageReadStatus.NewerFormat, Read([.. bytes[..3], 2, .. bytes[4..]]));
        Assert.Equal(PackageReadStatus.Malformed, Read([.. bytes, .. new byte[PhonePackageFormat.MaxPackageBytes]]));
    }

    [Fact]
    public void ADecompressionBombIsRefused()
    {
        // 3 MiB of zeros compress to a few KiB; the reader stops at 2 MiB.
        using var compressed = new MemoryStream();
        using (var deflate = new DeflateStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(new byte[3 * 1024 * 1024]);
        byte[] head = [.. "SST"u8, 1];
        var body = compressed.ToArray();
        var signature = Ed25519.Sign(SstVectors.SeedA, PhonePackageFormat.SignedMessage(head, body));
        Assert.Equal(PackageReadStatus.Tampered, Read([.. head, .. signature, .. body]));
    }

    [Fact]
    public void ALessonPointingOutsideItsListsIsRefusedEvenWhenSigned()
    {
        var content = SstVectors.TeacherContent();
        var broken = content with { Lessons = [.. content.Lessons, new PackageLesson(7, 1, 0, 0, 5)] };
        Assert.Equal(PackageReadStatus.Tampered, Read(Writer.Write(SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA, broken), SstVectors.SeedA)));
    }

    [Fact]
    public void APairingCodeIsSelfSignedAndItsKeyIdMustMatchItsKey()
    {
        var pairing = Writer.Write(SstVectors.Pairing(SstVectors.SchoolA, SstVectors.SeedA), SstVectors.SeedA);
        var read = PhonePackageReader.Read(pairing, null, SstVectors.Today);
        Assert.Equal(PackageReadStatus.Valid, read.Status);
        Assert.Equal(SstVectors.TrustedA.PublicKey, read.Package!.PublicKey);

        // Another key claiming school A's key id, or school A's key with a forged id: refused.
        var forged = SstVectors.Pairing(SstVectors.SchoolA, SstVectors.SeedB) with { Header = SstVectors.Pairing(SstVectors.SchoolA, SstVectors.SeedA).Header };
        Assert.Equal(PackageReadStatus.Tampered, PhonePackageReader.Read(Writer.Write(forged, SstVectors.SeedB), null, SstVectors.Today).Status);
    }

    [Fact]
    public void OneScanATeacherPackagePairsAnUnpairedPhoneAndAPairedPhoneMustMatchTheStoredKey()
    {
        var bytes = ValidTeacher();
        // Unpaired: valid, and the package proposes its own school (the phone confirms the name, then stores it).
        var first = PhonePackageReader.Read(bytes, null, SstVectors.Today);
        Assert.Equal(PackageReadStatus.Valid, first.Status);
        Assert.Equal(SstVectors.SchoolA, first.NewTrust!.SchoolId);
        Assert.Equal(SstVectors.TrustedA.PublicKey, first.NewTrust.PublicKey);
        // Paired with the same key: valid, nothing new to trust.
        Assert.Null(PhonePackageReader.Read(bytes, first.NewTrust, SstVectors.Today).NewTrust);
        // Paired, then a package signed with a recreated key of the same school: «تغيّر مفتاح المدرسة», never silently accepted.
        var recreated = Writer.Write(SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA2), SstVectors.SeedA2);
        var changed = PhonePackageReader.Read(recreated, first.NewTrust, SstVectors.Today);
        Assert.Equal((PackageReadStatus.KeyChanged, (TrustedSchool?)null), (changed.Status, changed.NewTrust));
        // An unpaired phone does not pair with an expired package.
        var expired = PhonePackageReader.Read(bytes, null, new DateOnly(2027, 7, 1));
        Assert.Equal((PackageReadStatus.Expired, (TrustedSchool?)null), (expired.Status, expired.NewTrust));
    }

    [Fact]
    public void ACarriedKeyThatDidNotSignThePackageIsRefusedPairedOrNot()
    {
        // Re-signed by B but still claiming A's key id and carrying A's key: the signature does not verify.
        var forged = Writer.Write(SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA), SstVectors.SeedB);
        Assert.Equal(PackageReadStatus.Tampered, PhonePackageReader.Read(forged, null, SstVectors.Today).Status);
        Assert.Equal(PackageReadStatus.Tampered, PhonePackageReader.Read(forged, SstVectors.TrustedA, SstVectors.Today).Status);
        // Signed by A but carrying B's key: the carried key must be the signing key.
        var mismatch = Writer.Write(SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA) with { PublicKey = Ed25519.PublicKeyOf(SstVectors.SeedB) }, SstVectors.SeedA);
        Assert.Equal(PackageReadStatus.Tampered, PhonePackageReader.Read(mismatch, null, SstVectors.Today).Status);
        Assert.Equal(PackageReadStatus.Tampered, PhonePackageReader.Read(mismatch, SstVectors.TrustedA, SstVectors.Today).Status);
        // Without any key a package can only be read by a paired phone.
        var keyless = Writer.Write(SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA) with { PublicKey = null }, SstVectors.SeedA);
        Assert.Equal(PackageReadStatus.NotPaired, PhonePackageReader.Read(keyless, null, SstVectors.Today).Status);
        Assert.Equal(PackageReadStatus.Valid, PhonePackageReader.Read(keyless, SstVectors.TrustedA, SstVectors.Today).Status);
    }

    /// <summary>A teacher of the owner-shaped school with the program's own Iraqi holiday template for 2026-2027 and a mid-year break.</summary>
    private static TimetableContent TeacherOfBigSchool(int sections, int subjects)
    {
        var content = SstVectors.TeacherContent();
        var holidays = Application.Calendar.IraqHolidayTemplate.PlaceIn(new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30))
            .Select(item => new PackageHoliday(item.Start, item.End, item.Holiday.Title, item.Holiday.Hijri, item.Holiday.ByDecision, PackageDayKind.OfficialHoliday,
                PackageHolidayCodes.CodeOf(item.Holiday.Key, item.Holiday.Title)))
            .Append(new PackageHoliday(new DateOnly(2027, 1, 16), new DateOnly(2027, 1, 31), "عطلة نصف السنة", false, false, PackageDayKind.SchoolHoliday))
            .OrderBy(item => item.Start).ToArray();
        string[] subjectNames = ["الرياضيات", "الفيزياء", "الكيمياء", "الحاسوب"];
        return content with
        {
            Subjects = subjectNames.Take(subjects).Select((name, index) => new PackageSubject(name, index + 1)).ToArray(),
            Sections = Enumerable.Range(0, sections).Select(index => new PackageSection(index < 6 ? "الرابع الإعدادي" : "الخامس العلمي", ((char)('أ' + index)).ToString())).ToArray(),
            Holidays = holidays,
            Teachers = [new("عبد الرحمن محمد عبد الله الحسيني", "عبد الرحمن")],
            Lessons = content.Lessons.Select((lesson, index) => lesson with { Subject = index % subjects, Section = index % sections }).ToArray(),
        };
    }

    private static string QrTextOf(TimetableContent content) =>
        PhonePackageFormat.ToQrText(Writer.Write(SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA, content), SstVectors.SeedA));

    [Theory]
    [InlineData(6, 2)]
    [InlineData(6, 1)]
    [InlineData(4, 2)]
    public void AFullTimeTeacherOfABigSchoolFitsOneCodeWithTheIraqiCalendar(int sections, int subjects)
    {
        // 30 lessons, double shift, the school's public key (one scan) and every holiday of the Iraqi template with its Arabic title.
        var text = QrTextOf(TeacherOfBigSchool(sections, subjects));
        Assert.True(new QrEncoder().Encode(text, SharingService.MaxQrVersion) is not null, $"{text.Length} characters do not fit version {SharingService.MaxQrVersion}.");
    }

    [Fact]
    public void ATeacherSpreadOverElevenSectionsAndFourSubjectsNeedsTheFile()
    {
        // The densest case measured (30 lessons in 11 sections, 4 subjects, the Iraqi calendar): above version 25, so the page offers the
        // .sst file instead of the code (DECISIONS_LOG #110). Recorded so a format change that shrinks or grows packages is noticed.
        var text = QrTextOf(TeacherOfBigSchool(11, 4));
        Assert.InRange(text.Length, 998, 1400);
        Assert.Null(new QrEncoder().Encode(text, SharingService.MaxQrVersion));
    }

    [Fact]
    public void EveryTemplateHolidayHasAFixedCodeAndOnlyAnUnchangedTitleTravelsAsItsCode()
    {
        Assert.Equal(Application.Calendar.IraqHolidayTemplate.Holidays.Select(holiday => holiday.Key).Order(StringComparer.Ordinal),
            PackageHolidayCodes.Table.Values.Select(entry => entry.Key).Order(StringComparer.Ordinal));
        Assert.Equal(PackageHolidayCodes.Table.Count, PackageHolidayCodes.Table.Keys.Distinct().Count());
        Assert.Equal(12, PackageHolidayCodes.CodeOf("eid-al-fitr", "عيد الفطر المبارك"));
        Assert.Equal(0, PackageHolidayCodes.CodeOf("eid-al-fitr", "عيد الفطر (ثلاثة أيام)"));
        Assert.Equal(0, PackageHolidayCodes.CodeOf(null, "عطلة"));
        Assert.Null(PackageHolidayCodes.TitleOf(999));

        // On the wire the title is empty and the code carries it; the reader gives the title back.
        var content = SstVectors.TeacherContent() with { Holidays = [new(new DateOnly(2027, 3, 10), new DateOnly(2027, 3, 12), "عيد الفطر المبارك", true, false, PackageDayKind.OfficialHoliday, 12)] };
        var package = SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA, content);
        var read = PhonePackageReader.Read(Writer.Write(package, SstVectors.SeedA), SstVectors.TrustedA, SstVectors.Today).Package!.Timetable!.Holidays.Single();
        Assert.Equal(("عيد الفطر المبارك", 12), (read.Title, read.TitleCode));
        Assert.True(PhonePackageWriter.Payload(package).AsSpan().IndexOf("عيد الفطر"u8) < 0, "The template title must not be in the payload.");
    }

    [Fact]
    public void TheLastDayOfTheYearIsStillValidAndTheNextDayIsExpired()
    {
        var bytes = ValidTeacher();
        Assert.Equal(PackageReadStatus.Valid, PhonePackageReader.Read(bytes, SstVectors.TrustedA, new DateOnly(2027, 6, 30)).Status);
        Assert.Equal(PackageReadStatus.Expired, PhonePackageReader.Read(bytes, SstVectors.TrustedA, new DateOnly(2027, 7, 1)).Status);
    }

    [Fact]
    public void ATeacherPackageOfATypicalSchoolFitsTheQrBudget()
    {
        // 30 lessons, double shift, five days off, and (one scan) the school's public key: the QR text still fits a version-25 QR at
        // level M (997 characters in byte mode), and QRCoder places it at 25 or below.
        var text = PhonePackageFormat.ToQrText(ValidTeacher());
        Assert.True(text.Length <= 997, $"QR text is {text.Length} characters.");
        Assert.InRange(new QrEncoder().Encode(text, SharingService.MaxQrVersion)!.Version, 1, SharingService.MaxQrVersion);
    }
}
