using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.Calendar;

public enum CalendarDayKind { OfficialHoliday, SchoolHoliday, Exam, SpecialDay }

/// <summary>Where an entry came from: typed by the owner, or added from the Iraqi official-holidays template (MF8).</summary>
public enum CalendarDaySource { Manual, IraqTemplate }

/// <summary>
/// A dated entry of the academic calendar (spec 2.11): one day or a range, a title, a kind and whether it affects
/// the timetable. Dates outside the current year are allowed; the application reports them as a warning.
/// </summary>
public sealed class CalendarDay : VersionedEntity
{
    public const int TitleMaxLength = 120;
    public const int MaxLengthInDays = 366;

    private CalendarDay()
    {
    }

    public string Title { get; private set; } = string.Empty;
    public string NormalizedTitle { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public CalendarDayKind Kind { get; private set; }
    public bool AffectsSchedule { get; private set; }

    /// <summary>MF8: manual, or from the Iraqi template (then <see cref="TemplateKey"/> names the holiday).</summary>
    public CalendarDaySource Source { get; private set; }
    public string? TemplateKey { get; private set; }

    /// <summary>MF8: the date follows the Hijri calendar by calculation (Umm al-Qura), so the official announcement may differ by a day or two.</summary>
    public bool IsApproximate { get; private set; }

    /// <summary>MF8: a disabled entry stays in the calendar but is ignored (not shown as a holiday, no effect on the schedule).</summary>
    public bool IsEnabled { get; private set; } = true;

    public static CalendarDay FromTemplate(string key, string title, DateOnly startDate, DateOnly endDate, bool approximate)
    {
        var day = new CalendarDay();
        day.Apply(title, startDate, endDate, CalendarDayKind.OfficialHoliday, true);
        day.Source = CalendarDaySource.IraqTemplate;
        day.TemplateKey = key;
        day.IsApproximate = approximate;
        return day;
    }

    public void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
            return;
        IsEnabled = enabled;
        Touch();
    }

    public static CalendarDay Create(string? title, DateOnly startDate, DateOnly? endDate, CalendarDayKind kind, bool affectsSchedule)
    {
        var day = new CalendarDay();
        day.Apply(title, startDate, endDate, kind, affectsSchedule);
        return day;
    }

    public void Update(string? title, DateOnly startDate, DateOnly? endDate, CalendarDayKind kind, bool affectsSchedule)
    {
        Apply(title, startDate, endDate, kind, affectsSchedule);
        IsApproximate = false; // the owner has set the date by hand
        Touch();
    }

    /// <summary>True when any day of the entry falls outside [yearStart, yearEnd].</summary>
    public bool IsOutside(DateOnly yearStart, DateOnly yearEnd) => StartDate < yearStart || EndDate > yearEnd;

    private void Apply(string? title, DateOnly startDate, DateOnly? endDate, CalendarDayKind kind, bool affectsSchedule)
    {
        var end = endDate ?? startDate;
        new DomainErrors()
            .Text(title, nameof(Title), TitleMaxLength)
            .When(end < startDate, "EndDate", DomainErrorCode.InvalidDateRange)
            .When(end.DayNumber - startDate.DayNumber >= MaxLengthInDays, "EndDate", DomainErrorCode.OutOfRange)
            .When(!Enum.IsDefined(kind), nameof(Kind), DomainErrorCode.InvalidOption)
            .ThrowIfAny();
        Title = ArabicText.Clean(title);
        NormalizedTitle = ArabicText.Normalize(title);
        StartDate = startDate;
        EndDate = end;
        Kind = kind;
        AffectsSchedule = affectsSchedule;
    }
}
