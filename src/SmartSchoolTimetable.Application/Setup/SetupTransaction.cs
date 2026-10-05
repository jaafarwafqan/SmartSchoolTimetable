using SmartSchoolTimetable.Application.Common;

namespace SmartSchoolTimetable.Application.Setup;

/// <summary>
/// Runs one template or wizard step as a single transaction (spec 2.5 §5): the step calls the normal services,
/// and the first failed result rolls back everything the step already saved and is returned unchanged.
/// </summary>
internal static class SetupTransaction
{
    private sealed class StepFailed(object result) : Exception("A setup step failed; the transaction is rolled back.")
    {
        public object Result { get; } = result;
    }

    /// <summary>Returns the value of a successful result, or aborts the step with it.</summary>
    public static T Require<T>(OperationResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Succeeded ? result.Value! : throw new StepFailed(result);
    }

    public static async Task<OperationResult<T>> RunAsync<T>(IDataStore store, Func<Task<T>> step, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(step);
        T value = default!;
        try
        {
            await store.ExecuteInTransactionAsync(async () => value = await step(), token);
            return OperationResult.Success(value);
        }
        catch (StepFailed failed)
        {
            return failed.Result switch
            {
                OperationResult<T> same => same,
                _ => CastFailure<T>(failed.Result),
            };
        }
    }

    private static OperationResult<T> CastFailure<T>(object result)
    {
        var cast = result.GetType().GetMethod(nameof(OperationResult<int>.Cast))!.MakeGenericMethod(typeof(T));
        return (OperationResult<T>)cast.Invoke(result, null)!;
    }
}
