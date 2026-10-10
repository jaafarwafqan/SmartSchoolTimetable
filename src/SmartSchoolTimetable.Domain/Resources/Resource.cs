using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.Resources;

/// <summary>Kinds of shared rooms and places (مختبر، ساحة، قاعة، أخرى).</summary>
public enum ResourceKind
{
    Lab = 1,
    Field = 2,
    Hall = 3,
    Other = 4,
}

/// <summary>
/// A shared resource such as a laboratory or the sports field (Phase 3 §2.2). Global, like subjects and teachers.
/// <see cref="Capacity"/> is how many sections can use it in the same slot; it is evaluated per shift and per slot
/// (two shifts never share a slot). A subject may require one resource (DECISIONS_PENDING #46).
/// </summary>
public sealed class Resource : VersionedEntity
{
    public const int NameMaxLength = 80;
    public const int NotesMaxLength = 500;
    public const int MinCapacity = 1;
    public const int MaxCapacity = 20;
    public const int DefaultCapacity = 1;

    private Resource()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public ResourceKind Kind { get; private set; } = ResourceKind.Other;
    public int Capacity { get; private set; } = DefaultCapacity;
    public string? Notes { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public static Resource Create(string? name, ResourceKind kind, int capacity, string? notes)
    {
        var resource = new Resource();
        resource.Apply(name, kind, capacity, notes);
        return resource;
    }

    public void Update(string? name, ResourceKind kind, int capacity, string? notes)
    {
        Apply(name, kind, capacity, notes);
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

    private void Apply(string? name, ResourceKind kind, int capacity, string? notes)
    {
        new DomainErrors()
            .Text(name, nameof(Name), NameMaxLength)
            .Text(notes, nameof(Notes), NotesMaxLength, required: false)
            .When(!Enum.IsDefined(kind), nameof(Kind), DomainErrorCode.InvalidOption)
            .When(capacity is < MinCapacity or > MaxCapacity, nameof(Capacity), DomainErrorCode.OutOfRange)
            .ThrowIfAny();
        Name = ArabicText.Clean(name);
        NormalizedName = ArabicText.Normalize(name);
        Kind = kind;
        Capacity = capacity;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
