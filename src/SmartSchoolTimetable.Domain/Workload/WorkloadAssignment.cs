using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.Workload;

/// <summary>
/// One teacher for one curriculum line in one section (Phase 3 §2.3, DECISIONS_PENDING #45): the section takes the
/// line's weekly lessons from its stage's curriculum, so the lessons are derived, never stored here. At most one
/// ACTIVE assignment exists per (section, line) (filtered unique index); archived ones keep history.
/// </summary>
public sealed class WorkloadAssignment : VersionedEntity
{
    private WorkloadAssignment()
    {
    }

    public long SectionId { get; private set; }
    public long CurriculumEntryId { get; private set; }
    public long TeacherId { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public static WorkloadAssignment Create(long sectionId, long curriculumEntryId, long teacherId)
    {
        new DomainErrors()
            .When(sectionId <= 0, nameof(SectionId), DomainErrorCode.Required)
            .When(curriculumEntryId <= 0, nameof(CurriculumEntryId), DomainErrorCode.Required)
            .When(teacherId <= 0, nameof(TeacherId), DomainErrorCode.Required)
            .ThrowIfAny();
        return new WorkloadAssignment { SectionId = sectionId, CurriculumEntryId = curriculumEntryId, TeacherId = teacherId };
    }

    /// <summary>Gives the line to another teacher. Returns false when it is already theirs.</summary>
    public bool Reassign(long teacherId)
    {
        new DomainErrors().When(teacherId <= 0, nameof(TeacherId), DomainErrorCode.Required).ThrowIfAny();
        if (teacherId == TeacherId)
            return false;
        TeacherId = teacherId;
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
}
