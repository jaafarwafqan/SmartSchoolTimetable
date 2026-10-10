using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SmartSchoolTimetable.Application.Generation;

/// <summary>Live state of the active run: its phase and the last real solver event.</summary>
public sealed record LiveProgress(string Phase, SolverProgress? Solver);

/// <summary>
/// Process-wide state of generation (singleton): the queue the background worker reads, the live progress the
/// polling endpoint merges into the stored run, and the cancellation of each active run. The single-active rule is
/// enforced by <see cref="StartLock"/> around the database check (GenerationService.StartAsync).
/// </summary>
public sealed class GenerationRegistry
{
    private readonly Channel<long> _queue = Channel.CreateUnbounded<long>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<long, LiveProgress> _progress = new();
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _cancellations = new();

    public SemaphoreSlim StartLock { get; } = new(1, 1);

    public ChannelReader<long> Queue => _queue.Reader;

    public void Enqueue(long runId)
    {
        _cancellations.TryAdd(runId, new CancellationTokenSource());
        _progress[runId] = new LiveProgress(GenerationPhases.Queued, null);
        _queue.Writer.TryWrite(runId);
    }

    public CancellationToken TokenFor(long runId) => _cancellations.GetOrAdd(runId, _ => new CancellationTokenSource()).Token;

    /// <summary>Asks the run to stop; false when it is not running in this process.</summary>
    public bool Cancel(long runId)
    {
        if (!_cancellations.TryGetValue(runId, out var source))
            return false;
        source.Cancel();
        return true;
    }

    public bool IsCancelled(long runId) => _cancellations.TryGetValue(runId, out var source) && source.IsCancellationRequested;

    public void Report(long runId, string phase, SolverProgress? solver = null) =>
        _progress.AddOrUpdate(runId, _ => new LiveProgress(phase, solver), (_, current) => new LiveProgress(phase, solver ?? current.Solver));

    public LiveProgress? ProgressOf(long runId) => _progress.TryGetValue(runId, out var progress) ? progress : null;

    public void Finish(long runId)
    {
        _progress.TryRemove(runId, out _);
        if (_cancellations.TryRemove(runId, out var source))
            source.Dispose();
    }
}

/// <summary>Live phases shown by the progress stepper.</summary>
public static class GenerationPhases
{
    public const string Queued = "queued";
    public const string Validating = "validating";
    public const string Generating = "generating";
    public const string Saving = "saving";
}
