using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.Teachers;

/// <summary>Editable fields of a teacher (spec 2.10). Null limits mean "no limit".</summary>
public sealed record TeacherDetails(
    string? FullName,
    string? ShortName,
    IReadOnlyCollection<int>? OffDays,
    IReadOnlyCollection<BlockedPeriod>? BlockedPeriods,
    bool FullyReleased,
    string? ReleaseReason,
    DateOnly? ReleaseFrom,
    DateOnly? ReleaseTo,
    int? MaxLessonsPerDay,
    int? MaxLessonsPerWeek,
    string? Notes);

/// <summary>
/// A teacher (global, DECISIONS_PENDING #1) with constraints. Rules: short name unique after normalization
/// (checked by the service and a unique index); off days are working days; blocked periods lie inside the schedule
/// grid; max per day ≤ lessons per day and max per week ≤ the grid's weekly capacity, checked once periods exist
/// (DECISIONS_PENDING #5); max per day ≤ max per week; release dates form a valid range.
/// </summary>
public sealed class Teacher : VersionedEntity
{
    public const int FullNameMaxLength = 150;
    public const int ShortNameMaxLength = 40;
    public const int ReasonMaxLength = 200;
    public const int NotesMaxLength = 500;

    private readonly List<BlockedPeriod> _blockedPeriods = [];

    private Teacher()
    {
    }

    public string FullName { get; private set; } = string.Empty;
    public string NormalizedFullName { get; private set; } = string.Empty;
    public string ShortName { get; private set; } = string.Empty;
    public string NormalizedShortName { get; private set; } = string.Empty;

    /// <summary>Bit (day - 1) is set for each off day (ISO weekdays).</summary>
    public int OffDaysMask { get; private set; }
    public bool FullyReleased { get; private set; }
    public string? ReleaseReason { get; private set; }
    public DateOnly? ReleaseFrom { get; private set; }
    public DateOnly? ReleaseTo { get; private set; }
    public int? MaxLessonsPerDay { get; private set; }
    public int? MaxLessonsPerWeek { get; private set; }
    public string? Notes { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public IReadOnlyList<BlockedPeriod> BlockedPeriods => _blockedPeriods;

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
    public IReadOnlyList<int> OffDays => Enumerable.Range(1, 7).Where(day => (OffDaysMask & (1 << (day - 1))) != 0).ToArray();

    public static Teacher Create(TeacherDetails details, ScheduleGrid grid)
    {
        var teacher = new Teacher();
        teacher.Apply(details, grid);
        return teacher;
    }

    public void Update(TeacherDetails details, ScheduleGrid grid)
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

    private void Apply(TeacherDetails details, ScheduleGrid grid)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(grid);
        var offDays = details.OffDays ?? [];
        var errors = new DomainErrors()
            .Text(details.FullName, nameof(FullName), FullNameMaxLength)
            .Text(details.ShortName, nameof(ShortName), ShortNameMaxLength)
            .Text(details.Notes, nameof(Notes), NotesMaxLength, required: false)
            .Text(details.FullyReleased ? details.ReleaseReason : null, nameof(ReleaseReason), ReasonMaxLength, required: false)
            .When(offDays.Any(day => !grid.WorkingDays.Contains(day)), nameof(OffDays), DomainErrorCode.InvalidOption)
            .When(details.FullyReleased && details.ReleaseFrom > details.ReleaseTo, nameof(ReleaseTo), DomainErrorCode.InvalidDateRange)
            .When(details.MaxLessonsPerDay is < 1, nameof(MaxLessonsPerDay), DomainErrorCode.OutOfRange)
            .When(details.MaxLessonsPerWeek is < 1, nameof(MaxLessonsPerWeek), DomainErrorCode.OutOfRange)
            .When(details.MaxLessonsPerDay > details.MaxLessonsPerWeek, nameof(MaxLessonsPerDay), DomainErrorCode.OutOfRange);
        if (grid.LessonsPerDay > 0)
        {
            errors.When(details.MaxLessonsPerDay > grid.LessonsPerDay, nameof(MaxLessonsPerDay), DomainErrorCode.MaxPerDayExceedsPeriods);
            errors.When(details.MaxLessonsPerWeek > grid.MaxWeeklyLessons, nameof(MaxLessonsPerWeek), DomainErrorCode.MaxPerWeekExceedsCapacity);
        }
        grid.ValidateBlocked(details.BlockedPeriods, nameof(BlockedPeriods), errors).ThrowIfAny();

        FullName = ArabicText.Clean(details.FullName);
        NormalizedFullName = ArabicText.Normalize(details.FullName);
        ShortName = ArabicText.Clean(details.ShortName);
        NormalizedShortName = ArabicText.Normalize(details.ShortName);
        OffDaysMask = offDays.Aggregate(0, (mask, day) => mask | (1 << (day - 1)));
        FullyReleased = details.FullyReleased;
        ReleaseReason = details.FullyReleased && !string.IsNullOrWhiteSpace(details.ReleaseReason) ? details.ReleaseReason.Trim() : null;
        ReleaseFrom = details.FullyReleased ? details.ReleaseFrom : null;
        ReleaseTo = details.FullyReleased ? details.ReleaseTo : null;
        MaxLessonsPerDay = details.MaxLessonsPerDay;
        MaxLessonsPerWeek = details.MaxLessonsPerWeek;
        Notes = string.IsNullOrWhiteSpace(details.Notes) ? null : details.Notes.Trim();
        _blockedPeriods.Clear();
        _blockedPeriods.AddRange((details.BlockedPeriods ?? []).Distinct().OrderBy(period => period.Day).ThenBy(period => period.LessonNumber));
    }
}
