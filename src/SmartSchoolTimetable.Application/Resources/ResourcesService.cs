using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Resources;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Resources;

/// <param name="Kind">lab, field, hall or other.</param>
public sealed record ResourceDto(long Id, string Name, string Kind, int Capacity, string? Notes, bool IsArchived, DateTimeOffset? ArchivedAt, int Version);

/// <param name="Kind">lab, field, hall or other.</param>
/// <param name="Capacity">Sections that can use the resource in the same slot (1–20, default 1).</param>
public sealed record SaveResourceCommand(string? Name, string? Kind, int? Capacity, string? Notes, int Version);

/// <summary>
/// Shared resources (Phase 3 §2.2): unique normalized names, a kind and a capacity; soft archive; delete and archive
/// go through the reference guard (subjects that require the resource); every edit carries the version it read.
/// </summary>
public sealed class ResourcesService(IDataStore store, TimeProvider clock)
{
    private static readonly Dictionary<string, ResourceKind> Kinds = new(StringComparer.Ordinal)
    {
        ["lab"] = ResourceKind.Lab,
        ["field"] = ResourceKind.Field,
        ["hall"] = ResourceKind.Hall,
        ["other"] = ResourceKind.Other,
    };

    private readonly ReferenceGuard references = new(store);

    public static string KindName(ResourceKind kind) => Kinds.First(pair => pair.Value == kind).Key;

    public async Task<PagedResult<ResourceDto>> ListAsync(ListQuery query, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(query);
        var rows = store.Read<Resource>();
        if (!query.WithArchived)
            rows = rows.Where(row => !row.IsArchived);
        var search = query.NormalizedSearch;
        if (search.Length > 0)
            rows = rows.Where(row => row.NormalizedName.Contains(search));
        rows = query.SortKey("name") switch
        {
            ("name", true) => rows.OrderByDescending(row => row.NormalizedName),
            ("kind", false) => rows.OrderBy(row => row.Kind).ThenBy(row => row.NormalizedName),
            ("kind", true) => rows.OrderByDescending(row => row.Kind).ThenBy(row => row.NormalizedName),
            ("capacity", false) => rows.OrderBy(row => row.Capacity).ThenBy(row => row.NormalizedName),
            ("capacity", true) => rows.OrderByDescending(row => row.Capacity).ThenBy(row => row.NormalizedName),
            _ => rows.OrderBy(row => row.NormalizedName),
        };
        return await store.ToPageAsync(rows, query, ToDto, token);
    }

    public async Task<OperationResult<ResourceDto>> CreateAsync(SaveResourceCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (ParseKind(command) is not { } kind)
            return OperationResult.Invalid<ResourceDto>(nameof(command.Kind), ErrorCodes.InvalidOption);
        Resource? resource = null;
        if (StoreSaving.TryDomain<ResourceDto>(() => resource = Resource.Create(command.Name, kind, command.Capacity ?? Resource.DefaultCapacity, command.Notes)) is { } invalid)
            return invalid;
        if (await NameTakenAsync(null, command.Name, token))
            return OperationResult.Invalid<ResourceDto>(nameof(command.Name), ErrorCodes.DuplicateName);
        store.Add(resource!);
        AuditTrail.Record(store, clock, AuditEvents.ResourceCreated, "resource", "Resource created.");
        return await store.SaveAsync(() => ToDto(resource!), nameof(command.Name), token);
    }

    public async Task<OperationResult<ResourceDto>> UpdateAsync(long id, SaveResourceCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindAsync(id, token) is not { } resource)
            return OperationResult.Failure<ResourceDto>(ErrorCodes.NotFound);
        if (!resource.IsVersion(command.Version))
            return OperationResult.Failure<ResourceDto>(ErrorCodes.Conflict);
        if (ParseKind(command) is not { } kind)
            return OperationResult.Invalid<ResourceDto>(nameof(command.Kind), ErrorCodes.InvalidOption);
        if (await NameTakenAsync(id, command.Name, token))
            return OperationResult.Invalid<ResourceDto>(nameof(command.Name), ErrorCodes.DuplicateName);
        if (StoreSaving.TryDomain<ResourceDto>(() => resource.Update(command.Name, kind, command.Capacity ?? resource.Capacity, command.Notes)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, AuditEvents.ResourceUpdated, $"resource:{id}", "Resource updated.");
        return await store.SaveAsync(() => ToDto(resource), nameof(command.Name), token);
    }

    public async Task<OperationResult<ResourceDto>> SetArchivedAsync(long id, int version, bool archived, CancellationToken token)
    {
        if (await FindAsync(id, token) is not { } resource)
            return OperationResult.Failure<ResourceDto>(ErrorCodes.NotFound);
        if (!resource.IsVersion(version))
            return OperationResult.Failure<ResourceDto>(ErrorCodes.Conflict);
        if (archived && await references.ArchiveBlockedAsync(ReferenceKinds.Resource, id, token) is { } inUse)
            return OperationResult.Failure<ResourceDto>(inUse);
        if (archived)
            resource.Archive(clock.GetUtcNow());
        else
            resource.Restore();
        AuditTrail.Record(store, clock, archived ? AuditEvents.ResourceArchived : AuditEvents.ResourceRestored, $"resource:{id}", archived ? "Resource archived." : "Resource restored.");
        return await store.SaveAsync(() => ToDto(resource), "Name", token);
    }

    public async Task<OperationResult<bool>> DeleteAsync(long id, int version, CancellationToken token)
    {
        if (await FindAsync(id, token) is not { } resource)
            return OperationResult.Failure<bool>(ErrorCodes.NotFound);
        if (!resource.IsVersion(version))
            return OperationResult.Failure<bool>(ErrorCodes.Conflict);
        if (await references.DeleteBlockedAsync(ReferenceKinds.Resource, id, token) is { } inUse)
            return OperationResult.Failure<bool>(inUse);
        store.Remove(resource);
        AuditTrail.Record(store, clock, AuditEvents.ResourceDeleted, $"resource:{id}", "Resource deleted.");
        return await store.SaveAsync(() => true, "Name", token);
    }

    private static ResourceKind? ParseKind(SaveResourceCommand command) =>
        command.Kind is null ? ResourceKind.Other : Kinds.TryGetValue(command.Kind, out var kind) ? kind : null;

    private Task<Resource?> FindAsync(long id, CancellationToken token) =>
        store.FirstOrDefaultAsync(store.Query<Resource>().Where(row => row.Id == id), token);

    private async Task<bool> NameTakenAsync(long? exceptId, string? name, CancellationToken token)
    {
        var normalized = ArabicText.Normalize(name);
        return normalized.Length > 0 && await store.AnyAsync(
            store.Query<Resource>().Where(row => row.Id != exceptId && row.NormalizedName == normalized), token);
    }

    private static ResourceDto ToDto(Resource row) =>
        new(row.Id, row.Name, KindName(row.Kind), row.Capacity, row.Notes, row.IsArchived, row.ArchivedAt, row.Version);
}
