using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.Settings;

/// <summary>The colour theme: follow the system, or always light or dark (M2).</summary>
public enum ThemePreference { System, Light, Dark }

public enum PaperSize { A4, A3 }

public enum PageOrientation { Portrait, Landscape }

/// <summary>Paper and orientation of one print job.</summary>
public sealed record PrintSetting(PaperSize Paper, PageOrientation Orientation);

/// <summary>
/// The owner's stored preferences (exactly one row, Id = 1, versioned like every editable record): the theme, which semester
/// the timetable viewer opens on, and the default paper and orientation of each print job. Digits (Arabic-Indic or Latin)
/// stay on the school profile and the auto-lock timeout on the owner account, where they already live.
/// </summary>
public sealed class AppPreferences : VersionedEntity
{
    public const long SingletonId = 1;

    private AppPreferences()
    {
    }

    public ThemePreference Theme { get; private set; } = ThemePreference.System;

    /// <summary>1 or 2 to always open the viewer on that semester; null to follow the year's dates (then semester 1).</summary>
    public int? DefaultSemester { get; private set; }

    public PaperSize SectionPaper { get; private set; } = PaperSize.A4;
    public PageOrientation SectionOrientation { get; private set; } = PageOrientation.Landscape;
    public PaperSize TeacherPaper { get; private set; } = PaperSize.A4;
    public PageOrientation TeacherOrientation { get; private set; } = PageOrientation.Portrait;
    public PaperSize SchoolPaper { get; private set; } = PaperSize.A3;
    public PageOrientation SchoolOrientation { get; private set; } = PageOrientation.Landscape;

    /// <summary>«ملاءمة الصفحة» on by default.</summary>
    public bool PrintFit { get; private set; } = true;

    public PrintSetting Section => new(SectionPaper, SectionOrientation);
    public PrintSetting Teacher => new(TeacherPaper, TeacherOrientation);
    public PrintSetting School => new(SchoolPaper, SchoolOrientation);

    public static AppPreferences CreateDefault() => new() { Id = SingletonId };

    /// <summary>Returns false (nothing changes, no new version) when the values equal the stored ones.</summary>
    public bool Update(ThemePreference theme, int? defaultSemester, PrintSetting section, PrintSetting teacher, PrintSetting school, bool printFit)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(teacher);
        ArgumentNullException.ThrowIfNull(school);
        new DomainErrors()
            .When(!Enum.IsDefined(theme), nameof(Theme), DomainErrorCode.InvalidOption)
            .When(defaultSemester is not null and not (1 or 2), nameof(DefaultSemester), DomainErrorCode.InvalidOption)
            .When(!Enum.IsDefined(section.Paper) || !Enum.IsDefined(section.Orientation), nameof(Section), DomainErrorCode.InvalidOption)
            .When(!Enum.IsDefined(teacher.Paper) || !Enum.IsDefined(teacher.Orientation), nameof(Teacher), DomainErrorCode.InvalidOption)
            .When(!Enum.IsDefined(school.Paper) || !Enum.IsDefined(school.Orientation), nameof(School), DomainErrorCode.InvalidOption)
            .ThrowIfAny();
        if (Theme == theme && DefaultSemester == defaultSemester && Section == section && Teacher == teacher && School == school && PrintFit == printFit)
            return false;
        Theme = theme;
        DefaultSemester = defaultSemester;
        (SectionPaper, SectionOrientation) = (section.Paper, section.Orientation);
        (TeacherPaper, TeacherOrientation) = (teacher.Paper, teacher.Orientation);
        (SchoolPaper, SchoolOrientation) = (school.Paper, school.Orientation);
        PrintFit = printFit;
        Touch();
        return true;
    }
}
