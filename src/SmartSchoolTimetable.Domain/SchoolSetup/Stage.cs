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
