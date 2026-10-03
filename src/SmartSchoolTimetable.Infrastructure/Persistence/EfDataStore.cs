using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartSchoolTimetable.Application.Common;

namespace SmartSchoolTimetable.Infrastructure.Persistence;

/// <summary>EF Core implementation of the Application data-store port.</summary>
public sealed class EfDataStore(LocalDbContext dbContext) : IDataStore
{
    private const int SqliteConstraintError = 19;

    public IQueryable<T> Query<T>() where T : class => dbContext.Set<T>();

    public void Add<T>(T entity) where T : class => dbContext.Set<T>().Add(entity);

    public void Remove<T>(T entity) where T : class => dbContext.Set<T>().Remove(entity);

    public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        query.ToListAsync(cancellationToken);

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        query.FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        query.CountAsync(cancellationToken);

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        query.AnyAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await work();
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            dbContext.ChangeTracker.Clear();
            throw new ConcurrencyConflictException("The record was changed by another session.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintError })
        {
            dbContext.ChangeTracker.Clear();
            throw new DataConflictException("A database constraint rejected the change.", exception);
        }
    }
}
