using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.Curriculum;

/// <summary>
/// One line of the curriculum table (spec 2.5 §3.3): a subject taught in a stage for N lessons per week. The same
/// subject may appear more than once in a stage (e.g. "قواعد" and "أدب"), told apart by the optional label;
/// (stage, subject) is NOT unique (ADR 0021). Phase 3 turns each entry into one workload line per section.
/// </summary>
public sealed class CurriculumEntry : VersionedEntity
{
    public const int MinWeeklyLessons = 1;
    public const int MaxWeeklyLessons = 15;
    public const int LabelMaxLength = 40;
    public const int NotesMaxLength = 300;

    private CurriculumEntry()
    {
    }

    public long StageId { get; private set; }
    public long SubjectId { get; private set; }
    public int WeeklyLessons { get; private set; }
    public string? Label { get; private set; }
    public string NormalizedLabel { get; private set; } = string.Empty;
    public bool NeedsDoublePeriod { get; private set; }
    public string? Notes { get; private set; }
    /// <summary>
    /// Created or reset from the suggested curriculum template (ADR 0029); cleared as soon as the owner edits the line,
    /// so a later template run never overwrites an owner's value.
    /// </summary>
    public bool IsSuggested { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public static CurriculumEntry Create(long stageId, long subjectId, int weeklyLessons, string? label, bool needsDoublePeriod, string? notes)
    {
        var entry = new CurriculumEntry { StageId = stageId, SubjectId = subjectId };
        entry.Apply(weeklyLessons, label, needsDoublePeriod, notes);
        return entry;
    }

    /// <summary>A line from the suggested template, marked «مقترح».</summary>
    public static CurriculumEntry CreateSuggested(long stageId, long subjectId, int weeklyLessons)
    {
        var entry = Create(stageId, subjectId, weeklyLessons, null, needsDoublePeriod: false, null);
        entry.IsSuggested = true;
        return entry;
    }

    /// <summary>An owner edit: the line is no longer a suggestion.</summary>
    public void Update(int weeklyLessons, string? label, bool needsDoublePeriod, string? notes)
    {
        Apply(weeklyLessons, label, needsDoublePeriod, notes);
        IsSuggested = false;
        Touch();
    }

    /// <summary>"إعادة المقترح لهذه المرحلة" after the owner confirmed the before/after preview.</summary>
    public void ResetToSuggestion(int weeklyLessons)
    {
        Apply(weeklyLessons, Label, NeedsDoublePeriod, Notes);
        IsSuggested = true;
        Touch();
    }

    public void SetWeeklyLessons(int weeklyLessons) => Update(weeklyLessons, Label, NeedsDoublePeriod, Notes);

    public void Archive(DateTimeOffset now)
    {
        if (IsArchived)
            return;
        IsArchived = true;
        ArchivedAt = now;
        Touch();
    }

    public void Restore()
    {
        if (!IsArchived)
            return;
        IsArchived = false;
        ArchivedAt = null;
        Touch();
    }

    /// <summary>A copy for another stage (curriculum copy helper).</summary>
    public CurriculumEntry CopyTo(long stageId)
    {
        var copy = Create(stageId, SubjectId, WeeklyLessons, Label, NeedsDoublePeriod, Notes);
        copy.IsSuggested = IsSuggested;
        return copy;
    }

    /// <summary>True for the same subject and label (normalized); used to skip duplicates when copying.</summary>
    public bool SameLineAs(long subjectId, string? label) => SubjectId == subjectId && NormalizedLabel == ArabicText.Normalize(label);

    private void Apply(int weeklyLessons, string? label, bool needsDoublePeriod, string? notes)
    {
        new DomainErrors()
            .When(weeklyLessons is < MinWeeklyLessons or > MaxWeeklyLessons, nameof(WeeklyLessons), DomainErrorCode.OutOfRange)
            .Text(label, nameof(Label), LabelMaxLength, required: false)
            .Text(notes, nameof(Notes), NotesMaxLength, required: false)
            .ThrowIfAny();
        WeeklyLessons = weeklyLessons;
        Label = string.IsNullOrWhiteSpace(label) ? null : ArabicText.Clean(label);
        NormalizedLabel = ArabicText.Normalize(label);
        NeedsDoublePeriod = needsDoublePeriod;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
