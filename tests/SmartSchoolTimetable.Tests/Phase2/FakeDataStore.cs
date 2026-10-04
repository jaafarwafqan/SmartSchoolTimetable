using System.Reflection;
using SmartSchoolTimetable.Application.Common;

namespace SmartSchoolTimetable.Tests.Phase2;

/// <summary>
/// In-memory <see cref="IDataStore"/> for Application unit tests. Assigns ids on save (also to owned children
/// that expose an Id) and can inject the failures EF would raise (stale version, unique constraint).
/// </summary>
internal sealed class FakeDataStore : IDataStore
{
    private readonly Dictionary<Type, List<object>> _sets = [];
    private long _nextId = 1;

    public Exception? NextSaveFailure { get; set; }
    public int SaveCount { get; private set; }

    public IQueryable<T> Query<T>() where T : class => SetFor(typeof(T)).OfType<T>().AsQueryable();

    public void Add<T>(T entity) where T : class => SetFor(typeof(T)).Add(entity);

    public void Remove<T>(T entity) where T : class => SetFor(typeof(T)).Remove(entity);

    public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.ToList());

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.FirstOrDefault());

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.Count());

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.Any());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (NextSaveFailure is { } failure)
        {
            NextSaveFailure = null;
            throw failure;
        }
        SaveCount++;
        foreach (var entity in _sets.Values.SelectMany(set => set))
            AssignIds(entity);
        return Task.CompletedTask;
    }

    public Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken cancellationToken) => work();

    private List<object> SetFor(Type type)
    {
        if (!_sets.TryGetValue(type, out var set))
            _sets[type] = set = [];
        return set;
    }

    private void AssignIds(object entity)
    {
        SetIdIfMissing(entity);
        foreach (var property in entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetValue(entity) is System.Collections.IEnumerable children && property.PropertyType != typeof(string))
            {
                foreach (var child in children)
                {
                    if (child is not null && child.GetType().GetProperty("Id") is not null)
                        SetIdIfMissing(child);
                }
            }
        }
    }

    private void SetIdIfMissing(object entity)
    {
        var id = entity.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (id?.PropertyType == typeof(long) && (long)id.GetValue(entity)! == 0)
            id.SetValue(entity, _nextId++);
    }
}

internal sealed class FakeAssetStore : IAssetStore
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public List<string> Deleted { get; } = [];

    public Task<string> SaveAsync(string prefix, string extension, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var name = $"{prefix}-{Guid.NewGuid():N}.{extension}";
        Files[name] = content.ToArray();
        return Task.FromResult(name);
    }

    public Stream? OpenRead(string storedFileName) =>
        Files.TryGetValue(storedFileName, out var bytes) ? new MemoryStream(bytes) : null;

    public void Delete(string storedFileName)
    {
        Deleted.Add(storedFileName);
        Files.Remove(storedFileName);
    }
}
