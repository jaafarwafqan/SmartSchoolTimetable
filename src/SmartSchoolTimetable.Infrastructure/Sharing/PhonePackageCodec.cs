using System.Formats.Cbor;
using System.IO.Compression;
using SmartSchoolTimetable.Application.Sharing;

namespace SmartSchoolTimetable.Infrastructure.Sharing;

/// <summary>
/// The .sst wire format (docs/SST_PACKAGE_FORMAT.md, ADR 0056):
/// <c>"SST" | envelope version (1) | Ed25519 signature (64) | raw DEFLATE of the CBOR payload</c>.
/// The signature covers the first four bytes and the compressed body. The payload is a canonical CBOR map with small
/// integer keys (<see cref="Keys"/>). A QR code carries the same bytes as unpadded base64url text.
/// </summary>
public static class PhonePackageFormat
{
    public static ReadOnlySpan<byte> Magic => "SST"u8;
    public const byte EnvelopeVersion = 1;
    public const int SignatureOffset = 4;
    public const int BodyOffset = SignatureOffset + Ed25519.SignatureLength;

    /// <summary>Limits a reader applies before trusting anything (a hostile file cannot exhaust memory).</summary>
    public const int MaxPackageBytes = 256 * 1024;
    public const int MaxPayloadBytes = 2 * 1024 * 1024;

    /// <summary>The integer keys of the payload map. Readers ignore keys they do not know; a new key never needs a new format version.</summary>
    public static class Keys
    {
        public const int FormatVersion = 0;
        public const int Kind = 1;
        public const int SchoolId = 2;
        public const int SchoolName = 3;
        public const int KeyId = 4;
        public const int GeneratedAt = 5;
        public const int PublicKey = 6;
        public const int AcademicYear = 10;
        public const int TimetableVersion = 11;
        public const int ApprovedAt = 12;
        public const int ExpiresOn = 13;
        public const int TimeZone = 14;
        public const int YearStart = 15;
        public const int YearEnd = 16;
        public const int Semesters = 17;
        public const int Holidays = 18;
        public const int WorkingDays = 19;
        public const int WeekStart = 20;
        public const int Sessions = 21;
        public const int SessionDays = 22;
        public const int Subjects = 23;
        public const int Sections = 24;
        public const int Teachers = 25;
        public const int Lessons = 26;
    }

    public const int HolidayApproximate = 1;
    public const int HolidayByDecision = 2;

    public static int Days(DateOnly date) => date.DayNumber - DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber;

    public static DateOnly Date(long days) => DateOnly.FromDayNumber(checked((int)(DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber + days)));

    /// <summary>The bytes the signature covers: the first four envelope bytes, then the compressed body.</summary>
    public static byte[] SignedMessage(ReadOnlySpan<byte> envelopeHead, ReadOnlySpan<byte> body) => [.. envelopeHead[..SignatureOffset], .. body];

    public static string ToQrText(byte[] package) => Convert.ToBase64String(package).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromQrText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var base64 = text.Trim().Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }
}

/// <summary>Writes packages (the desktop side).</summary>
public sealed class PhonePackageWriter : IPhonePackageWriter
{
    public byte[] Write(PhonePackage package, byte[] privateSeed)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(privateSeed);
        return Seal(Payload(package), privateSeed);
    }

    /// <summary>Compresses and signs a CBOR payload (public so the test vectors can seal a hand-made payload).</summary>
    public static byte[] Seal(byte[] payload, byte[] privateSeed)
    {
        ArgumentNullException.ThrowIfNull(payload);
        using var compressed = new MemoryStream();
        using (var deflate = new DeflateStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(payload);
        var body = compressed.ToArray();
        byte[] head = [.. PhonePackageFormat.Magic, PhonePackageFormat.EnvelopeVersion];
        var signature = Ed25519.Sign(privateSeed, PhonePackageFormat.SignedMessage(head, body));
        return [.. head, .. signature, .. body];
    }

    public static byte[] Payload(PhonePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var header = package.Header;
        var content = package.Timetable;
        var writer = new CborWriter(CborConformanceMode.Canonical);
        var count = 6 + (package.PublicKey is null ? 0 : 1) + (content is null ? 0 : 17);
        writer.WriteStartMap(count);
        Key(writer, PhonePackageFormat.Keys.FormatVersion).WriteInt32(header.FormatVersion);
        Key(writer, PhonePackageFormat.Keys.Kind).WriteInt32((int)header.Kind);
        Key(writer, PhonePackageFormat.Keys.SchoolId).WriteByteString(header.SchoolId.ToByteArray(bigEndian: true));
        Key(writer, PhonePackageFormat.Keys.SchoolName).WriteTextString(header.SchoolName);
        Key(writer, PhonePackageFormat.Keys.KeyId).WriteByteString(header.KeyId);
        Key(writer, PhonePackageFormat.Keys.GeneratedAt).WriteInt64(header.GeneratedAt.ToUnixTimeSeconds());
        if (package.PublicKey is { } publicKey)
            Key(writer, PhonePackageFormat.Keys.PublicKey).WriteByteString(publicKey);
        if (content is not null)
        {
            Key(writer, PhonePackageFormat.Keys.AcademicYear).WriteTextString(content.AcademicYear);
            Key(writer, PhonePackageFormat.Keys.TimetableVersion).WriteInt32(content.TimetableVersion);
            Key(writer, PhonePackageFormat.Keys.ApprovedAt).WriteInt64(content.ApprovedAt.ToUnixTimeSeconds());
            Key(writer, PhonePackageFormat.Keys.ExpiresOn).WriteInt32(PhonePackageFormat.Days(content.ExpiresOn));
            Key(writer, PhonePackageFormat.Keys.TimeZone).WriteTextString(content.TimeZone);
            Key(writer, PhonePackageFormat.Keys.YearStart).WriteInt32(PhonePackageFormat.Days(content.YearStart));
            Key(writer, PhonePackageFormat.Keys.YearEnd).WriteInt32(PhonePackageFormat.Days(content.YearEnd));
            Rows(Key(writer, PhonePackageFormat.Keys.Semesters), content.Semesters, (w, item) =>
                Ints(w, PhonePackageFormat.Days(item.Start), PhonePackageFormat.Days(item.End)));
            Rows(Key(writer, PhonePackageFormat.Keys.Holidays), content.Holidays, (w, item) =>
            {
                // A template holiday with its own title travels as its code, the title left empty (§4.1).
                w.WriteStartArray(item.TitleCode > 0 ? 6 : 5);
                w.WriteInt32(PhonePackageFormat.Days(item.Start));
                w.WriteInt32(PhonePackageFormat.Days(item.End));
                w.WriteTextString(item.TitleCode > 0 ? string.Empty : item.Title);
                w.WriteInt32((item.Approximate ? PhonePackageFormat.HolidayApproximate : 0) | (item.ByDecision ? PhonePackageFormat.HolidayByDecision : 0));
                w.WriteInt32((int)item.Kind);
                if (item.TitleCode > 0)
                    w.WriteInt32(item.TitleCode);
                w.WriteEndArray();
            });
            Ints(Key(writer, PhonePackageFormat.Keys.WorkingDays), [.. content.WorkingDays]);
            Key(writer, PhonePackageFormat.Keys.WeekStart).WriteInt32(content.WeekStart);
            Rows(Key(writer, PhonePackageFormat.Keys.Sessions), content.Sessions, (w, session) =>
            {
                w.WriteStartArray(2);
                w.WriteInt32(session.Session);
                Rows(w, session.Periods, (row, period) => Ints(row, period.IsBreak ? 1 : 0, period.StartMinute, period.EndMinute));
                w.WriteEndArray();
            });
            Rows(Key(writer, PhonePackageFormat.Keys.SessionDays), content.SessionDays, (w, day) => Ints(w, day.Semester, day.Day, day.Session));
            Rows(Key(writer, PhonePackageFormat.Keys.Subjects), content.Subjects, (w, subject) =>
            {
                w.WriteStartArray(2);
                w.WriteTextString(subject.Name);
                w.WriteInt32(subject.ColorIndex);
                w.WriteEndArray();
            });
            Rows(Key(writer, PhonePackageFormat.Keys.Sections), content.Sections, (w, section) => Texts(w, section.StageName, section.Label));
            Rows(Key(writer, PhonePackageFormat.Keys.Teachers), content.Teachers, (w, teacher) => Texts(w, teacher.Name, teacher.ShortName));
            Rows(Key(writer, PhonePackageFormat.Keys.Lessons), content.Lessons, (w, lesson) => Ints(w, lesson.Day, lesson.Lesson, lesson.Subject, lesson.Section, lesson.Teacher));
        }
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static CborWriter Key(CborWriter writer, int key)
    {
        writer.WriteInt32(key);
        return writer;
    }

    private static void Rows<T>(CborWriter writer, IReadOnlyList<T> rows, Action<CborWriter, T> write)
    {
        writer.WriteStartArray(rows.Count);
        foreach (var row in rows)
            write(writer, row);
        writer.WriteEndArray();
    }

    private static void Ints(CborWriter writer, params int[] values)
    {
        writer.WriteStartArray(values.Length);
        foreach (var value in values)
            writer.WriteInt32(value);
        writer.WriteEndArray();
    }

    private static void Texts(CborWriter writer, params string[] values)
    {
        writer.WriteStartArray(values.Length);
        foreach (var value in values)
            writer.WriteTextString(value);
        writer.WriteEndArray();
    }
}

/// <summary>Why a package was accepted or refused (the phone shows one Arabic message per status).</summary>
public enum PackageReadStatus
{
    Valid,

    /// <summary>Not a package at all (wrong magic, truncated, too large).</summary>
    Malformed,

    /// <summary>A newer envelope or payload format: «حدّث التطبيق».</summary>
    NewerFormat,

    /// <summary>The phone is not paired and the package carries no public key to pair with (a timetable package always carries it).</summary>
    NotPaired,

    /// <summary>Signed for another school.</summary>
    WrongSchool,

    /// <summary>The school id matches but the key id does not: the school key was recreated, so the phone must be paired again.</summary>
    KeyChanged,

    /// <summary>The signature does not match the content (changed after signing, or damaged).</summary>
    Tampered,

    /// <summary>The academic year it was made for has ended.</summary>
    Expired,
}

/// <summary>The school a phone trusts after pairing.</summary>
public sealed record TrustedSchool(Guid SchoolId, byte[] PublicKey);

/// <param name="NewTrust">
/// One scan (DECISIONS_LOG #111): on a phone that is not paired yet, a valid package proposes its own school (id, name and public key).
/// The phone shows the school name and the fingerprint, and stores this trust only when the user confirms. Null otherwise.
/// </param>
public sealed record PackageReadResult(PackageReadStatus Status, PhonePackage? Package, TrustedSchool? NewTrust = null);

/// <summary>
/// The reference reader (the phone app implements the same steps; docs/SST_PACKAGE_FORMAT.md §5). The desktop never
/// imports packages; this reader exists for the tests and the shared test vectors.
/// </summary>
public static class PhonePackageReader
{
    public static PackageReadResult Read(byte[] package, TrustedSchool? trusted, DateOnly today, int supportedFormat = 1)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.Length <= PhonePackageFormat.BodyOffset || package.Length > PhonePackageFormat.MaxPackageBytes
            || !package.AsSpan(0, 3).SequenceEqual(PhonePackageFormat.Magic))
            return new(PackageReadStatus.Malformed, null);
        if (package[3] > PhonePackageFormat.EnvelopeVersion)
            return new(PackageReadStatus.NewerFormat, null);
        if (package[3] != PhonePackageFormat.EnvelopeVersion)
            return new(PackageReadStatus.Malformed, null);
        var signature = package[PhonePackageFormat.SignatureOffset..PhonePackageFormat.BodyOffset];
        var body = package[PhonePackageFormat.BodyOffset..];
        var message = PhonePackageFormat.SignedMessage(package, body);

        PhonePackage? decoded;
        int format;
        try
        {
            var payload = Inflate(body);
            format = FormatOf(payload);
            if (format > supportedFormat)
                return new(PackageReadStatus.NewerFormat, null);
            decoded = Decode(payload);
        }
        catch (Exception exception) when (exception is CborContentException or InvalidOperationException or InvalidDataException or OverflowException or ArgumentException or FormatException)
        {
            // Unreadable after the envelope: the bytes were changed or damaged.
            return new(PackageReadStatus.Tampered, null);
        }

        var header = decoded.Header;
        // A carried key must be the one that signed: its id is the header's key id.
        var carried = decoded.PublicKey;
        if (carried is not null && !SharingService.KeyIdOf(carried).AsSpan().SequenceEqual(header.KeyId))
            return new(PackageReadStatus.Tampered, null);
        if (header.Kind == PhonePackageKind.Pairing && carried is null)
            return new(PackageReadStatus.Tampered, null);
        if (header.Kind != PhonePackageKind.Pairing && decoded.Timetable is null)
            return new(PackageReadStatus.Tampered, null);

        if (trusted is null || header.Kind == PhonePackageKind.Pairing && header.SchoolId != trusted.SchoolId)
        {
            // Not paired yet (or a pairing code of another school, which the phone treats as re-pairing with a confirmation):
            // trust on first use, checked against the key the package carries.
            if (carried is null)
                return new(PackageReadStatus.NotPaired, null);
            if (!Ed25519.Verify(carried, message, signature))
                return new(PackageReadStatus.Tampered, null);
            if (decoded.Timetable is { } fresh && today > fresh.ExpiresOn)
                return new(PackageReadStatus.Expired, decoded);
            return new(PackageReadStatus.Valid, decoded, new TrustedSchool(header.SchoolId, carried));
        }
        if (header.SchoolId != trusted.SchoolId)
            return new(PackageReadStatus.WrongSchool, null);
        if (!SharingService.KeyIdOf(trusted.PublicKey).AsSpan().SequenceEqual(header.KeyId))
            return new(PackageReadStatus.KeyChanged, null);
        if (!Ed25519.Verify(trusted.PublicKey, message, signature))
            return new(PackageReadStatus.Tampered, null);
        if (decoded.Timetable is { } content && today > content.ExpiresOn)
            return new(PackageReadStatus.Expired, decoded);
        return new(PackageReadStatus.Valid, decoded);
    }

    private static byte[] Inflate(byte[] body)
    {
        using var input = new MemoryStream(body);
        using var inflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = inflate.Read(buffer)) > 0)
        {
            if (output.Length + read > PhonePackageFormat.MaxPayloadBytes)
                throw new InvalidDataException("Payload too large.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    /// <summary>Only key 0 is read, so a newer format is recognised even if the rest of its layout changed.</summary>
    private static int FormatOf(byte[] payload)
    {
        var reader = new CborReader(payload, CborConformanceMode.Lax);
        var count = reader.ReadStartMap();
        for (var index = 0; count is null || index < count; index++)
        {
            if (reader.PeekState() == CborReaderState.EndMap)
                break;
            var key = reader.ReadInt32();
            if (key == PhonePackageFormat.Keys.FormatVersion)
                return reader.ReadInt32();
            reader.SkipValue();
        }
        throw new InvalidDataException("No format version.");
    }

    public static PhonePackage Decode(byte[] payload)
    {
        var reader = new CborReader(payload, CborConformanceMode.Canonical);
        var count = reader.ReadStartMap() ?? throw new InvalidDataException("Indefinite map.");
        var values = new Dictionary<int, object>();
        for (var index = 0; index < count; index++)
        {
            var key = reader.ReadInt32();
            values[key] = key switch
            {
                PhonePackageFormat.Keys.SchoolId or PhonePackageFormat.Keys.KeyId or PhonePackageFormat.Keys.PublicKey => reader.ReadByteString(),
                PhonePackageFormat.Keys.SchoolName or PhonePackageFormat.Keys.AcademicYear or PhonePackageFormat.Keys.TimeZone => reader.ReadTextString(),
                PhonePackageFormat.Keys.FormatVersion or PhonePackageFormat.Keys.Kind or PhonePackageFormat.Keys.GeneratedAt or PhonePackageFormat.Keys.TimetableVersion
                    or PhonePackageFormat.Keys.ApprovedAt or PhonePackageFormat.Keys.ExpiresOn or PhonePackageFormat.Keys.YearStart or PhonePackageFormat.Keys.YearEnd
                    or PhonePackageFormat.Keys.WeekStart => reader.ReadInt64(),
                PhonePackageFormat.Keys.Semesters or PhonePackageFormat.Keys.Holidays or PhonePackageFormat.Keys.WorkingDays or PhonePackageFormat.Keys.Sessions
                    or PhonePackageFormat.Keys.SessionDays or PhonePackageFormat.Keys.Subjects or PhonePackageFormat.Keys.Sections or PhonePackageFormat.Keys.Teachers
                    or PhonePackageFormat.Keys.Lessons => ReadAny(reader),
                _ => Skip(reader),
            };
        }
        reader.ReadEndMap();
        if (reader.BytesRemaining != 0)
            throw new InvalidDataException("Trailing bytes.");

        T Get<T>(int key) => values.TryGetValue(key, out var value) && value is T typed ? typed : throw new InvalidDataException($"Key {key} missing.");
        var kind = (PhonePackageKind)Get<long>(PhonePackageFormat.Keys.Kind);
        if (!Enum.IsDefined(kind))
            throw new InvalidDataException("Unknown kind.");
        var header = new PackageHeader((int)Get<long>(PhonePackageFormat.Keys.FormatVersion), kind,
            new Guid(Get<byte[]>(PhonePackageFormat.Keys.SchoolId), bigEndian: true), Get<string>(PhonePackageFormat.Keys.SchoolName),
            Get<byte[]>(PhonePackageFormat.Keys.KeyId), DateTimeOffset.FromUnixTimeSeconds(Get<long>(PhonePackageFormat.Keys.GeneratedAt)));
        var publicKey = values.TryGetValue(PhonePackageFormat.Keys.PublicKey, out var carriedKey) ? carriedKey as byte[] ?? throw new InvalidDataException("Key 6.") : null;
        if (publicKey is not null && publicKey.Length != Ed25519.PublicKeyLength)
            throw new InvalidDataException("Public key length.");
        if (kind == PhonePackageKind.Pairing)
            return new PhonePackage(header, publicKey ?? throw new InvalidDataException("Key 6 missing."), null);

        static List<object> Items(object value) => value as List<object> ?? throw new InvalidDataException("Array expected.");
        static int Int(object value) => value is long number ? checked((int)number) : throw new InvalidDataException("Integer expected.");
        static string Text(object value) => value as string ?? throw new InvalidDataException("Text expected.");
        var rows = (int key) => Items(Get<List<object>>(key)).Select(Items).ToArray();
        var content = new TimetableContent(
            Get<string>(PhonePackageFormat.Keys.AcademicYear),
            (int)Get<long>(PhonePackageFormat.Keys.TimetableVersion),
            DateTimeOffset.FromUnixTimeSeconds(Get<long>(PhonePackageFormat.Keys.ApprovedAt)),
            PhonePackageFormat.Date(Get<long>(PhonePackageFormat.Keys.ExpiresOn)),
            Get<string>(PhonePackageFormat.Keys.TimeZone),
            PhonePackageFormat.Date(Get<long>(PhonePackageFormat.Keys.YearStart)),
            PhonePackageFormat.Date(Get<long>(PhonePackageFormat.Keys.YearEnd)),
            rows(PhonePackageFormat.Keys.Semesters).Select(row => new PackageSemester(PhonePackageFormat.Date(Int(row[0])), PhonePackageFormat.Date(Int(row[1])))).ToArray(),
            // Rows may grow at the end in later builds; a reader takes the fields it knows. An unknown kind reads as a holiday.
            rows(PhonePackageFormat.Keys.Holidays).Select(row =>
            {
                var code = row.Count > 5 ? Int(row[5]) : 0;
                var title = Text(row[2]);
                return new PackageHoliday(PhonePackageFormat.Date(Int(row[0])), PhonePackageFormat.Date(Int(row[1])),
                    title.Length == 0 && code > 0 ? PackageHolidayCodes.TitleOf(code) ?? string.Empty : title,
                    (Int(row[3]) & PhonePackageFormat.HolidayApproximate) != 0, (Int(row[3]) & PhonePackageFormat.HolidayByDecision) != 0,
                    row.Count > 4 && Enum.IsDefined((PackageDayKind)Int(row[4])) ? (PackageDayKind)Int(row[4]) : PackageDayKind.OfficialHoliday, code);
            }).ToArray(),
            Items(Get<List<object>>(PhonePackageFormat.Keys.WorkingDays)).Select(Int).ToArray(),
            (int)Get<long>(PhonePackageFormat.Keys.WeekStart),
            rows(PhonePackageFormat.Keys.Sessions).Select(row => new PackageSession(Int(row[0]),
                Items(row[1]).Select(Items).Select(period => new PackagePeriod(Int(period[0]) == 1, Int(period[1]), Int(period[2]))).ToArray())).ToArray(),
            rows(PhonePackageFormat.Keys.SessionDays).Select(row => new PackageSessionDay(Int(row[0]), Int(row[1]), Int(row[2]))).ToArray(),
            rows(PhonePackageFormat.Keys.Subjects).Select(row => new PackageSubject(Text(row[0]), Int(row[1]))).ToArray(),
            rows(PhonePackageFormat.Keys.Sections).Select(row => new PackageSection(Text(row[0]), Text(row[1]))).ToArray(),
            rows(PhonePackageFormat.Keys.Teachers).Select(row => new PackageTeacher(Text(row[0]), Text(row[1]))).ToArray(),
            rows(PhonePackageFormat.Keys.Lessons).Select(row => new PackageLesson(Int(row[0]), Int(row[1]), Int(row[2]), Int(row[3]), Int(row[4]))).ToArray());
        if (content.Lessons.Any(lesson => lesson.Subject >= content.Subjects.Count || lesson.Section >= content.Sections.Count || lesson.Teacher >= content.Teachers.Count
                || lesson.Subject < 0 || lesson.Section < 0 || lesson.Teacher < 0))
            throw new InvalidDataException("Lesson index out of range.");
        return new PhonePackage(header, publicKey, content);
    }

    private static bool Skip(CborReader reader)
    {
        reader.SkipValue();
        return false;
    }

    /// <summary>Integers, text and nested arrays (all a timetable list holds).</summary>
    private static object ReadAny(CborReader reader) => reader.PeekState() switch
    {
        CborReaderState.UnsignedInteger or CborReaderState.NegativeInteger => reader.ReadInt64(),
        CborReaderState.TextString => reader.ReadTextString(),
        CborReaderState.StartArray => ReadArray(reader),
        _ => throw new InvalidDataException("Unexpected value."),
    };

    private static List<object> ReadArray(CborReader reader)
    {
        var count = reader.ReadStartArray() ?? throw new InvalidDataException("Indefinite array.");
        var items = new List<object>(count);
        for (var index = 0; index < count; index++)
            items.Add(ReadAny(reader));
        reader.ReadEndArray();
        return items;
    }
}
