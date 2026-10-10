using System.Text.Json;
using SmartSchoolTimetable.Domain;

namespace SmartSchoolTimetable.Application.Common;

/// <summary>Adds a local history entry to the current unit of work (saved with the change itself).
/// An entry is an event code (<see cref="AuditEvents"/>), a target and a few neutral parameters (numbers and stable codes only);
/// the Arabic sentence is built by the screen. Summaries are developer text and never contain secrets or personal data beyond the record id.</summary>
public static class AuditTrail
{
    private static readonly JsonSerializerOptions ParameterOptions = new(JsonSerializerDefaults.Web);

    public static void Record(IDataStore store, TimeProvider clock, string eventType, string target, string summary, object? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        store.Add(Entry(clock.GetUtcNow(), eventType, target, summary, parameters));
    }

    /// <summary>An entry for stores that save it themselves (the owner account repository).</summary>
    public static LocalAuditEntry Entry(DateTimeOffset now, string eventType, string target, string summary, object? parameters = null) =>
        LocalAuditEntry.Create(now, eventType, target, summary, parameters is null ? null : JsonSerializer.Serialize(parameters, ParameterOptions));
}
