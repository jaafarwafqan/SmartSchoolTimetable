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

/// <summary>A calendar entry that affects the timetable (a holiday, an exam or a special day).</summary>
/// <param name="Approximate">The date follows the Hijri calendar by calculation; the official announcement may differ by a day or two.</param>
/// <param name="ByDecision">Whether the day is a holiday may be announced yearly by a decision.</param>
/// <param name="Kind">What the day is; every day sent has no lessons on the phone and silences its notifications (DECISIONS_LOG #107).</param>
/// <param name="TitleCode">An Iraqi-template holiday with its title unchanged travels as this code (<see cref="PackageHolidayCodes"/>) instead of its title; 0 = none.</param>
public sealed record PackageHoliday(DateOnly Start, DateOnly End, string Title, bool Approximate, bool ByDecision, PackageDayKind Kind = PackageDayKind.OfficialHoliday,
    int TitleCode = 0);

/// <summary>The calendar kind of a day without lessons in a package. Stored as an integer; never reorder.</summary>
public enum PackageDayKind
{
    OfficialHoliday = 0,
    SchoolHoliday = 1,
    Exam = 2,
    SpecialDay = 3,
}

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

/// <summary>
/// A decoded package: the header, the school's public key and, except for a pairing code, the timetable. A teacher or principal
/// package carries the key too, so the first one a phone scans can pair it (DECISIONS_LOG #111: one scan); a package without the key
/// (a hand-made test payload) can only be read by a phone that is already paired.
/// </summary>
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

/// <summary>What a «حفظ نسخة من مفتاح المدرسة» file holds once opened (the seed stays in memory only).</summary>
public sealed record SchoolKeyBackup(Guid SchoolId, int Generation, DateTimeOffset CreatedAt, byte[] PublicKey, byte[] PrivateSeed);

public enum KeyBackupOpenStatus
{
    Opened,

    /// <summary>Not a key file, or a format this build does not know.</summary>
    Invalid,

    /// <summary>The password check failed.</summary>
    WrongPassword,

    /// <summary>The password is right but the content was changed or damaged.</summary>
    Tampered,
}

public sealed record KeyBackupOpenResult(KeyBackupOpenStatus Status, SchoolKeyBackup? Backup);

/// <summary>The password-protected .sstkey file (docs/SST_PACKAGE_FORMAT.md §6.1; Infrastructure).</summary>
public interface ISchoolKeyBackupFile
{
    byte[] Seal(SchoolKeyBackup backup, string password);

    KeyBackupOpenResult Open(byte[] file, string password);
}

/// <summary>A QR code's modules, one string of '0'/'1' per row, quiet zone included (4 modules on every side).</summary>
public sealed record QrCodeDto(int Version, int Size, IReadOnlyList<string> Rows);

/// <summary>QR encoding (Infrastructure, QRCoder). Error correction level M, byte mode.</summary>
public interface IQrEncoder
{
    /// <summary>The QR of <paramref name="text"/>, or null when it needs a version above <paramref name="maxVersion"/> (too dense to scan reliably).</summary>
    QrCodeDto? Encode(string text, int maxVersion);
}
