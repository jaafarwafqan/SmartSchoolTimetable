using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

public enum PeriodKind { Lesson, Break }

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
    public const int MaxRows = 20;

    private readonly List<LessonPeriod> _periods = [];

    private Shift()
    {
    }

    public long AcademicYearId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public IReadOnlyList<LessonPeriod> Periods => _periods.OrderBy(period => period.Position).ToArray();

    /// <summary>Lessons per day (breaks excluded).</summary>
    public int LessonCount => _periods.Count(period => period.Kind == PeriodKind.Lesson);

    public static Shift Create(long academicYearId, string? name, int displayOrder)
    {
        Validate(name, displayOrder);
        var shift = new Shift { AcademicYearId = academicYearId };
        shift.Apply(name!, displayOrder);
        return shift;
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

    /// <summary>The lesson number (1..N) of each lesson row, in time order.</summary>
    public IReadOnlyList<(int Number, LessonPeriod Period)> Lessons() =>
        Periods.Where(period => period.Kind == PeriodKind.Lesson)
            .Select((period, index) => (index + 1, period))
            .ToArray();

    /// <summary>A copy of this shift and its periods for another academic year (new-year structure copy).</summary>
    public Shift CopyTo(long academicYearId)
    {
        var copy = new Shift { AcademicYearId = academicYearId };
        copy.Apply(Name, DisplayOrder);
        foreach (var period in Periods)
            copy._periods.Add(new LessonPeriod(period.Position, period.ToDraft()));
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
