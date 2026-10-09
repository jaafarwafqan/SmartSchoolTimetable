using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

public enum PeriodKind { Lesson, Break }

/// <summary>Morning and evening shifts are created by the school's shift mode (spec 2.5 §3.2); others are custom.</summary>
public enum ShiftKind { Other, Morning, Evening }

/// <summary>Lessons taught on one working day by a shift: the first <paramref name="Lessons"/> lessons of the shift.</summary>
public sealed record DayLessons(int Day, int Lessons);

/// <summary>A row of a shift's daily schedule, proposed by the owner or by <see cref="PeriodGenerator"/>.</summary>
public sealed record PeriodDraft(PeriodKind Kind, TimeOnly StartTime, TimeOnly EndTime, bool StartBell = true, bool EndBell = true);

/// <summary>A lesson or break row of a shift (owned by the shift). Bell flags apply to lessons only.</summary>
public sealed class LessonPeriod
{
    private LessonPeriod()
    {
    }

    public LessonPeriod(int position, PeriodDraft draft)
    {
        Position = position;
        Kind = draft.Kind;
        StartTime = draft.StartTime;
        EndTime = draft.EndTime;
        StartBell = draft.Kind == PeriodKind.Lesson && draft.StartBell;
        EndBell = draft.Kind == PeriodKind.Lesson && draft.EndBell;
    }

    public long Id { get; private set; }

    /// <summary>1-based row position, breaks included.</summary>
    public int Position { get; private set; }
    public PeriodKind Kind { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public bool StartBell { get; private set; }
    public bool EndBell { get; private set; }

    internal PeriodDraft ToDraft() => new(Kind, StartTime, EndTime, StartBell, EndBell);
}

/// <summary>
/// A shift (صباحي، مسائي …) of an academic year with its daily periods. Period rules: each row ends after it
/// starts, rows are ascending and do not overlap, at least one lesson, at most <see cref="MaxLessons"/> lessons.
/// Lessons are numbered 1..N in time order; breaks have no number (DECISIONS_PENDING #4).
/// </summary>
public sealed class Shift : VersionedEntity
{
    public const int NameMaxLength = 50;
    public const int MaxDisplayOrder = 99;
    public const int MaxLessons = 12;
    /// <summary>Lessons plus a break in every gap between them (R2: a break may follow any lesson but the last).</summary>
    public const int MaxRows = MaxLessons * 2 - 1;

    private readonly List<LessonPeriod> _periods = [];
    private readonly List<DayLessons> _dayLessonOverrides = [];

    private Shift()
    {
    }

    public long AcademicYearId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public ShiftKind Kind { get; private set; }
    public IReadOnlyList<LessonPeriod> Periods => _periods.OrderBy(period => period.Position).ToArray();

    /// <summary>Lessons per day (breaks excluded).</summary>
    public int LessonCount => _periods.Count(period => period.Kind == PeriodKind.Lesson);

    /// <summary>Per-day exceptions only (days whose count differs from <see cref="LessonCount"/>; ADR 0020).</summary>
    public IReadOnlyList<DayLessons> DayLessonOverrides => _dayLessonOverrides.OrderBy(day => day.Day).ToArray();

    /// <summary>Lessons taught on <paramref name="day"/>: the per-day count, capped at the current lesson count.</summary>
    public int LessonsOn(int day) =>
        _dayLessonOverrides.FirstOrDefault(entry => entry.Day == day) is { } entry ? Math.Min(entry.Lessons, LessonCount) : LessonCount;

    /// <summary>Weekly lessons of this shift over the given working days (breaks excluded).</summary>
    public int WeeklyLessons(IEnumerable<int> workingDays) => workingDays.Sum(LessonsOn);

    public static Shift Create(long academicYearId, string? name, int displayOrder, ShiftKind kind = ShiftKind.Other)
    {
        Validate(name, displayOrder);
        var shift = new Shift { AcademicYearId = academicYearId, Kind = kind };
        shift.Apply(name!, displayOrder);
        return shift;
    }

    /// <summary>Marks an existing shift as the school's morning or evening shift (shift mode adopts same-named shifts).</summary>
    public void SetKind(ShiftKind kind)
    {
        new DomainErrors().When(!Enum.IsDefined(kind), nameof(Kind), DomainErrorCode.InvalidOption).ThrowIfAny();
        if (Kind == kind)
            return;
        Kind = kind;
        Touch();
    }

    public void Update(string? name, int displayOrder)
    {
        Validate(name, displayOrder);
        Apply(name!, displayOrder);
        Touch();
    }

    public void ReplacePeriods(IReadOnlyList<PeriodDraft>? drafts)
    {
        drafts ??= [];
        ValidatePeriods(drafts).ThrowIfAny();
        _periods.Clear();
        for (var index = 0; index < drafts.Count; index++)
            _periods.Add(new LessonPeriod(index + 1, drafts[index]));
        Touch();
    }

    /// <summary>
    /// Sets how many lessons are taught on each working day (spec 2.5 §3.1). Each count is 0..<see cref="LessonCount"/>;
    /// days left out use the full count. Only days that differ from the full count are stored.
    /// </summary>
    public void SetDayLessons(IReadOnlyCollection<DayLessons>? counts, IReadOnlyCollection<int> workingDays)
    {
        ArgumentNullException.ThrowIfNull(workingDays);
        counts ??= [];
        new DomainErrors()
            .When(counts.Any(entry => !workingDays.Contains(entry.Day)), "DayLessons", DomainErrorCode.InvalidOption)
            .When(counts.GroupBy(entry => entry.Day).Any(group => group.Count() > 1), "DayLessons", DomainErrorCode.Duplicate)
            .When(counts.Any(entry => entry.Lessons < 0 || entry.Lessons > LessonCount), "DayLessons", DomainErrorCode.OutOfRange)
            .ThrowIfAny();
        _dayLessonOverrides.Clear();
        _dayLessonOverrides.AddRange(counts.Where(entry => entry.Lessons != LessonCount));
        Touch();
    }

    /// <summary>The lesson number (1..N) of each lesson row, in time order.</summary>
    public IReadOnlyList<(int Number, LessonPeriod Period)> Lessons() =>
        Periods.Where(period => period.Kind == PeriodKind.Lesson)
            .Select((period, index) => (index + 1, period))
            .ToArray();

    /// <summary>A copy of this shift and its periods for another academic year (new-year structure copy).</summary>
    public Shift CopyTo(long academicYearId)
    {
        var copy = new Shift { AcademicYearId = academicYearId, Kind = Kind };
        copy.Apply(Name, DisplayOrder);
        foreach (var period in Periods)
            copy._periods.Add(new LessonPeriod(period.Position, period.ToDraft()));
        copy._dayLessonOverrides.AddRange(_dayLessonOverrides);
        return copy;
    }

    /// <summary>Validates a full period list; field names are "Periods" or "Periods[i].StartTime/EndTime".</summary>
    public static DomainErrors ValidatePeriods(IReadOnlyList<PeriodDraft> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        var errors = new DomainErrors();
        var lessons = drafts.Count(draft => draft.Kind == PeriodKind.Lesson);
        errors.When(lessons == 0, "Periods", DomainErrorCode.NoLessonPeriods);
        errors.When(lessons > MaxLessons || drafts.Count > MaxRows, "Periods", DomainErrorCode.TooManyPeriods);
        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            errors.When(!Enum.IsDefined(draft.Kind), $"Periods[{index}].Kind", DomainErrorCode.InvalidOption);
            errors.When(draft.EndTime <= draft.StartTime, $"Periods[{index}].EndTime", DomainErrorCode.InvalidTimeRange);
            if (index == 0)
                continue;
            var previous = drafts[index - 1];
            if (draft.StartTime < previous.StartTime)
                errors.Add($"Periods[{index}].StartTime", DomainErrorCode.PeriodsNotAscending);
            else if (draft.StartTime < previous.EndTime)
                errors.Add($"Periods[{index}].StartTime", DomainErrorCode.PeriodsOverlap);
        }
        return errors;
    }

    private void Apply(string name, int displayOrder)
    {
        Name = ArabicText.Clean(name);
        NormalizedName = ArabicText.Normalize(name);
        DisplayOrder = displayOrder;
    }

    private static void Validate(string? name, int displayOrder) =>
        new DomainErrors()
            .Text(name, nameof(Name), NameMaxLength)
            .When(displayOrder is < 1 or > MaxDisplayOrder, nameof(DisplayOrder), DomainErrorCode.OutOfRange)
            .ThrowIfAny();
}
