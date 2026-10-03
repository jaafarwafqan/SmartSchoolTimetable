namespace SmartSchoolTimetable.Domain;

public sealed class LocalAuditEntry
{
    private LocalAuditEntry()
    {
    }

    public long Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Target { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;

    public static LocalAuditEntry Create(
        DateTimeOffset occurredAt,
        string eventType,
        string target,
        string summary) =>
        new()
        {
            OccurredAt = occurredAt,
            EventType = eventType,
            Target = target,
            Summary = summary
        };
}
