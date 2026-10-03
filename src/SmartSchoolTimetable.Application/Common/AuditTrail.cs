using SmartSchoolTimetable.Domain;

namespace SmartSchoolTimetable.Application.Common;

/// <summary>Adds a local history entry to the current unit of work (saved with the change itself).
/// Summaries are neutral and never contain secrets or personal data beyond the record id.</summary>
public static class AuditTrail
{
    public static void Record(IDataStore store, TimeProvider clock, string eventType, string target, string summary)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        store.Add(LocalAuditEntry.Create(clock.GetUtcNow(), eventType, target, summary));
    }
}
