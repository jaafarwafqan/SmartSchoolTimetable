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
    IReadOnlyCollection<BlockedPeriod>? BlockedPeriods,
    long? RequiredResourceId = null);

/// <summary>Where a daily-limit override applies: every section of a grade, or one section.</summary>
public enum CapScope
{
    Stage = 1,
    Section = 2,
}

/// <summary>A subject's daily limit for one grade (<see cref="CapScope.Stage"/>) or one section; <see cref="Cap"/> is 1 to 6.</summary>
public sealed record SubjectCapOverride(CapScope Scope, long ScopeId, int Cap);

/// <summary>
/// A subject (global, not year-scoped: DECISIONS_PENDING #1). Colour is one of the ten subject palette tokens,
/// priority 1–5 (5 highest), blocked periods must lie inside the current schedule grid. Soft archive keeps
/// history for later phases. A subject may require one resource (Phase 3, DECISIONS_PENDING #46);
/// the Application layer checks that the resource exists and is active.
/// </summary>
public sealed class Subject : VersionedEntity
{
    public const int NameMaxLength = 80;
    public const int NotesMaxLength = 500;
    public const int ColorCount = 10;
    public const int MinPriority = 1;
    public const int MaxPriority = 5;
    public const int DefaultPriority = 3;
    public const int MinDailyCap = 1;
    public const int MaxDailyCap = 6;

    private readonly List<BlockedPeriod> _blockedPeriods = [];
    private readonly List<SubjectCapOverride> _capOverrides = [];

    private Subject()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int ColorIndex { get; private set; } = 1;
    public int Priority { get; private set; } = DefaultPriority;
    public bool DistributionEnabled { get; private set; } = true;
    public bool SpreadAcrossDays { get; private set; }
    public bool Heavy { get; private set; }
    public bool RequiresDoublePeriod { get; private set; }
    public string? Notes { get; private set; }
    public long? RequiredResourceId { get; private set; }

    /// <summary>The owner's override of the most lessons of this subject one section may have on one day (null: the automatic limit, max(2, ceil(weekly ÷ days))).</summary>
    public int? DailyCap { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public IReadOnlyList<BlockedPeriod> BlockedPeriods => _blockedPeriods;

    /// <summary>The daily limit of this subject for one grade or one section, which wins over <see cref="DailyCap"/> (most specific wins: section, then grade, then subject).</summary>
    public IReadOnlyList<SubjectCapOverride> CapOverrides => _capOverrides;

    /// <summary>
    /// Removes blocked slots that fall outside the grid (after working days, shifts or lessons per day shrank,
    /// Phase 3 §5.5) and returns them; the owner confirms this from a preview first.
    /// </summary>
    public IReadOnlyList<BlockedPeriod> DropBlockedOutside(ScheduleGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var orphans = _blockedPeriods.Where(period => !grid.Contains(period)).ToArray();
        if (orphans.Length == 0)
            return orphans;
        _blockedPeriods.RemoveAll(period => !grid.Contains(period));
        Touch();
        return orphans;
    }

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

    /// <summary>Sets or clears the daily limit override (1 to 6 lessons a day).</summary>
    public void SetDailyCap(int? cap)
    {
        new DomainErrors().When(cap is < MinDailyCap or > MaxDailyCap, nameof(DailyCap), DomainErrorCode.OutOfRange).ThrowIfAny();
        DailyCap = cap;
        Touch();
    }

    /// <summary>Sets (or, with null, removes) the daily limit of this subject for one grade or section. Returns false when nothing changed.</summary>
    public bool SetCapOverride(CapScope scope, long scopeId, int? cap)
    {
        new DomainErrors()
            .When(!Enum.IsDefined(scope) || scopeId <= 0, nameof(CapOverrides), DomainErrorCode.InvalidOption)
            .When(cap is < MinDailyCap or > MaxDailyCap, nameof(CapOverrides), DomainErrorCode.OutOfRange)
            .ThrowIfAny();
        var existing = _capOverrides.FindIndex(item => item.Scope == scope && item.ScopeId == scopeId);
        if (existing >= 0 ? cap is { } same && _capOverrides[existing].Cap == same : cap is null)
            return false;
        if (existing >= 0)
            _capOverrides.RemoveAt(existing);
        if (cap is { } value)
            _capOverrides.Add(new SubjectCapOverride(scope, scopeId, value));
        Touch();
        return true;
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
        RequiredResourceId = details.RequiredResourceId;
        _blockedPeriods.Clear();
        _blockedPeriods.AddRange((details.BlockedPeriods ?? []).Distinct().OrderBy(period => period.Day).ThenBy(period => period.LessonNumber));
    }
}
