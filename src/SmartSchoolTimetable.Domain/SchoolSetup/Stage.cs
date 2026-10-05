using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>A year-scoped school stage; archived stages and sections remain available for history.</summary>
public sealed class Stage : VersionedEntity
{
    public const int NameMaxLength = 80;
    public const int TemplateKeyMaxLength = 40;
    private Stage() { }
    public long AcademicYearId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }

    /// <summary>Template grade (and branch) this stage was created from, e.g. "preparatory-4-scientific"; null when typed.</summary>
    public string? TemplateKey { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    private readonly List<DayLessons> _dayLessons = [];

    /// <summary>
    /// The stage's own lessons per working day (ADR 0027). A day without an entry inherits the shift's count; an
    /// entry never raises it: sections teach the first N lessons of the shift's day.
    /// </summary>
    public IReadOnlyList<DayLessons> DayLessonCounts => _dayLessons.OrderBy(entry => entry.Day).ToArray();

    /// <summary>
    /// True when the counts came from "اقتراح توزيع الحصص اليومية" (ADR 0030); any owner edit clears it, and a later
    /// suggestion never overwrites counts the owner set by hand.
    /// </summary>
    public bool DayLessonsSuggested { get; private set; }

    /// <summary>Lessons a section of this stage has on <paramref name="day"/> in <paramref name="shift"/>.</summary>
    public int LessonsOn(int day, Shift shift)
    {
        ArgumentNullException.ThrowIfNull(shift);
        var shiftLessons = shift.LessonsOn(day);
        return _dayLessons.FirstOrDefault(entry => entry.Day == day) is { } own ? Math.Min(own.Lessons, shiftLessons) : shiftLessons;
    }

    public int WeeklyLessons(IEnumerable<int> workingDays, Shift shift) => workingDays.Sum(day => LessonsOn(day, shift));

    /// <summary>
    /// Sets the stage's lessons per day. Each count is 1..<paramref name="maxOnDay"/> (the most any shift of the stage
    /// teaches that day); an empty list returns every day to the shift's count.
    /// </summary>
    public void SetDayLessons(IReadOnlyCollection<DayLessons>? counts, IReadOnlyCollection<int> workingDays, Func<int, int> maxOnDay)
    {
        ArgumentNullException.ThrowIfNull(workingDays);
        ArgumentNullException.ThrowIfNull(maxOnDay);
        counts ??= [];
        new DomainErrors()
            .When(counts.Any(entry => !workingDays.Contains(entry.Day)), "DayLessons", DomainErrorCode.InvalidOption)
            .When(counts.GroupBy(entry => entry.Day).Any(group => group.Count() > 1), "DayLessons", DomainErrorCode.Duplicate)
            .When(counts.Any(entry => entry.Lessons < 1 || entry.Lessons > maxOnDay(entry.Day)), "DayLessons", DomainErrorCode.OutOfRange)
            .ThrowIfAny();
        _dayLessons.Clear();
        _dayLessons.AddRange(counts);
        DayLessonsSuggested = false;
        Touch();
    }

    /// <summary>Applies the daily distribution suggestion (validated like an owner edit, then marked as suggested).</summary>
    public void ApplySuggestedDayLessons(IReadOnlyCollection<DayLessons> counts, IReadOnlyCollection<int> workingDays, Func<int, int> maxOnDay)
    {
        SetDayLessons(counts, workingDays, maxOnDay);
        DayLessonsSuggested = true;
    }

    /// <summary>Lowers the stage's count for a day to <paramref name="lessons"/> when it is higher (a shift was shortened).</summary>
    public bool ClampDayLessons(int day, int lessons)
    {
        var index = _dayLessons.FindIndex(entry => entry.Day == day);
        if (index < 0 || _dayLessons[index].Lessons <= lessons)
            return false;
        _dayLessons[index] = new DayLessons(day, Math.Max(1, lessons));
        Touch();
        return true;
    }

    public static Stage Create(long academicYearId, string? name, int displayOrder, string? templateKey = null)
    {
        Validate(name, displayOrder);
        new DomainErrors().Text(templateKey, nameof(TemplateKey), TemplateKeyMaxLength, required: false).ThrowIfAny();
        var stage = new Stage { AcademicYearId = academicYearId, TemplateKey = string.IsNullOrWhiteSpace(templateKey) ? null : templateKey.Trim() };
        stage.Apply(name!, displayOrder);
        return stage;
    }

    public void Update(string? name, int displayOrder)
    {
        Validate(name, displayOrder);
        Apply(name!, displayOrder);
        Touch();
    }

    public void Archive(DateTimeOffset now)
    {
        if (IsArchived) return;
        IsArchived = true;
        ArchivedAt = now;
        Touch();
    }

    public void Restore()
    {
        if (!IsArchived) return;
        IsArchived = false;
        ArchivedAt = null;
        Touch();
    }

    public Stage CopyTo(long academicYearId)
    {
        var copy = Create(academicYearId, Name, DisplayOrder, TemplateKey);
        copy._dayLessons.AddRange(_dayLessons);
        copy.DayLessonsSuggested = DayLessonsSuggested;
        if (IsArchived) copy.Archive(ArchivedAt ?? DateTimeOffset.UtcNow);
        return copy;
    }

    private void Apply(string name, int order)
    {
        Name = ArabicText.Clean(name);
        NormalizedName = ArabicText.Normalize(name);
        DisplayOrder = order;
    }

    private static void Validate(string? name, int order) => new DomainErrors()
        .Text(name, nameof(Name), NameMaxLength)
        .When(order is < 1 or > 999, nameof(DisplayOrder), DomainErrorCode.OutOfRange)
        .ThrowIfAny();
}
