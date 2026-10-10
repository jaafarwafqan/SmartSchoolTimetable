using System.Text.Json;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain;

namespace SmartSchoolTimetable.Application.Audit;

/// <param name="Category">One of <see cref="AuditCategories"/>.</param>
/// <param name="Params">Numbers and stable codes for the Arabic sentence (null for older entries).</param>
public sealed record AuditEntryDto(long Id, DateTimeOffset OccurredAt, string EventType, string Category, string Target, JsonElement? Params);

/// <summary>Filters of the history list: an optional category and/or one exact event type, paged, newest first.</summary>
public sealed record AuditListQuery(string? Category, string? EventType, int? Page, int? PageSize);

/// <summary>
/// «سجل التغييرات» (M1): the local audit history, read-only. It stores event codes and parameters; the screen (and the
/// dashboard's recent-activity feed) turns them into Arabic. Single owner, so entries carry no attribution.
/// </summary>
public sealed class AuditService(IDataStore store)
{
    public async Task<OperationResult<PagedResult<AuditEntryDto>>> ListAsync(AuditListQuery query, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Category is { Length: > 0 } category && !AuditCategories.All.Contains(category))
            return OperationResult.Invalid<PagedResult<AuditEntryDto>>("category", ErrorCodes.AuditFilterInvalid);
        if (query.EventType is { Length: > 0 } eventType && !AuditEvents.Categories.ContainsKey(eventType))
            return OperationResult.Invalid<PagedResult<AuditEntryDto>>("eventType", ErrorCodes.AuditFilterInvalid);

        IQueryable<LocalAuditEntry> rows = store.Read<LocalAuditEntry>();
        if (query.EventType is { Length: > 0 } exact)
            rows = rows.Where(entry => entry.EventType == exact);
        else if (query.Category is { Length: > 0 } selected)
        {
            var known = AuditEvents.Categories.Keys.ToArray();
            var inCategory = AuditEvents.Categories.Where(pair => pair.Value == selected).Select(pair => pair.Key).ToArray();
            // Entries written by an older build with an event type this build does not know belong to the school group.
            rows = selected == AuditCategories.School
                ? rows.Where(entry => inCategory.Contains(entry.EventType) || !known.Contains(entry.EventType))
                : rows.Where(entry => inCategory.Contains(entry.EventType));
        }
        // Ids grow with time (SQLite cannot order by a DateTimeOffset), so newest first is highest id first.
        var ordered = rows.OrderByDescending(entry => entry.Id);
        var listQuery = new ListQuery(null, null, query.Page, query.PageSize, null);
        return OperationResult.Success(await store.ToPageAsync(ordered, listQuery, ToDto, token));
    }

    private static AuditEntryDto ToDto(LocalAuditEntry entry)
    {
        JsonElement? parameters = null;
        if (!string.IsNullOrEmpty(entry.ParamsJson))
        {
            try
            {
                using var document = JsonDocument.Parse(entry.ParamsJson);
                parameters = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                parameters = null;
            }
        }
        return new AuditEntryDto(entry.Id, entry.OccurredAt, entry.EventType, AuditEvents.CategoryOf(entry.EventType), entry.Target, parameters);
    }
}
