namespace SmartSchoolTimetable.Application.Sharing;

/// <summary>What a date is for the school, by the rules of docs/SST_PACKAGE_FORMAT.md §4 (the phone app follows the same steps).</summary>
public enum SchoolDayStatus
{
    /// <summary>A working day with lessons (some sections may still have none).</summary>
    School,

    /// <summary>A key-18 entry covers the date (a holiday, or an exam or special day that affects the timetable).</summary>
    DayOff,

    /// <summary>Not one of the school's working days (for example Friday).</summary>
    NotAWorkingDay,

    /// <summary>Before the academic year starts or after it ends.</summary>
    OutsideYear,
}

/// <param name="Semester">1 or 2: the semester whose dates contain the date; 1 when none does.</param>
/// <param name="Session">The session whose clock applies (1 morning, 2 noon, 3 evening).</param>
/// <param name="Periods">That session's rows (lessons and breaks) in time order; empty unless <see cref="Status"/> is School.</param>
/// <param name="DayOff">The calendar entry that makes the date a day off.</param>
public sealed record SchoolDay(SchoolDayStatus Status, int Day, int Semester, int Session, IReadOnlyList<PackagePeriod> Periods, PackageHoliday? DayOff = null)
{
    /// <summary>Lesson rows with their lesson number (1..N): breaks are not numbered.</summary>
    public IReadOnlyList<(int Number, PackagePeriod Period)> Lessons() =>
        Periods.Where(period => !period.IsBreak).Select((period, index) => (index + 1, period)).ToArray();
}

public static class PackageCalendar
{
    /// <summary>ISO weekday (1 = Monday … 7 = Sunday).</summary>
    public static int IsoDay(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    public static SchoolDay Resolve(TimetableContent content, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(content);
        var day = IsoDay(date);
        var semesterIndex = content.Semesters.Select((semester, index) => (semester, index)).FirstOrDefault(item => item.semester.Start <= date && date <= item.semester.End);
        var semester = semesterIndex.semester is null ? 1 : semesterIndex.index + 1;
        var session = content.SessionDays.FirstOrDefault(item => item.Semester == semester && item.Day == day)?.Session ?? 1;
        if (date < content.YearStart || date > content.YearEnd)
            return new SchoolDay(SchoolDayStatus.OutsideYear, day, semester, session, []);
        if (content.Holidays.FirstOrDefault(item => item.Start <= date && date <= item.End) is { } off)
            return new SchoolDay(SchoolDayStatus.DayOff, day, semester, session, [], off);
        if (!content.WorkingDays.Contains(day))
            return new SchoolDay(SchoolDayStatus.NotAWorkingDay, day, semester, session, []);
        var periods = content.Sessions.FirstOrDefault(item => item.Session == session)?.Periods
            ?? (content.Sessions.Count > 0 ? content.Sessions[0].Periods : []);
        return new SchoolDay(SchoolDayStatus.School, day, semester, session, periods);
    }
}
