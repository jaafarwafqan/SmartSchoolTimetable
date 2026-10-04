using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.Calendar;

public enum CalendarDayKind { OfficialHoliday, SchoolHoliday, Exam, SpecialDay }

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

    public static CalendarDay Create(string? title, DateOnly startDate, DateOnly? endDate, CalendarDayKind kind, bool affectsSchedule)
    {
        var day = new CalendarDay();
        day.Apply(title, startDate, endDate, kind, affectsSchedule);
        return day;
    }

    public void Update(string? title, DateOnly startDate, DateOnly? endDate, CalendarDayKind kind, bool affectsSchedule)
    {
        Apply(title, startDate, endDate, kind, affectsSchedule);
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
