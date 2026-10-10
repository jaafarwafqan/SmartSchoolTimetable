using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.Generation;

/// <summary>Where a version came from.</summary>
public enum TimetableSource
{
    Generated = 1,

    /// <summary>A manual edit of another version (Phase 4 M4).</summary>
    Edited = 2,
}

/// <summary>One placed lesson of a version (owned rows; immutable once the version is written).</summary>
public sealed record TimetableLesson(long SectionId, long CurriculumEntryId, long TeacherId, int Day, int LessonNumber);

/// <summary>
/// A saved timetable (Phase 4 §6). Immutable once written: an edit creates a NEW version linked to its parent. It
/// keeps the scheduling input it was made from (JSON), so it can be read, printed and checked even after the school
/// data changes. Exactly one version per year may be approved («الجدول المعتمد»); older ones stay listed. Nothing
/// deletes versions automatically.
/// </summary>
public sealed class TimetableVersion : VersionedEntity
{
    public const int NoteMaxLength = 200;

    private readonly List<TimetableLesson> _lessons = [];

    private TimetableVersion()
    {
    }

    public long AcademicYearId { get; private set; }
    public int Number { get; private set; }
    public TimetableSource Source { get; private set; }
    public long? GenerationRunId { get; private set; }
    public long? ParentVersionId { get; private set; }
    public string Mode { get; private set; } = string.Empty;
    public string InputHash { get; private set; } = string.Empty;
    public string InputJson { get; private set; } = string.Empty;
    public string? ScoreJson { get; private set; }
    public long? Score { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsApproved { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public IReadOnlyList<TimetableLesson> Lessons => _lessons;

    public static TimetableVersion Create(long academicYearId, int number, TimetableSource source, long? generationRunId, long? parentVersionId, string mode,
        string inputHash, string inputJson, long? score, string? scoreJson, string? note, IEnumerable<TimetableLesson> lessons, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lessons);
        var rows = lessons.ToArray();
        new DomainErrors()
            .When(academicYearId <= 0, nameof(AcademicYearId), DomainErrorCode.Required)
            .When(number <= 0, nameof(Number), DomainErrorCode.OutOfRange)
            .When(string.IsNullOrEmpty(inputJson), nameof(InputJson), DomainErrorCode.Required)
            .When(rows.Length == 0, nameof(Lessons), DomainErrorCode.Required)
            .When(note is { Length: > NoteMaxLength }, nameof(Note), DomainErrorCode.TooLong)
            .ThrowIfAny();
        var version = new TimetableVersion
        {
            AcademicYearId = academicYearId,
            Number = number,
            Source = source,
            GenerationRunId = generationRunId,
            ParentVersionId = parentVersionId,
            Mode = mode,
            InputHash = inputHash,
            InputJson = inputJson,
            Score = score,
            ScoreJson = scoreJson,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = now,
        };
        version._lessons.AddRange(rows);
        return version;
    }

    public void Approve(DateTimeOffset now)
    {
        if (IsApproved)
            return;
        IsApproved = true;
        ApprovedAt = now;
        Touch();
    }

    /// <summary>Another version became the approved one.</summary>
    public void Unapprove()
    {
        if (!IsApproved)
            return;
        IsApproved = false;
        ApprovedAt = null;
        Touch();
    }
}
