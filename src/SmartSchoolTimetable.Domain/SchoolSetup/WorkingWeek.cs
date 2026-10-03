using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>ISO-8601 weekday numbers: 1 = Monday … 7 = Sunday (DECISIONS_PENDING #3).</summary>
public static class Weekday
{
    public const int Monday = 1;
    public const int Tuesday = 2;
    public const int Wednesday = 3;
    public const int Thursday = 4;
    public const int Friday = 5;
    public const int Saturday = 6;
    public const int Sunday = 7;

    public static bool IsValid(int day) => day is >= Monday and <= Sunday;

    /// <summary>All seven days in display order, starting from <paramref name="weekStart"/>.</summary>
    public static IReadOnlyList<int> InOrderFrom(int weekStart) =>
        Enumerable.Range(0, 7).Select(offset => (weekStart - 1 + offset) % 7 + 1).ToArray();
}

/// <summary>The school's working days and week start (exactly one row, Id = 1). Defaults: Sunday–Thursday.</summary>
public sealed class WorkingWeek : VersionedEntity
{
    public const long SingletonId = 1;
    public static readonly IReadOnlyList<int> DefaultDays = [Weekday.Sunday, Weekday.Monday, Weekday.Tuesday, Weekday.Wednesday, Weekday.Thursday];

    private WorkingWeek()
    {
    }

    /// <summary>Bit (day - 1) is set for each working day.</summary>
    public int DaysMask { get; private set; }
    public int WeekStartDay { get; private set; } = Weekday.Sunday;

    /// <summary>Working days in display order (starting from the week start day).</summary>
    public IReadOnlyList<int> Days => Weekday.InOrderFrom(WeekStartDay).Where(Includes).ToArray();

    public int DayCount => Days.Count;

    public static WorkingWeek CreateDefault()
    {
        var week = new WorkingWeek { Id = SingletonId };
        week.Apply(DefaultDays, Weekday.Sunday);
        return week;
    }

    public bool Includes(int day) => Weekday.IsValid(day) && (DaysMask & (1 << (day - 1))) != 0;

    public void Update(IReadOnlyCollection<int>? days, int weekStartDay)
    {
        days ??= [];
        new DomainErrors()
            .When(days.Count == 0, "Days", DomainErrorCode.NoWorkingDays)
            .When(days.Any(day => !Weekday.IsValid(day)), "Days", DomainErrorCode.InvalidOption)
            .When(!Weekday.IsValid(weekStartDay), "WeekStartDay", DomainErrorCode.InvalidOption)
            .ThrowIfAny();
        Apply(days, weekStartDay);
        Touch();
    }

    private void Apply(IEnumerable<int> days, int weekStartDay)
    {
        DaysMask = days.Aggregate(0, (mask, day) => mask | (1 << (day - 1)));
        WeekStartDay = weekStartDay;
    }
}
