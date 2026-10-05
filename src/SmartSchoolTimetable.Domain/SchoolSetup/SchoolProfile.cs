using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

public enum SchoolType { Primary, Intermediate, Preparatory, Secondary, Other }

public enum StudyType { Morning, Evening, Dual }

public enum NumeralSystem { ArabicIndic, Western }

public enum CalendarDisplay { Gregorian, Hijri }

public enum SchoolAssetKind { Logo, Stamp }

/// <summary>Metadata of an uploaded image; the bytes live in the app data folder, never in the database.</summary>
public sealed record SchoolAsset(string StoredFileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt);

/// <summary>The single school of this installation (exactly one row, Id = 1).</summary>
public sealed class SchoolProfile : VersionedEntity
{
    public const long SingletonId = 1;
    public const int NameMaxLength = 200;
    public const int PersonNameMaxLength = 150;
    public const string DefaultTimeZone = "Asia/Baghdad";

    /// <summary>IANA zones offered to the owner (DECISIONS_PENDING #7).</summary>
    public static readonly IReadOnlyList<string> SupportedTimeZones =
    [
        "Asia/Baghdad", "Asia/Riyadh", "Asia/Kuwait", "Asia/Qatar", "Asia/Bahrain", "Asia/Dubai", "Asia/Muscat",
        "Asia/Amman", "Asia/Damascus", "Asia/Beirut", "Asia/Jerusalem", "Africa/Cairo", "Asia/Tehran",
        "Europe/Istanbul", "UTC",
    ];

    private SchoolProfile()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public SchoolType SchoolType { get; private set; } = SchoolType.Intermediate;
    public StudyType StudyType { get; private set; } = StudyType.Morning;
    public string? PrincipalName { get; private set; }
    public string? ScheduleOfficerName { get; private set; }
    public string TimeZoneId { get; private set; } = DefaultTimeZone;
    public NumeralSystem NumeralSystem { get; private set; } = NumeralSystem.ArabicIndic;
    public CalendarDisplay CalendarDisplay { get; private set; } = CalendarDisplay.Gregorian;
    public SchoolAsset? Logo { get; private set; }
    public SchoolAsset? Stamp { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>"School profile filled" step of the setup checklist.</summary>
    public bool IsFilled => !string.IsNullOrWhiteSpace(Name);

    public static SchoolProfile CreateDefault(DateTimeOffset now) => new() { Id = SingletonId, UpdatedAt = now };

    public void Update(
        string? name,
        SchoolType schoolType,
        StudyType studyType,
        string? principalName,
        string? scheduleOfficerName,
        string? timeZoneId,
        NumeralSystem numeralSystem,
        CalendarDisplay calendarDisplay,
        DateTimeOffset now)
    {
        new DomainErrors()
            .Text(name, nameof(Name), NameMaxLength)
            .Text(principalName, nameof(PrincipalName), PersonNameMaxLength, required: false)
            .Text(scheduleOfficerName, nameof(ScheduleOfficerName), PersonNameMaxLength, required: false)
            .When(!Enum.IsDefined(schoolType), nameof(SchoolType), DomainErrorCode.InvalidOption)
            .When(!Enum.IsDefined(studyType), nameof(StudyType), DomainErrorCode.InvalidOption)
            .When(!Enum.IsDefined(numeralSystem), nameof(NumeralSystem), DomainErrorCode.InvalidOption)
            .When(!Enum.IsDefined(calendarDisplay), nameof(CalendarDisplay), DomainErrorCode.InvalidOption)
            .When(timeZoneId is null || !SupportedTimeZones.Contains(timeZoneId), "TimeZone", DomainErrorCode.InvalidOption)
            .ThrowIfAny();

        Name = ArabicText.Clean(name);
        SchoolType = schoolType;
        StudyType = studyType;
        PrincipalName = OptionalText(principalName);
        ScheduleOfficerName = OptionalText(scheduleOfficerName);
        TimeZoneId = timeZoneId!;
        NumeralSystem = numeralSystem;
        CalendarDisplay = calendarDisplay;
        UpdatedAt = now;
        Touch();
    }

    /// <summary>Replaces the asset and returns the previous stored file name so the caller can delete it.</summary>
    public string? SetAsset(SchoolAssetKind kind, SchoolAsset? asset, DateTimeOffset now)
    {
        var previous = kind == SchoolAssetKind.Logo ? Logo : Stamp;
        if (kind == SchoolAssetKind.Logo)
            Logo = asset;
        else
            Stamp = asset;
        UpdatedAt = now;
        Touch();
        return previous?.StoredFileName;
    }

    /// <summary>Shift mode = study type (spec 2.5 §3.2): morning only, evening only or dual.</summary>
    public void SetStudyType(StudyType studyType, DateTimeOffset now)
    {
        new DomainErrors().When(!Enum.IsDefined(studyType), nameof(StudyType), DomainErrorCode.InvalidOption).ThrowIfAny();
        if (StudyType == studyType)
            return;
        StudyType = studyType;
        UpdatedAt = now;
        Touch();
    }

    /// <summary>Wizard step 1: the school type is chosen from cards (no other profile field changes).</summary>
    public void SetSchoolType(SchoolType schoolType, DateTimeOffset now)
    {
        new DomainErrors().When(!Enum.IsDefined(schoolType), nameof(SchoolType), DomainErrorCode.InvalidOption).ThrowIfAny();
        if (SchoolType == schoolType)
            return;
        SchoolType = schoolType;
        UpdatedAt = now;
        Touch();
    }

    public SchoolAsset? GetAsset(SchoolAssetKind kind) => kind == SchoolAssetKind.Logo ? Logo : Stamp;

    private static string? OptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ArabicText.Clean(value);
}
