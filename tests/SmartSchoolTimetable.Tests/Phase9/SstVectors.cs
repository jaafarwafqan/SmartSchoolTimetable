using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartSchoolTimetable.Application.Sharing;
using SmartSchoolTimetable.Infrastructure.Sharing;

namespace SmartSchoolTimetable.Tests.Phase9;

/// <summary>
/// The shared .sst test vectors (tests/vectors, also copied into the phone app's repository). Everything is fictional: the
/// school, the names, and the three Ed25519 keys, whose seeds are derived from public strings and are TEST KEYS ONLY.
/// </summary>
internal static class SstVectors
{
    public static readonly DateOnly Today = new(2026, 11, 1);
    public static readonly Guid SchoolA = Guid.Parse("5a1e0000-0000-4000-8000-00000000000a");
    public static readonly Guid SchoolB = Guid.Parse("5a1e0000-0000-4000-8000-00000000000b");
    public static readonly byte[] SeedA = TestSeed("A");
    public static readonly byte[] SeedA2 = TestSeed("A2");
    public static readonly byte[] SeedB = TestSeed("B");
    public static readonly DateTimeOffset GeneratedAt = new(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <param name="Pairs">Read on an unpaired phone, the package proposes its school for trust (one scan, DECISIONS_LOG #111).</param>
    public sealed record Vector(string File, string Expected, bool Trusted, string? ExpectedJson = null, string? Note = null, bool Pairs = false);

    public static readonly IReadOnlyList<Vector> All =
    [
        new("pairing.sst", "valid", false, "pairing.expected.json", "School A pairing code (self-signed). Scanning it makes school A trusted.", Pairs: true),
        new("valid-teacher.sst", "valid", true, "valid-teacher.expected.json", "Teacher package of school A; valid-teacher.qr.txt holds the same bytes as QR text."),
        new("valid-principal.sst", "valid", true, "valid-principal.expected.json", "Principal package of school A."),
        new("tampered.sst", "tampered", true, null, "valid-teacher with one lesson moved after signing (the signature is the original one)."),
        new("wrong-school.sst", "wrongSchool", true, null, "A valid package of school B, signed with key B."),
        new("key-changed.sst", "keyChanged", true, null, "School A, signed with a recreated key (A2): the phone must scan the new pairing code."),
        new("expired.sst", "expired", true, null, "School A, academic year 2025-2026 (expired on 2026-06-30)."),
        new("future-format.sst", "newerFormat", true, null, "School A, payload format 2 with an unknown key: «حدّث التطبيق»."),
        new("first-scan.sst", "valid", false, "valid-teacher.expected.json", "valid-teacher.sst read on an unpaired phone: valid, and it pairs the phone with school A (after the user confirms the school name).", Pairs: true),
        new("not-paired.sst", "notPaired", false, null, "A teacher package WITHOUT the school's public key (key 6), read on an unpaired phone: it cannot pair, so «امسح رمز ربط المدرسة أولاً»."),
        new("carried-key-mismatch.sst", "tampered", false, null, "Signed with key A but carrying key B: the carried key must be the signing key (key id), on paired and unpaired phones alike."),
    ];

    private static PackageHeader Header(PhonePackageKind kind, Guid school, byte[] seed) =>
        new(SharingService.FormatVersion, kind, school, school == SchoolB ? "مدرسة تجريبية أخرى" : "مدرسة الاختبار النموذجية",
            SharingService.KeyIdOf(Ed25519.PublicKeyOf(seed)), GeneratedAt);

    public static PhonePackage Pairing(Guid school, byte[] seed) =>
        new(Header(PhonePackageKind.Pairing, school, seed), Ed25519.PublicKeyOf(seed), null);

    /// <summary>A timetable package carries the school's public key, so one scan pairs a new phone.</summary>
    public static PhonePackage Teacher(Guid school, byte[] seed, TimetableContent? content = null) =>
        new(Header(PhonePackageKind.Teacher, school, seed), Ed25519.PublicKeyOf(seed), content ?? TeacherContent());

    public static PhonePackage Principal() => new(Header(PhonePackageKind.Principal, SchoolA, SeedA), Ed25519.PublicKeyOf(SeedA), PrincipalContent());

    public static TimetableContent TeacherContent()
    {
        var all = PrincipalContent();
        var lessons = all.Lessons.Where(lesson => lesson.Teacher == 0).ToArray();
        var subjects = lessons.Select(lesson => lesson.Subject).Distinct().Order().ToArray();
        var sections = lessons.Select(lesson => lesson.Section).Distinct().Order().ToArray();
        return all with
        {
            Subjects = subjects.Select(index => all.Subjects[index]).ToArray(),
            Sections = sections.Select(index => all.Sections[index]).ToArray(),
            Teachers = [all.Teachers[0]],
            Lessons = lessons.Select(lesson => lesson with { Subject = Array.IndexOf(subjects, lesson.Subject), Section = Array.IndexOf(sections, lesson.Section), Teacher = 0 }).ToArray(),
        };
    }

    /// <summary>A fictional double-shift school: Sunday–Thursday, mornings and evenings swap between the semesters.</summary>
    public static TimetableContent PrincipalContent()
    {
        PackagePeriod Lesson(int start, int minutes) => new(false, start, start + minutes);
        PackagePeriod Break(int start, int minutes) => new(true, start, start + minutes);
        var morning = new PackageSession(1, [Lesson(480, 45), Lesson(530, 45), Lesson(580, 45), Break(625, 15), Lesson(640, 45), Lesson(690, 45), Lesson(740, 45)]);
        var evening = new PackageSession(3, [Lesson(780, 40), Lesson(825, 40), Lesson(870, 40), Break(910, 10), Lesson(920, 40), Lesson(965, 40), Lesson(1010, 40)]);
        int[] days = [7, 1, 2, 3, 4];
        var sessionDays = days.Select(day => new PackageSessionDay(1, day, day is 7 or 1 ? 1 : 3))
            .Concat(days.Select(day => new PackageSessionDay(2, day, day is 7 or 1 ? 3 : 1))).ToArray();
        var lessons = new List<PackageLesson>();
        foreach (var day in days)
        {
            for (var number = 1; number <= 6; number++)
            {
                // Section 0 and 1 swap the two teachers; teacher 2 has no lessons (always free).
                var teacher = (day + number) % 2;
                lessons.Add(new PackageLesson(day, number, (day + number) % 3, 0, teacher));
                lessons.Add(new PackageLesson(day, number, (day + number + 1) % 3, 1, 1 - teacher));
            }
        }
        return new TimetableContent(
            "2026-2027", 3, new DateTimeOffset(2026, 10, 15, 8, 0, 0, TimeSpan.Zero), new DateOnly(2027, 6, 30), "Asia/Baghdad",
            new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30),
            [new(new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 15)), new(new DateOnly(2027, 2, 1), new DateOnly(2027, 6, 30))],
            [
                new(new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 5), "عطلة تجريبية", true, false),
                new(new DateOnly(2027, 1, 3), new DateOnly(2027, 1, 14), "امتحانات نصف السنة", false, false, PackageDayKind.Exam),
                new(new DateOnly(2027, 1, 16), new DateOnly(2027, 1, 31), "عطلة نصف السنة", false, false, PackageDayKind.SchoolHoliday),
                new(new DateOnly(2027, 3, 21), new DateOnly(2027, 3, 21), "عطلة بقرار", false, true),
                new(new DateOnly(2027, 3, 10), new DateOnly(2027, 3, 12), "عيد الفطر المبارك", true, false, PackageDayKind.OfficialHoliday, 12),
                new(new DateOnly(2027, 1, 6), new DateOnly(2027, 1, 6), "عيد الجيش العراقي", false, false, PackageDayKind.OfficialHoliday, 2),
            ],
            days, 7, [morning, evening], sessionDays,
            [new("رياضيات", 1), new("علوم", 3), new("لغة عربية", 5)],
            [new("الأول المتوسط", "أ"), new("الأول المتوسط", "ب")],
            [new("المعلم التجريبي الأول", "الأول"), new("المعلمة التجريبية الثانية", "الثانية"), new("المعلم التجريبي الثالث", "الثالث")],
            lessons.OrderBy(lesson => lesson.Day).ThenBy(lesson => lesson.Lesson).ThenBy(lesson => lesson.Section).ToArray());
    }

    public static TrustedSchool TrustedA => new(SchoolA, Ed25519.PublicKeyOf(SeedA));

    /// <summary>Writes every vector, the manifest and the expected decodings into <paramref name="directory"/>.</summary>
    public static void WriteAll(string directory)
    {
        Directory.CreateDirectory(directory);
        var writer = new PhonePackageWriter();
        var files = new Dictionary<string, byte[]>
        {
            ["pairing.sst"] = writer.Write(Pairing(SchoolA, SeedA), SeedA),
            ["valid-teacher.sst"] = writer.Write(Teacher(SchoolA, SeedA), SeedA),
            ["valid-principal.sst"] = writer.Write(Principal(), SeedA),
            ["wrong-school.sst"] = writer.Write(Teacher(SchoolB, SeedB), SeedB),
            ["key-changed.sst"] = writer.Write(Teacher(SchoolA, SeedA2), SeedA2),
            ["expired.sst"] = writer.Write(Teacher(SchoolA, SeedA, TeacherContent() with
            {
                AcademicYear = "2025-2026", ExpiresOn = new DateOnly(2026, 6, 30), YearStart = new DateOnly(2025, 9, 1), YearEnd = new DateOnly(2026, 6, 30),
                Semesters = [new(new DateOnly(2025, 9, 1), new DateOnly(2026, 1, 15)), new(new DateOnly(2026, 2, 1), new DateOnly(2026, 6, 30))], Holidays = [],
            }), SeedA),
            ["future-format.sst"] = PhonePackageWriter.Seal(FuturePayload(), SeedA),
        };
        files["first-scan.sst"] = files["valid-teacher.sst"];
        files["not-paired.sst"] = writer.Write(Teacher(SchoolA, SeedA) with { PublicKey = null }, SeedA);
        files["carried-key-mismatch.sst"] = writer.Write(Teacher(SchoolA, SeedA) with { PublicKey = Ed25519.PublicKeyOf(SeedB) }, SeedA);
        // Tampered: the payload of a moved lesson, sealed, then given the original signature.
        var original = files["valid-teacher.sst"];
        var moved = TeacherContent() with { Lessons = [.. TeacherContent().Lessons.Select((lesson, index) => index == 0 ? lesson with { Lesson = 6 } : lesson)] };
        var changed = writer.Write(Teacher(SchoolA, SeedA, moved), SeedA);
        files["tampered.sst"] = [.. changed[..PhonePackageFormat.SignatureOffset], .. original[PhonePackageFormat.SignatureOffset..PhonePackageFormat.BodyOffset], .. changed[PhonePackageFormat.BodyOffset..]];

        foreach (var (name, bytes) in files)
            File.WriteAllBytes(Path.Combine(directory, name), bytes);
        File.WriteAllText(Path.Combine(directory, "valid-teacher.qr.txt"), PhonePackageFormat.ToQrText(files["valid-teacher.sst"]), new UTF8Encoding(false));
        WriteJson(directory, "pairing.expected.json", Pairing(SchoolA, SeedA));
        WriteJson(directory, "valid-teacher.expected.json", Teacher(SchoolA, SeedA));
        WriteJson(directory, "valid-principal.expected.json", Principal());

        var manifest = new
        {
            description = "Shared .sst test vectors (docs/SST_PACKAGE_FORMAT.md). Fictional data; the keys are TEST KEYS ONLY.",
            today = Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            supportedFormat = SharingService.FormatVersion,
            trustedSchool = new
            {
                schoolId = SchoolA,
                publicKeyHex = Convert.ToHexString(TrustedA.PublicKey).ToLowerInvariant(),
                keyIdHex = Convert.ToHexString(SharingService.KeyIdOf(TrustedA.PublicKey)).ToLowerInvariant(),
            },
            vectors = All.Select(vector => new
            {
                file = vector.File,
                sha256 = Convert.ToHexString(SHA256.HashData(files[vector.File])).ToLowerInvariant(),
                expected = vector.Expected,
                trusted = vector.Trusted,
                pairs = vector.Pairs,
                expectedJson = vector.ExpectedJson,
                note = vector.Note,
            }),
        };
        // The holiday code table the phone bundles (docs/SST_PACKAGE_FORMAT.md §4.1).
        File.WriteAllText(Path.Combine(directory, "holiday-codes.json"), JsonSerializer.Serialize(PackageHolidayCodes.Table.OrderBy(entry => entry.Key)
            .Select(entry => new { code = entry.Key, key = entry.Value.Key, title = entry.Value.Title }), Json).ReplaceLineEndings("\n") + "\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "vectors.json"), JsonSerializer.Serialize(manifest, Json).ReplaceLineEndings("\n") + "\n", new UTF8Encoding(false));
        var sums = Directory.EnumerateFiles(directory).Select(Path.GetFileName).Where(name => name is not "SHA256SUMS" and not "README.md").Order(StringComparer.Ordinal)
            .Select(name => $"{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, name!)))).ToLowerInvariant()}  {name}");
        File.WriteAllText(Path.Combine(directory, "SHA256SUMS"), string.Join('\n', sums) + "\n", new UTF8Encoding(false));
    }

    public static string ToJson(PhonePackage package) => JsonSerializer.Serialize(package, Json).ReplaceLineEndings("\n") + "\n";

    private static void WriteJson(string directory, string name, PhonePackage package) =>
        File.WriteAllText(Path.Combine(directory, name), ToJson(package), new UTF8Encoding(false));

    /// <summary>Format 2 with key 99 that format 1 does not know: a format-1 reader must stop at key 0.</summary>
    private static byte[] FuturePayload()
    {
        var current = PhonePackageWriter.Payload(Teacher(SchoolA, SeedA));
        var reader = new System.Formats.Cbor.CborReader(current, System.Formats.Cbor.CborConformanceMode.Canonical);
        var count = reader.ReadStartMap()!.Value;
        var writer = new System.Formats.Cbor.CborWriter(System.Formats.Cbor.CborConformanceMode.Canonical);
        writer.WriteStartMap(count + 1);
        for (var index = 0; index < count; index++)
        {
            var key = reader.ReadInt32();
            writer.WriteInt32(key);
            if (key == PhonePackageFormat.Keys.FormatVersion)
            {
                reader.ReadInt32();
                writer.WriteInt32(2);
            }
            else
            {
                writer.WriteEncodedValue(reader.ReadEncodedValue().Span);
            }
        }
        writer.WriteInt32(99);
        writer.WriteTextString("future");
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static byte[] TestSeed(string name) => SHA256.HashData(Encoding.UTF8.GetBytes($"SmartSchoolTimetable TEST VECTOR KEY {name} - never a real school key"));
}
