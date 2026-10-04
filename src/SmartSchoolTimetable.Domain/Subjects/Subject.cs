using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.Subjects;

/// <summary>Editable fields of a subject (spec 2.9).</summary>
public sealed record SubjectDetails(
    string? Name,
    int ColorIndex,
    int Priority,
    bool DistributionEnabled,
    bool SpreadAcrossDays,
    bool Heavy,
    bool RequiresDoublePeriod,
    string? Notes,
    IReadOnlyCollection<BlockedPeriod>? BlockedPeriods);

/// <summary>
/// A subject (global, not year-scoped: DECISIONS_PENDING #1). Colour is one of the ten subject palette tokens,
/// priority 1–5 (5 highest), blocked periods must lie inside the current schedule grid. Soft archive keeps
/// history for later phases. Required resources are Phase 3.
/// </summary>
public sealed class Subject : VersionedEntity
{
    public const int NameMaxLength = 80;
    public const int NotesMaxLength = 500;
    public const int ColorCount = 10;
    public const int MinPriority = 1;
    public const int MaxPriority = 5;

    private readonly List<BlockedPeriod> _blockedPeriods = [];

    private Subject()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int ColorIndex { get; private set; } = 1;
    public int Priority { get; private set; } = 3;
    public bool DistributionEnabled { get; private set; } = true;
    public bool SpreadAcrossDays { get; private set; }
    public bool Heavy { get; private set; }
    public bool RequiresDoublePeriod { get; private set; }
    public string? Notes { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public IReadOnlyList<BlockedPeriod> BlockedPeriods => _blockedPeriods;

    public static Subject Create(SubjectDetails details, ScheduleGrid grid)
    {
        var subject = new Subject();
        subject.Apply(details, grid);
        return subject;
    }

    public void Update(SubjectDetails details, ScheduleGrid grid)
    {
        Apply(details, grid);
        Touch();
    }

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

    private void Apply(SubjectDetails details, ScheduleGrid grid)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(grid);
        var errors = new DomainErrors()
            .Text(details.Name, nameof(Name), NameMaxLength)
            .Text(details.Notes, nameof(Notes), NotesMaxLength, required: false)
            .When(details.ColorIndex is < 1 or > ColorCount, nameof(ColorIndex), DomainErrorCode.OutOfRange)
            .When(details.Priority is < MinPriority or > MaxPriority, nameof(Priority), DomainErrorCode.OutOfRange);
        grid.ValidateBlocked(details.BlockedPeriods, nameof(BlockedPeriods), errors).ThrowIfAny();

        Name = ArabicText.Clean(details.Name);
        NormalizedName = ArabicText.Normalize(details.Name);
        ColorIndex = details.ColorIndex;
        Priority = details.Priority;
        DistributionEnabled = details.DistributionEnabled;
        SpreadAcrossDays = details.SpreadAcrossDays;
        Heavy = details.Heavy;
        RequiresDoublePeriod = details.RequiresDoublePeriod;
        Notes = string.IsNullOrWhiteSpace(details.Notes) ? null : details.Notes.Trim();
        _blockedPeriods.Clear();
        _blockedPeriods.AddRange((details.BlockedPeriods ?? []).Distinct().OrderBy(period => period.Day).ThenBy(period => period.LessonNumber));
    }
}
