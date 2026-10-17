using System.IO.Compression;
using System.Security.Cryptography;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Calendar;
using SmartSchoolTimetable.Domain.Generation;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Sharing;

namespace SmartSchoolTimetable.Application.Sharing;

/// <param name="Fingerprint">The key id as four groups of hex digits, shown on the desktop and on the phone when pairing.</param>
/// <param name="BackedUpAt">When this key was last saved to a password-protected file (null: the page asks the owner to save one).</param>
public sealed record SchoolKeyDto(Guid SchoolId, string Fingerprint, int Generation, DateTimeOffset CreatedAt, bool Readable, DateTimeOffset? BackedUpAt = null);

/// <summary>«حفظ نسخة من مفتاح المدرسة»: the password the owner chooses, twice.</summary>
public sealed record BackupSchoolKeyCommand(string? Password, string? ConfirmPassword);

/// <param name="File">The .sstkey file, base64 (it is about 200 bytes).</param>
/// <param name="ConfirmReplace">Needed when this computer already has a different key that still works, or a key of another school id.</param>
public sealed record ImportSchoolKeyCommand(string? File, string? Password, bool ConfirmReplace);

/// <param name="Changed">False when the file holds the key this computer already uses (nothing to do).</param>
public sealed record ImportSchoolKeyResult(SchoolKeyDto Key, bool Changed);

/// <param name="Conflicts">Today's school data conflicts with the approved version: nothing can be exported until it is fixed.</param>
public sealed record ApprovedTimetableDto(long Id, int Number, DateTimeOffset ApprovedAt, bool Conflicts, DateOnly ExpiresOn, bool YearEnded);

public sealed record ShareTeacherDto(long Id, string Name, int Lessons);

/// <summary>«مشاركة الجدول مع الهاتف»: the key, the approved version and its teachers. <paramref name="Key"/> is null until the first export.</summary>
public sealed record SharingStatusDto(SchoolKeyDto? Key, ApprovedTimetableDto? Approved, IReadOnlyList<ShareTeacherDto> Teachers);

/// <param name="Confirm">First confirmation: «أريد إنشاء مفتاح جديد».</param>
/// <param name="ConfirmRepair">Second confirmation: «أفهم أن كل هاتف يجب ربطه من جديد».</param>
public sealed record RegenerateSchoolKeyCommand(bool Confirm, bool ConfirmRepair);

/// <summary>
/// Phone packages (Phase 9, ADR 0056): the school's signing key and the signed .sst packages built from the APPROVED
/// timetable of the current year. The desktop never talks to a phone; packages leave as files or QR codes.
/// </summary>
public sealed partial class SharingService(IDataStore store, ISchoolKeyCrypto crypto, IPhonePackageWriter writer, ISchoolKeyBackupFile keyFiles, IQrEncoder qr, TimeProvider clock)
{
    public const string PackageContentType = "application/octet-stream";
    public const string ZipContentType = "application/zip";
    public const string PackageExtension = ".sst";

    /// <summary>The current payload format; the reader refuses anything higher (docs/SST_PACKAGE_FORMAT.md).</summary>
    public const int FormatVersion = 1;

    public const string KeyFileExtension = ".sstkey";

    /// <summary>Characters refused by Windows, Android (FAT/exFAT storage) or iOS Files, and invisible direction marks; they become «-».</summary>
    private static readonly char[] UnsafeFileNameChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*',
        '\u200E', '\u200F', '\u202A', '\u202B', '\u202C', '\u202D', '\u202E', '\u2066', '\u2067', '\u2068', '\u2069'];

    /// <summary>Device names Windows refuses as a file name, with or without an extension.</summary>
    private static readonly string[] ReservedFileNames = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];

    /// <summary>The name part is cut to this many characters, far below every platform's 255-byte limit (an Arabic letter is 2 bytes).</summary>
    public const int MaxFileNameLength = 80;

    public async Task<OperationResult<SharingStatusDto>> StatusAsync(CancellationToken token)
    {
        var key = await store.FirstOrDefaultAsync(store.Read<SchoolKey>(), token);
        var keyDto = key is null ? null : KeyDto(key, crypto.Unprotect(key.ProtectedPrivateKey) is not null);
        var source = await ApprovedAsync(token);
        if (source is null)
            return OperationResult.Success(new SharingStatusDto(keyDto, null, []));
        var lessons = source.Version.Lessons.GroupBy(lesson => lesson.TeacherId).ToDictionary(group => group.Key, group => group.Count());
        var teachers = source.Snapshot.Teachers.Where(teacher => lessons.ContainsKey(teacher.Id))
            .Select(teacher => new ShareTeacherDto(teacher.Id, teacher.Name, lessons[teacher.Id])).ToArray();
        return OperationResult.Success(new SharingStatusDto(keyDto, source.Dto(Today(source.Profile)), teachers));
    }

    /// <summary>«رمز ربط المدرسة»: the school id, name and public key, signed with the school key (the key is made on first use).</summary>
    public Task<OperationResult<TimetableFile>> PairingAsync(CancellationToken token) => PairingAsync(AuditEvents.SharingPairingExported, token);

    private async Task<OperationResult<TimetableFile>> PairingAsync(string auditEvent, CancellationToken token)
    {
        var profile = await store.FirstOrDefaultAsync(store.Read<SchoolProfile>(), token);
        var signer = await SignerAsync(token);
        if (!signer.Succeeded)
            return signer.Cast<TimetableFile>();
        var (key, seed) = signer.Value!;
        var package = new PhonePackage(Header(PhonePackageKind.Pairing, key, profile?.Name ?? string.Empty), key.PublicKey, null);
        var bytes = Sign(package, seed);
        AuditTrail.Record(store, clock, auditEvent, "school-key", "School pairing code exported or shown.", new { generation = key.Generation });
        await store.SaveChangesAsync(token);
        return OperationResult.Success(new TimetableFile("school-pairing" + PackageExtension, PackageContentType, bytes));
    }

    public async Task<OperationResult<TimetableFile>> TeacherAsync(long teacherId, CancellationToken token)
    {
        var prepared = await PrepareAsync(token);
        if (!prepared.Succeeded)
            return prepared.Cast<TimetableFile>();
        var (source, key, seed) = prepared.Value!;
        if (source.Snapshot.Teachers.FirstOrDefault(teacher => teacher.Id == teacherId) is not { } teacher)
            return OperationResult.Failure<TimetableFile>(ErrorCodes.NotFound);
        if (source.Version.Lessons.All(lesson => lesson.TeacherId != teacherId))
            return OperationResult.Failure<TimetableFile>(ErrorCodes.ShareTeacherHasNoLessons);
        var bytes = Sign(TeacherPackage(source, key, teacher), seed);
        AuditTrail.Record(store, clock, AuditEvents.SharingTeacherExported, $"teacher:{teacher.Id}", $"Phone package of timetable version {source.Version.Number} exported for a teacher.",
            new { number = source.Version.Number });
        await store.SaveChangesAsync(token);
        return OperationResult.Success(new TimetableFile(SafeFileName(teacher.Name) + $"-v{source.Version.Number}" + PackageExtension, PackageContentType, bytes));
    }

    /// <summary>One .sst per teacher with lessons, in a zip (entry names carry the teacher's name, so the owner knows whose file is whose).</summary>
    public async Task<OperationResult<TimetableFile>> AllTeachersAsync(CancellationToken token)
    {
        var prepared = await PrepareAsync(token);
        if (!prepared.Succeeded)
            return prepared.Cast<TimetableFile>();
        var (source, key, seed) = prepared.Value!;
        var used = source.Version.Lessons.Select(lesson => lesson.TeacherId).ToHashSet();
        var teachers = source.Snapshot.Teachers.Where(teacher => used.Contains(teacher.Id)).ToArray();
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var teacher in teachers)
            {
                var name = SafeFileName(teacher.Name) + $"-v{source.Version.Number}" + PackageExtension;
                if (!names.Add(name))
                {
                    name = SafeFileName(teacher.Name) + $"-{teacher.Id}-v{source.Version.Number}" + PackageExtension;
                    names.Add(name);
                }
                var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                entry.LastWriteTime = source.GeneratedAt;
                await using var stream = entry.Open();
                await stream.WriteAsync(Sign(TeacherPackage(source, key, teacher), seed), token);
            }
        }
        AuditTrail.Record(store, clock, AuditEvents.SharingAllTeachersExported, $"timetable:{source.Version.Id}", $"Phone packages of timetable version {source.Version.Number} exported for {teachers.Length} teachers.",
            new { number = source.Version.Number, count = teachers.Length });
        await store.SaveChangesAsync(token);
        return OperationResult.Success(new TimetableFile($"teachers-v{source.Version.Number}.zip", ZipContentType, buffer.ToArray()));
    }

    /// <summary>The principal's package: every section, teacher and lesson. It holds every teacher's name, so the screen warns to share it privately.</summary>
    public async Task<OperationResult<TimetableFile>> PrincipalAsync(CancellationToken token)
    {
        var prepared = await PrepareAsync(token);
        if (!prepared.Succeeded)
            return prepared.Cast<TimetableFile>();
        var (source, key, seed) = prepared.Value!;
        var bytes = Sign(PrincipalPackage(source, key), seed);
        AuditTrail.Record(store, clock, AuditEvents.SharingPrincipalExported, $"timetable:{source.Version.Id}", $"Principal phone package of timetable version {source.Version.Number} exported.",
            new { number = source.Version.Number });
        await store.SaveChangesAsync(token);
        return OperationResult.Success(new TimetableFile($"principal-v{source.Version.Number}" + PackageExtension, PackageContentType, bytes));
    }

    /// <summary>«إعادة إنشاء مفتاح المدرسة»: a new key pair for the same school id. Every phone must scan the new pairing code.</summary>
    public async Task<OperationResult<SchoolKeyDto>> RegenerateKeyAsync(RegenerateSchoolKeyCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Confirm || !command.ConfirmRepair)
            return OperationResult.Failure<SchoolKeyDto>(ErrorCodes.ShareKeyConfirmationRequired);
        var (publicKey, seed) = crypto.Generate();
        var key = await store.FirstOrDefaultAsync(store.Query<SchoolKey>(), token);
        if (key is null)
        {
            key = SchoolKey.Create(Guid.NewGuid(), publicKey, crypto.Protect(seed), clock.GetUtcNow());
            store.Add(key);
            AuditTrail.Record(store, clock, AuditEvents.SharingKeyCreated, "school-key", "School signing key created.", new { generation = key.Generation });
        }
        else
        {
            key.Replace(publicKey, crypto.Protect(seed), clock.GetUtcNow());
            AuditTrail.Record(store, clock, AuditEvents.SharingKeyRegenerated, "school-key", "School signing key recreated.", new { generation = key.Generation });
        }
        CryptographicOperations.ZeroMemory(seed);
        await store.SaveChangesAsync(token);
        return OperationResult.Success(KeyDto(key, true));
    }

    /// <summary>The first 8 bytes of SHA-256 of a public key.</summary>
    public static byte[] KeyIdOf(byte[] publicKey) => SHA256.HashData(publicKey)[..8];

    public static string FingerprintOf(byte[] publicKey) =>
        string.Join('-', Convert.ToHexString(KeyIdOf(publicKey)).Chunk(4).Select(chunk => new string(chunk)));

    /// <summary>A file name made from a teacher's name: characters Windows refuses become «-».</summary>
    public static string SafeFileName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var cleaned = new string(name.Normalize(System.Text.NormalizationForm.FormC)
            .Select(character => UnsafeFileNameChars.Contains(character) || char.IsControl(character) ? '-' : character).ToArray());
        cleaned = string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (cleaned.Length > MaxFileNameLength)
            cleaned = cleaned[..MaxFileNameLength];
        // Half a surrogate pair, trailing dots or spaces (Windows drops them) and a leading dot (hidden on Android and iOS) are removed.
        if (cleaned.Length > 0 && char.IsHighSurrogate(cleaned[^1]))
            cleaned = cleaned[..^1];
        cleaned = cleaned.Trim().Trim('.').Trim();
        if (ReservedFileNames.Contains(cleaned.Split('.')[0], StringComparer.OrdinalIgnoreCase))
            cleaned = "_" + cleaned;
        return cleaned.Length == 0 ? "teacher" : cleaned;
    }

    // ---- package content -------------------------------------------------------------------------------------------

    /// <summary>The approved version of the current year with everything a package needs (null when there is none).</summary>
    private sealed record Source(AcademicYear Year, TimetableVersion Version, SchedulingInput Snapshot, SchedulingInput? Today, SchoolProfile? Profile,
        SessionPlan? Plan, int WeekStart, IReadOnlyList<CalendarDay> Holidays, DateTimeOffset GeneratedAt)
    {
        public bool Conflicts => Today is not null
            && CurrentDataAnalyzer.Analyze(Today, TimetableService.PlacedOf(Version), Version.Mode == GenerationModes.DoublePeriods).HasConflicts;

        public ApprovedTimetableDto Dto(DateOnly today) =>
            new(Version.Id, Version.Number, Version.ApprovedAt ?? Version.CreatedAt, Conflicts, Year.EndDate, today > Year.EndDate);
    }

    private async Task<Source?> ApprovedAsync(CancellationToken token)
    {
        if (await store.FirstOrDefaultAsync(store.Read<AcademicYear>().Where(year => year.IsCurrent), token) is not { } year)
            return null;
        if (await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(version => version.AcademicYearId == year.Id && version.IsApproved), token) is not { } version)
            return null;
        var profile = await store.FirstOrDefaultAsync(store.Read<SchoolProfile>(), token);
        var plan = await store.FirstOrDefaultAsync(store.Read<SessionPlan>().Where(item => item.AcademicYearId == year.Id), token);
        var week = await store.FirstOrDefaultAsync(store.Read<WorkingWeek>(), token);
        var days = await store.ListAsync(store.Read<CalendarDay>()
            .Where(day => day.IsEnabled && day.AffectsSchedule
                && day.StartDate <= year.EndDate && day.EndDate >= year.StartDate), token);
        var now = clock.GetUtcNow();
        return new Source(year, version, TimetableService.SnapshotOf(version), await SchedulingInputBuilder.BuildAsync(store, year.Id, token), profile, plan,
            week?.WeekStartDay ?? Weekday.Sunday, days.OrderBy(day => day.StartDate).ThenBy(day => day.EndDate).ThenBy(day => day.Id).ToArray(),
            new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero));
    }

    /// <summary>Every export: an approved version that still fits today's data, in a year that has not ended, and a readable key.</summary>
    private async Task<OperationResult<(Source Source, SchoolKey Key, byte[] Seed)>> PrepareAsync(CancellationToken token)
    {
        if (await ApprovedAsync(token) is not { } source)
            return OperationResult.Failure<(Source, SchoolKey, byte[])>(ErrorCodes.ShareNoApprovedTimetable);
        if (source.Conflicts)
            return OperationResult.Failure<(Source, SchoolKey, byte[])>(ErrorCodes.ShareTimetableConflicts);
        if (Today(source.Profile) > source.Year.EndDate)
            return OperationResult.Failure<(Source, SchoolKey, byte[])>(ErrorCodes.ShareYearEnded);
        var signer = await SignerAsync(token);
        if (!signer.Succeeded)
            return signer.Cast<(Source, SchoolKey, byte[])>();
        return OperationResult.Success((source, signer.Value.Key, signer.Value.Seed));
    }

    /// <summary>The school key and its private seed; the key is made on first use (saved with the caller's unit of work).</summary>
    private async Task<OperationResult<(SchoolKey Key, byte[] Seed)>> SignerAsync(CancellationToken token)
    {
        var key = await store.FirstOrDefaultAsync(store.Query<SchoolKey>(), token);
        if (key is null)
        {
            var (publicKey, fresh) = crypto.Generate();
            key = SchoolKey.Create(Guid.NewGuid(), publicKey, crypto.Protect(fresh), clock.GetUtcNow());
            store.Add(key);
            AuditTrail.Record(store, clock, AuditEvents.SharingKeyCreated, "school-key", "School signing key created.", new { generation = key.Generation });
            return OperationResult.Success((key, fresh));
        }
        return crypto.Unprotect(key.ProtectedPrivateKey) is { } seed
            ? OperationResult.Success((key, seed))
            : OperationResult.Failure<(SchoolKey, byte[])>(ErrorCodes.ShareKeyUnreadable);
    }

    private byte[] Sign(PhonePackage package, byte[] seed) => writer.Write(package, seed);

    private PackageHeader Header(PhonePackageKind kind, SchoolKey key, string schoolName)
    {
        var now = clock.GetUtcNow();
        return new PackageHeader(FormatVersion, kind, key.SchoolId, schoolName, KeyIdOf(key.PublicKey),
            new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero));
    }

    private PhonePackage TeacherPackage(Source source, SchoolKey key, TeacherInput teacher) =>
        new(Header(PhonePackageKind.Teacher, key, source.Profile?.Name ?? string.Empty), key.PublicKey,
            Content(source, source.Version.Lessons.Where(lesson => lesson.TeacherId == teacher.Id).ToArray(), [teacher], allSections: false));

    private PhonePackage PrincipalPackage(Source source, SchoolKey key)
    {
        var used = source.Version.Lessons.Select(lesson => lesson.TeacherId).ToHashSet();
        // Every active teacher, even one without lessons (always free in «من المتفرغ الآن؟»).
        var teachers = source.Snapshot.Teachers.Where(teacher => used.Contains(teacher.Id) || !teacher.IsArchived).ToArray();
        return new(Header(PhonePackageKind.Principal, key, source.Profile?.Name ?? string.Empty), key.PublicKey,
            Content(source, source.Version.Lessons.ToArray(), teachers, allSections: true));
    }

    private static TimetableContent Content(Source source, IReadOnlyList<TimetableLesson> lessons, IReadOnlyList<TeacherInput> teachers, bool allSections)
    {
        var snapshot = source.Snapshot;
        var lines = snapshot.Lines.ToDictionary(line => line.Id, line => line.SubjectId);
        var subjectOf = (TimetableLesson lesson) => lines.TryGetValue(lesson.CurriculumEntryId, out var subject) ? subject : 0;
        var usedSubjects = lessons.Select(subjectOf).ToHashSet();
        var usedSections = lessons.Select(lesson => lesson.SectionId).ToHashSet();
        var subjects = snapshot.Subjects.Where(subject => usedSubjects.Contains(subject.Id)).ToArray();
        var sections = snapshot.Sections.Where(section => allSections || usedSections.Contains(section.Id)).ToArray();
        var subjectIndex = subjects.Select((subject, index) => (subject.Id, index)).ToDictionary(item => item.Id, item => item.index);
        var sectionIndex = sections.Select((section, index) => (section.Id, index)).ToDictionary(item => item.Id, item => item.index);
        var teacherIndex = teachers.Select((teacher, index) => (teacher.Id, index)).ToDictionary(item => item.Id, item => item.index);
        var packageLessons = lessons
            .Where(lesson => subjectIndex.ContainsKey(subjectOf(lesson)) && sectionIndex.ContainsKey(lesson.SectionId) && teacherIndex.ContainsKey(lesson.TeacherId))
            .Select(lesson => new PackageLesson(lesson.Day, lesson.LessonNumber, subjectIndex[subjectOf(lesson)], sectionIndex[lesson.SectionId], teacherIndex[lesson.TeacherId]))
            .OrderBy(lesson => lesson.Day).ThenBy(lesson => lesson.Lesson).ThenBy(lesson => lesson.Section).ThenBy(lesson => lesson.Teacher)
            .ToArray();

        var semesters = source.Year.Terms.OrderBy(term => term.StartDate).Take(SessionPlan.MaxTerms)
            .Select(term => new PackageSemester(term.StartDate, term.EndDate)).ToArray();
        var holidays = source.Holidays.Select(day => new PackageHoliday(day.StartDate, day.EndDate, day.Title, day.IsApproximate, day.ByDecision, KindOf(day.Kind),
            day.Source == CalendarDaySource.IraqTemplate ? PackageHolidayCodes.CodeOf(day.TemplateKey, day.Title) : 0)).ToArray();
        var (sessions, sessionDays) = Timings(source);
        return new TimetableContent(
            source.Year.Label,
            source.Version.Number,
            source.Version.ApprovedAt ?? source.Version.CreatedAt,
            source.Year.EndDate,
            source.Profile?.TimeZoneId ?? SchoolProfile.DefaultTimeZone,
            source.Year.StartDate,
            source.Year.EndDate,
            semesters,
            holidays,
            snapshot.WorkingDays.ToArray(),
            source.WeekStart,
            sessions,
            sessionDays,
            subjects.Select(subject => new PackageSubject(subject.Name, subject.ColorIndex)).ToArray(),
            sections.Select(section => new PackageSection(section.StageName, section.Label)).ToArray(),
            teachers.Select(teacher => new PackageTeacher(teacher.Name, string.IsNullOrWhiteSpace(teacher.ShortName) ? teacher.Name : teacher.ShortName)).ToArray(),
            packageLessons);
    }

    /// <summary>
    /// The school's one structural shift (ADR 0046) gives session 1 from the version's own snapshot; a double shift adds the other
    /// sessions' timing and the day → session mapping of each semester from today's settings, exactly as the desktop viewer shows them.
    /// </summary>
    private static (IReadOnlyList<PackageSession> Sessions, IReadOnlyList<PackageSessionDay> Days) Timings(Source source)
    {
        var snapshot = source.Snapshot;
        var shift = (source.Plan is { } plan ? snapshot.Shifts.FirstOrDefault(item => item.Id == plan.ShiftId) : null) ?? snapshot.Shifts.OrderBy(item => item.Id).FirstOrDefault();
        var first = new PackageSession((int)SessionKind.Morning, (shift?.Periods ?? []).OrderBy(period => period.Position)
            .Select(period => new PackagePeriod(period.Kind != nameof(PeriodKind.Lesson), period.StartMinute, period.EndMinute)).ToArray());
        if (source.Plan is not { System: not SessionSystem.OneSession } sessionPlan || shift is null || sessionPlan.ShiftId != shift.Id)
            return ([first], []);
        static int Minutes(TimeOnly time) => time.Hour * 60 + time.Minute;
        var others = sessionPlan.Periods.GroupBy(period => period.Session).OrderBy(group => group.Key)
            .Select(group => new PackageSession((int)group.Key, group.OrderBy(period => period.Position)
                .Select(period => new PackagePeriod(period.Kind != PeriodKind.Lesson, Minutes(period.StartTime), Minutes(period.EndTime))).ToArray()));
        var days = sessionPlan.Days.Where(day => snapshot.WorkingDays.Contains(day.Day))
            .Select(day => new PackageSessionDay(day.Term, day.Day, (int)day.Session)).ToArray();
        return ([first, .. others], days);
    }

    private DateOnly Today(SchoolProfile? profile)
    {
        var zone = profile?.TimeZoneId ?? SchoolProfile.DefaultTimeZone;
        try
        {
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        }
    }

    private static SchoolKeyDto KeyDto(SchoolKey key, bool readable) => new(key.SchoolId, FingerprintOf(key.PublicKey), key.Generation, key.CreatedAt, readable, key.BackedUpAt);

    private static PackageDayKind KindOf(CalendarDayKind kind) => kind switch
    {
        CalendarDayKind.SchoolHoliday => PackageDayKind.SchoolHoliday,
        CalendarDayKind.Exam => PackageDayKind.Exam,
        CalendarDayKind.SpecialDay => PackageDayKind.SpecialDay,
        _ => PackageDayKind.OfficialHoliday,
    };

    // ---- the school key's password-protected copy (DECISIONS_LOG #106) ------------------------------------------------

    /// <summary>«حفظ نسخة من مفتاح المدرسة»: the key in a .sstkey file only this password opens (the key is made first when there is none).</summary>
    public async Task<OperationResult<TimetableFile>> BackupKeyAsync(BackupSchoolKeyCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = new List<FieldError>();
        if (string.IsNullOrEmpty(command.Password))
            errors.Add(new FieldError(nameof(command.Password), ErrorCodes.Required));
        else if (command.Password.Length < CredentialRules.PasswordMinLength)
            errors.Add(new FieldError(nameof(command.Password), ErrorCodes.PasswordTooShort));
        else if (command.Password.Length > CredentialRules.PasswordMaxLength)
            errors.Add(new FieldError(nameof(command.Password), ErrorCodes.PasswordTooLong));
        else if (!string.Equals(command.Password, command.ConfirmPassword, StringComparison.Ordinal))
            errors.Add(new FieldError(nameof(command.ConfirmPassword), ErrorCodes.PasswordMismatch));
        if (errors.Count > 0)
            return OperationResult.Invalid<TimetableFile>(errors);
        var signer = await SignerAsync(token);
        if (!signer.Succeeded)
            return signer.Cast<TimetableFile>();
        var (key, seed) = signer.Value!;
        var file = keyFiles.Seal(new SchoolKeyBackup(key.SchoolId, key.Generation, key.CreatedAt, key.PublicKey, seed), command.Password!);
        CryptographicOperations.ZeroMemory(seed);
        key.MarkBackedUp(clock.GetUtcNow());
        AuditTrail.Record(store, clock, AuditEvents.SharingKeyBackedUp, "school-key", "School key saved to a password-protected file.", new { generation = key.Generation });
        await store.SaveChangesAsync(token);
        return OperationResult.Success(new TimetableFile($"school-key-{FingerprintOf(key.PublicKey)}{KeyFileExtension}", PackageContentType, file));
    }

    /// <summary>
    /// «استيراد مفتاح المدرسة»: puts a saved key back (a new computer, or a restored backup DPAPI cannot read). The school id is the file's, so
    /// paired phones keep working. Replacing a different key that still works here, or a key of another school id, needs <c>ConfirmReplace</c>.
    /// </summary>
    public async Task<OperationResult<ImportSchoolKeyResult>> ImportKeyAsync(ImportSchoolKeyCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrEmpty(command.File))
            return OperationResult.Invalid<ImportSchoolKeyResult>(nameof(command.File), ErrorCodes.Required);
        if (string.IsNullOrEmpty(command.Password))
            return OperationResult.Invalid<ImportSchoolKeyResult>(nameof(command.Password), ErrorCodes.Required);
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(command.File);
        }
        catch (FormatException)
        {
            return OperationResult.Failure<ImportSchoolKeyResult>(ErrorCodes.ShareKeyFileInvalid);
        }
        var opened = keyFiles.Open(bytes, command.Password);
        if (opened.Status != KeyBackupOpenStatus.Opened)
        {
            return OperationResult.Failure<ImportSchoolKeyResult>(opened.Status switch
            {
                KeyBackupOpenStatus.WrongPassword => ErrorCodes.ShareKeyFileWrongPassword,
                KeyBackupOpenStatus.Tampered => ErrorCodes.ShareKeyFileTampered,
                _ => ErrorCodes.ShareKeyFileInvalid,
            });
        }
        var backup = opened.Backup!;
        try
        {
            var existing = await store.FirstOrDefaultAsync(store.Query<SchoolKey>(), token);
            if (existing is not null)
            {
                var existingSeed = crypto.Unprotect(existing.ProtectedPrivateKey);
                var readable = existingSeed is not null;
                if (existingSeed is not null)
                    CryptographicOperations.ZeroMemory(existingSeed);
                var samePublicKey = existing.PublicKey.AsSpan().SequenceEqual(backup.PublicKey);
                if (readable && samePublicKey && existing.SchoolId == backup.SchoolId)
                    return OperationResult.Success(new ImportSchoolKeyResult(KeyDto(existing, true), false));
                if ((existing.SchoolId != backup.SchoolId || (readable && !samePublicKey)) && !command.ConfirmReplace)
                    return OperationResult.Failure<ImportSchoolKeyResult>(ErrorCodes.ShareKeyImportConfirmationRequired);
            }
            var imported = SchoolKey.Restore(backup.SchoolId, backup.Generation, backup.CreatedAt, backup.PublicKey, crypto.Protect(backup.PrivateSeed), clock.GetUtcNow());
            if (existing is null)
                store.Add(imported);
            else
                existing.ReplaceWith(imported);
            var key = existing ?? imported;
            AuditTrail.Record(store, clock, AuditEvents.SharingKeyImported, "school-key", "School key imported from its password-protected file.",
                new { generation = key.Generation, replaced = existing is not null });
            await store.SaveChangesAsync(token);
            return OperationResult.Success(new ImportSchoolKeyResult(KeyDto(key, true), true));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(backup.PrivateSeed);
        }
    }
}
