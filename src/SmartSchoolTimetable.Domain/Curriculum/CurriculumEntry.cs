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
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public static CurriculumEntry Create(long stageId, long subjectId, int weeklyLessons, string? label, bool needsDoublePeriod, string? notes)
    {
        var entry = new CurriculumEntry { StageId = stageId, SubjectId = subjectId };
        entry.Apply(weeklyLessons, label, needsDoublePeriod, notes);
        return entry;
    }

    public void Update(int weeklyLessons, string? label, bool needsDoublePeriod, string? notes)
    {
        Apply(weeklyLessons, label, needsDoublePeriod, notes);
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
    public CurriculumEntry CopyTo(long stageId) => Create(stageId, SubjectId, WeeklyLessons, Label, NeedsDoublePeriod, Notes);

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
