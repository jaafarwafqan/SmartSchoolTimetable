using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Application.Common;

/// <summary>Shared save and domain-call handling, so every service maps store and rule failures the same way.</summary>
public static class StoreSaving
{
    /// <summary>
    /// Saves pending changes. A stale version becomes CONFLICT (the UI offers a reload); a unique-index violation
    /// becomes DUPLICATE_NAME on <paramref name="duplicateField"/> (a race the pre-check could not see).
    /// </summary>
    public static async Task<OperationResult<T>> SaveAsync<T>(
        this IDataStore store,
        Func<CancellationToken, Task<T>> map,
        string duplicateField,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(map);
        try
        {
            await store.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return OperationResult.Failure<T>(ErrorCodes.Conflict);
        }
        catch (DataConflictException)
        {
            return OperationResult.Invalid<T>(duplicateField, ErrorCodes.DuplicateName);
        }
        return OperationResult.Success(await map(cancellationToken));
    }

    public static Task<OperationResult<T>> SaveAsync<T>(this IDataStore store, Func<T> map, string duplicateField, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(map);
        return store.SaveAsync(_ => Task.FromResult(map()), duplicateField, cancellationToken);
    }

    /// <summary>Runs a Domain operation; returns its rule violations as field errors, or null when it succeeded.</summary>
    public static OperationResult<T>? TryDomain<T>(Action operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            operation();
            return null;
        }
        catch (DomainValidationException exception)
        {
            return OperationResult.FromDomain<T>(exception);
        }
    }
}
