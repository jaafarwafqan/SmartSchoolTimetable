namespace SmartSchoolTimetable.Application.Sharing;

/// <summary>What a phone package carries (docs/SST_PACKAGE_FORMAT.md). Stored as an integer in the package; never reorder.</summary>
public enum PhonePackageKind
{
    /// <summary>«رمز ربط المدرسة»: the school id, name and public key (self-signed). Scanned once per phone.</summary>
    Pairing = 0,

    /// <summary>One teacher's lessons.</summary>
    Teacher = 1,

    /// <summary>Every section, teacher and lesson (shared privately with the principal).</summary>
    Principal = 2,
}

/// <param name="FormatVersion">The payload format; a phone refuses a higher one with «حدّث التطبيق».</param>
/// <param name="KeyId">The first 8 bytes of SHA-256 of the signing public key: tells "same school, new key" from tampering.</param>
public sealed record PackageHeader(int FormatVersion, PhonePackageKind Kind, Guid SchoolId, string SchoolName, byte[] KeyId, DateTimeOffset GeneratedAt);

/// <summary>A row of a session's daily timing (minutes after midnight, local school time).</summary>
public sealed record PackagePeriod(bool IsBreak, int StartMinute, int EndMinute);

/// <param name="Session">1 morning (the school's own timing; also a single-session school's only timing), 2 noon, 3 evening.</param>
public sealed record PackageSession(int Session, IReadOnlyList<PackagePeriod> Periods);

/// <summary>On working <paramref name="Day"/> (ISO, 1 = Monday) of <paramref name="Semester"/> (1 or 2) the school attends <paramref name="Session"/>.</summary>
public sealed record PackageSessionDay(int Semester, int Day, int Session);

public sealed record PackageSemester(DateOnly Start, DateOnly End);

/// <param name="Approximate">The date follows the Hijri calendar by calculation; the official announcement may differ by a day or two.</param>
/// <param name="ByDecision">Whether the day is a holiday may be announced yearly by a decision.</param>
public sealed record PackageHoliday(DateOnly Start, DateOnly End, string Title, bool Approximate, bool ByDecision);

public sealed record PackageSubject(string Name, int ColorIndex);

public sealed record PackageSection(string StageName, string Label);

public sealed record PackageTeacher(string Name, string ShortName);

/// <summary>A lesson: day (ISO), lesson number (1..N, breaks not counted) and indexes into the package's subject, section and teacher lists.</summary>
public sealed record PackageLesson(int Day, int Lesson, int Subject, int Section, int Teacher);

/// <param name="ExpiresOn">The last day the package is valid (the end of the academic year).</param>
/// <param name="TimeZone">The school's IANA time zone; every date and time of the package is local to it.</param>
/// <param name="Semesters">The year's semesters in date order (at most two).</param>
/// <param name="SessionDays">Empty for a single-session school (every day uses session 1).</param>
public sealed record TimetableContent(
    string AcademicYear,
    int TimetableVersion,
    DateTimeOffset ApprovedAt,
    DateOnly ExpiresOn,
    string TimeZone,
    DateOnly YearStart,
    DateOnly YearEnd,
    IReadOnlyList<PackageSemester> Semesters,
    IReadOnlyList<PackageHoliday> Holidays,
    IReadOnlyList<int> WorkingDays,
    int WeekStart,
    IReadOnlyList<PackageSession> Sessions,
    IReadOnlyList<PackageSessionDay> SessionDays,
    IReadOnlyList<PackageSubject> Subjects,
    IReadOnlyList<PackageSection> Sections,
    IReadOnlyList<PackageTeacher> Teachers,
    IReadOnlyList<PackageLesson> Lessons);

/// <summary>A decoded package: the header plus exactly one of <paramref name="PublicKey"/> (pairing) or <paramref name="Timetable"/>.</summary>
public sealed record PhonePackage(PackageHeader Header, byte[]? PublicKey, TimetableContent? Timetable);

/// <summary>Ed25519 keys and the DPAPI protection of the private seed (Infrastructure, ADR 0056).</summary>
public interface ISchoolKeyCrypto
{
    /// <summary>A new key pair: the 32-byte public key and the 32-byte private seed.</summary>
    (byte[] PublicKey, byte[] PrivateSeed) Generate();

    /// <summary>Protects the private seed for this Windows user.</summary>
    byte[] Protect(byte[] privateSeed);

    /// <summary>The private seed, or null when this Windows user cannot read it (a backup restored on another computer or account).</summary>
    byte[]? Unprotect(byte[] protectedSeed);
}

/// <summary>Writes the signed binary package (CBOR, deflate, Ed25519; docs/SST_PACKAGE_FORMAT.md).</summary>
public interface IPhonePackageWriter
{
    byte[] Write(PhonePackage package, byte[] privateSeed);
}
