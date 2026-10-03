namespace SmartSchoolTimetable.Application.Common;

/// <summary>
/// Persistence port for the Phase 2 aggregates. Application composes LINQ queries over <see cref="Query{T}"/>
/// and executes them through the async helpers, so it never references EF Core (architecture tests).
/// Owned collections (terms, periods, blocked slots) load together with their aggregate.
/// </summary>
public interface IDataStore
{
    IQueryable<T> Query<T>() where T : class;

    void Add<T>(T entity) where T : class;

    void Remove<T>(T entity) where T : class;

    Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    /// <summary>Saves the unit of work. Throws <see cref="ConcurrencyConflictException"/> on a stale version
    /// and <see cref="DataConflictException"/> when a database uniqueness rule is violated.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Runs several saves atomically (used when an intermediate state must be written first,
    /// for example clearing the old current year before marking the new one).</summary>
    Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken cancellationToken);
}

/// <summary>The row changed since it was loaded (another tab saved first).</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A database constraint (for example a unique name) rejected the save.</summary>
public sealed class DataConflictException : Exception
{
    public DataConflictException()
    {
    }

    public DataConflictException(string message)
        : base(message)
    {
    }

    public DataConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
