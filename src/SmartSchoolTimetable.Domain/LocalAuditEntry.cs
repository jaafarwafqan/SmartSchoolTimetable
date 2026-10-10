namespace SmartSchoolTimetable.Domain;

public sealed class LocalAuditEntry
{
    public const int ParamsMaxLength = 512;

    private LocalAuditEntry()
    {
    }

    public long Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Target { get; private set; } = string.Empty;

    /// <summary>Developer-facing English text. It is never shown to the owner: the screen maps <see cref="EventType"/> and <see cref="ParamsJson"/> to Arabic.</summary>
    public string Summary { get; private set; } = string.Empty;

    /// <summary>The numbers and stable codes the Arabic sentence needs, as a small JSON object (no names, no secrets). Null for entries written before M1.</summary>
    public string? ParamsJson { get; private set; }

    public static LocalAuditEntry Create(
        DateTimeOffset occurredAt,
        string eventType,
        string target,
        string summary,
        string? paramsJson = null) =>
        new()
        {
            OccurredAt = occurredAt,
            EventType = eventType,
            Target = target,
            Summary = summary,
            ParamsJson = paramsJson is { Length: > 0 and <= ParamsMaxLength } ? paramsJson : null
        };
}
