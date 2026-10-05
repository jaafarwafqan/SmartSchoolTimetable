using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>A section belongs to a stage and a shift in the same academic year.</summary>
public sealed class Section : VersionedEntity
{
    public const int LabelMaxLength = 40;
    private Section() { }
    public long StageId { get; private set; }
    public long ShiftId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string NormalizedLabel { get; private set; } = string.Empty;
    public int? StudentCount { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public static Section Create(long stageId, long shiftId, string? label, int? studentCount)
    {
        Validate(label, studentCount);
        var section = new Section { StageId = stageId, ShiftId = shiftId };
        section.Apply(label!);
        section.StudentCount = studentCount;
        return section;
    }

    public void Update(long shiftId, string? label, int? studentCount)
    {
        Validate(label, studentCount);
        ShiftId = shiftId;
        Apply(label!);
        StudentCount = studentCount;
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

    /// <summary>
    /// Weekly capacity of a section = the sum over the working days of the lessons its shift teaches that day
    /// (per-day counts, breaks excluded; spec 2.5 §3.1). Zero without a shift or lessons; Phase 3 compares it
    /// with the planned workload.
    /// </summary>
    public static int WeeklyCapacity(WorkingWeek? week, Shift? shift) =>
        week is null || shift is null ? 0 : shift.WeeklyLessons(week.Days);

    /// <summary>Weekly capacity with the stage's own lessons per day (ADR 0027): never above the shift's.</summary>
    public static int WeeklyCapacity(WorkingWeek? week, Shift? shift, Stage? stage) =>
        week is null || shift is null ? 0 : stage is null ? shift.WeeklyLessons(week.Days) : stage.WeeklyLessons(week.Days, shift);

    public Section CopyTo(long stageId, long shiftId)
    {
        var copy = Create(stageId, shiftId, Label, StudentCount);
        if (IsArchived) copy.Archive(ArchivedAt ?? DateTimeOffset.UtcNow);
        return copy;
    }

    private void Apply(string label)
    {
        Label = ArabicText.Clean(label);
        NormalizedLabel = ArabicText.Normalize(label);
    }

    private static void Validate(string? label, int? students) => new DomainErrors()
        .Text(label, nameof(Label), LabelMaxLength)
        .When(students is < 0 or > 200, nameof(StudentCount), DomainErrorCode.OutOfRange)
        .ThrowIfAny();
}
