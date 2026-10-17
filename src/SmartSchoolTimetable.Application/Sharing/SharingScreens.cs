using System.Buffers.Text;
using System.Security.Cryptography;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Sharing;

namespace SmartSchoolTimetable.Application.Sharing;

/// <param name="Qr">Null when the package needs a QR denser than <see cref="SharingService.MaxQrVersion"/>: «استخدم ملف .sst لهذا المعلم».</param>
/// <param name="Characters">Length of the QR text (unpadded base64url of the package).</param>
public sealed record PackageQrDto(QrCodeDto? Qr, int Characters);

public sealed record PairingQrDto(string SchoolName, string Fingerprint, PackageQrDto Code);

public sealed record TeacherQrDto(long TeacherId, string Name, int Lessons, int VersionNumber, DateTimeOffset ApprovedAt, PackageQrDto Code);

public sealed record TeacherCardDto(long TeacherId, string Name, int Lessons, PackageQrDto Code);

/// <param name="AppQr">The «حمّل التطبيق» link as a QR, or null when no link is set.</param>
public sealed record TeacherCardsDto(string SchoolName, string AcademicYear, int VersionNumber, DateTimeOffset ApprovedAt, string Fingerprint, PackageQrDto Pairing,
    string? AppLink, QrCodeDto? AppQr, IReadOnlyList<TeacherCardDto> Cards);

/// <param name="TeacherIds">The teachers to print; null or empty prints every teacher with lessons.</param>
public sealed record TeacherCardsCommand(IReadOnlyList<long>? TeacherIds);

public sealed record SharingSettingsDto(string? AppLink, int Version);

public sealed record SaveSharingSettingsCommand(string? AppLink, int Version);

/// <summary>A row of today's clock: a lesson has its number, a break none (minutes after local midnight).</summary>
public sealed record KioskPeriod(int? Number, int StartMinute, int EndMinute);

public sealed record KioskSection(int Index, string StageName, string Label);

public sealed record KioskLesson(int Section, int Lesson, string Subject, int ColorIndex, string Teacher);

/// <summary>
/// «شاشة العرض»: today at the school, from the approved version. <paramref name="Status"/> is <c>school</c>, <c>dayOff</c>,
/// <c>notAWorkingDay</c>, <c>outsideYear</c> or <c>noTimetable</c>; the screen works out the current and next period from its own clock.
/// </summary>
public sealed record KioskDto(string SchoolName, string Date, string TimeZone, string Status, string? DayOffTitle, int? VersionNumber, int Semester, int Session,
    IReadOnlyList<KioskPeriod> Periods, IReadOnlyList<KioskSection> Sections, IReadOnlyList<KioskLesson> Lessons);

public sealed record KioskSessionDto(string Key);

/// <summary>
/// The key of the staff-room screen: random, kept in memory only and valid until the program closes or the owner stops it. It reads
/// today's timetable and nothing else, so the screen keeps running while the owner's own session locks as usual.
/// </summary>
public sealed class KioskAccess
{
    private readonly object _gate = new();
    private byte[]? _key;

    public string Start()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        lock (_gate)
            _key = key;
        return Base64Url.EncodeToString(key);
    }

    public void Stop()
    {
        lock (_gate)
            _key = null;
    }

    public bool IsValid(string? presented)
    {
        byte[]? current;
        lock (_gate)
            current = _key;
        if (current is null || string.IsNullOrEmpty(presented) || presented.Length > 64)
            return false;
        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(presented);
        }
        catch (FormatException)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(bytes, current);
    }
}

public sealed partial class SharingService
{
    /// <summary>The densest QR still offered (level M, 117 modules plus the quiet zone; 997 bytes): beyond it the page says to send the file.</summary>
    public const int MaxQrVersion = 25;

    /// <summary>The «حمّل التطبيق» link fits a much smaller code.</summary>
    public const int MaxLinkQrVersion = 10;

    public async Task<OperationResult<PairingQrDto>> PairingQrAsync(CancellationToken token)
    {
        var file = await PairingAsync(AuditEvents.SharingPairingShown, token);
        if (!file.Succeeded)
            return file.Cast<PairingQrDto>();
        var key = (await store.FirstOrDefaultAsync(store.Read<SchoolKey>(), token))!;
        var profile = await store.FirstOrDefaultAsync(store.Read<Domain.SchoolSetup.SchoolProfile>(), token);
        return OperationResult.Success(new PairingQrDto(profile?.Name ?? string.Empty, FingerprintOf(key.PublicKey), Qr(file.Value!.Content)));
    }

    /// <summary>One teacher's code on the screen (audited like a file export).</summary>
    public async Task<OperationResult<TeacherQrDto>> TeacherQrAsync(long teacherId, CancellationToken token)
    {
        var prepared = await PrepareAsync(token);
        if (!prepared.Succeeded)
            return prepared.Cast<TeacherQrDto>();
        var (source, key, seed) = prepared.Value!;
        if (source.Snapshot.Teachers.FirstOrDefault(teacher => teacher.Id == teacherId) is not { } teacher)
            return OperationResult.Failure<TeacherQrDto>(ErrorCodes.NotFound);
        var lessons = source.Version.Lessons.Count(lesson => lesson.TeacherId == teacherId);
        if (lessons == 0)
            return OperationResult.Failure<TeacherQrDto>(ErrorCodes.ShareTeacherHasNoLessons);
        var code = Qr(Sign(TeacherPackage(source, key, teacher), seed));
        CryptographicOperations.ZeroMemory(seed);
        AuditTrail.Record(store, clock, AuditEvents.SharingTeacherQrShown, $"teacher:{teacher.Id}", $"Phone QR of timetable version {source.Version.Number} shown for a teacher.",
            new { number = source.Version.Number });
        await store.SaveChangesAsync(token);
        return OperationResult.Success(new TeacherQrDto(teacher.Id, teacher.Name, lessons, source.Version.Number, source.Version.ApprovedAt ?? source.Version.CreatedAt, code));
    }

    /// <summary>«بطاقات المعلمين»: everything the A4 cards print (each teacher's code, the pairing code, the «حمّل التطبيق» code). Audited as a print.</summary>
    public async Task<OperationResult<TeacherCardsDto>> CardsAsync(TeacherCardsCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var prepared = await PrepareAsync(token);
        if (!prepared.Succeeded)
            return prepared.Cast<TeacherCardsDto>();
        var (source, key, seed) = prepared.Value!;
        var counts = source.Version.Lessons.GroupBy(lesson => lesson.TeacherId).ToDictionary(group => group.Key, group => group.Count());
        var wanted = command.TeacherIds is { Count: > 0 } ids ? ids.ToHashSet() : null;
        if (wanted is not null && wanted.Any(id => !counts.ContainsKey(id)))
            return OperationResult.Invalid<TeacherCardsDto>(nameof(command.TeacherIds), ErrorCodes.InvalidOption);
        var teachers = source.Snapshot.Teachers.Where(teacher => counts.ContainsKey(teacher.Id) && (wanted is null || wanted.Contains(teacher.Id))).ToArray();
        var cards = teachers.Select(teacher => new TeacherCardDto(teacher.Id, teacher.Name, counts[teacher.Id], Qr(Sign(TeacherPackage(source, key, teacher), seed)))).ToArray();
        var pairing = Qr(Sign(new PhonePackage(Header(PhonePackageKind.Pairing, key, source.Profile?.Name ?? string.Empty), key.PublicKey, null), seed));
        CryptographicOperations.ZeroMemory(seed);
        var settings = await store.FirstOrDefaultAsync(store.Read<SharingSettings>(), token);
        var appLink = settings?.AppLink;
        AuditTrail.Record(store, clock, AuditEvents.SharingCardsPrinted, $"timetable:{source.Version.Id}", $"Teacher cards of timetable version {source.Version.Number} prepared for printing ({cards.Length}).",
            new { number = source.Version.Number, count = cards.Length });
        await store.SaveChangesAsync(token);
        return OperationResult.Success(new TeacherCardsDto(source.Profile?.Name ?? string.Empty, source.Year.Label, source.Version.Number,
            source.Version.ApprovedAt ?? source.Version.CreatedAt, FingerprintOf(key.PublicKey), pairing, appLink,
            appLink is null ? null : qr.Encode(appLink, MaxLinkQrVersion), cards));
    }

    public async Task<SharingSettingsDto> SettingsAsync(CancellationToken token)
    {
        var settings = await store.FirstOrDefaultAsync(store.Read<SharingSettings>(), token);
        return new SharingSettingsDto(settings?.AppLink, settings?.Version ?? 0);
    }

    public async Task<OperationResult<SharingSettingsDto>> SaveSettingsAsync(SaveSharingSettingsCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var settings = await store.FirstOrDefaultAsync(store.Query<SharingSettings>(), token);
        if (settings is null)
        {
            if (command.Version != 0)
                return OperationResult.Failure<SharingSettingsDto>(ErrorCodes.Conflict);
            settings = SharingSettings.CreateDefault();
            store.Add(settings);
        }
        else if (!settings.IsVersion(command.Version))
        {
            return OperationResult.Failure<SharingSettingsDto>(ErrorCodes.Conflict);
        }
        try
        {
            if (settings.Update(command.AppLink))
                AuditTrail.Record(store, clock, AuditEvents.SharingSettingsUpdated, "sharing-settings", "Phone sharing settings updated.");
        }
        catch (Domain.Common.DomainValidationException exception)
        {
            return OperationResult.FromDomain<SharingSettingsDto>(exception);
        }
        await store.SaveChangesAsync(token);
        return OperationResult.Success(new SharingSettingsDto(settings.AppLink, settings.Version));
    }

    /// <summary>«شاشة العرض»: today's clock and every section's lessons of the approved version (shown even when today's data has changed).</summary>
    public async Task<KioskDto> KioskAsync(CancellationToken token)
    {
        var profile = await store.FirstOrDefaultAsync(store.Read<Domain.SchoolSetup.SchoolProfile>(), token);
        var zone = profile?.TimeZoneId ?? Domain.SchoolSetup.SchoolProfile.DefaultTimeZone;
        var today = Today(profile);
        var date = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (await ApprovedAsync(token) is not { } source)
            return new KioskDto(profile?.Name ?? string.Empty, date, zone, "noTimetable", null, null, 1, 1, [], [], []);
        var content = PrincipalPackage(source, NoKey).Timetable!;
        var day = PackageCalendar.Resolve(content, today);
        var status = day.Status switch
        {
            SchoolDayStatus.School => "school",
            SchoolDayStatus.DayOff => "dayOff",
            SchoolDayStatus.NotAWorkingDay => "notAWorkingDay",
            _ => "outsideYear",
        };
        var number = 0;
        var periods = day.Periods.Select(period => new KioskPeriod(period.IsBreak ? null : ++number, period.StartMinute, period.EndMinute)).ToArray();
        var sections = content.Sections.Select((section, index) => new KioskSection(index, section.StageName, section.Label)).ToArray();
        var lessons = day.Status == SchoolDayStatus.School
            ? content.Lessons.Where(lesson => lesson.Day == day.Day).Select(lesson => new KioskLesson(lesson.Section, lesson.Lesson, content.Subjects[lesson.Subject].Name,
                content.Subjects[lesson.Subject].ColorIndex, content.Teachers[lesson.Teacher].ShortName)).ToArray()
            : [];
        return new KioskDto(profile?.Name ?? string.Empty, date, zone, status, day.DayOff?.Title, source.Version.Number, day.Semester, day.Session, periods, sections, lessons);
    }

    /// <summary>The kiosk builds the principal content without signing anything: the header's key fields are not used.</summary>
    private static readonly SchoolKey NoKey = SchoolKey.Create(Guid.NewGuid(), new byte[SchoolKey.PublicKeyLength], [0], DateTimeOffset.UnixEpoch);

    private PackageQrDto Qr(byte[] package)
    {
        var text = Base64Url.EncodeToString(package);
        return new PackageQrDto(qr.Encode(text, MaxQrVersion), text.Length);
    }
}
