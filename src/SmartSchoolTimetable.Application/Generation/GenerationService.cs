using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Generation;
using SmartSchoolTimetable.Domain.Scheduling;

namespace SmartSchoolTimetable.Application.Generation;

/// <param name="Mode">«عادي» (standard) or «دروس مزدوجة» (doublePeriods); default standard.</param>
/// <param name="TimeLimitSeconds">10–600; default 60.</param>
/// <param name="Deterministic">«نتيجة قابلة للإعادة»: one worker and the seed.</param>
/// <param name="Seed">Optional; a random one is chosen and stored when absent.</param>
/// <param name="Workers">Advanced; 1 to the number of logical cores.</param>
/// <param name="LockFromVersionId">«إبقاء تعديلاتي»: the manual edits of this version stay exactly where they are; null discards them (a normal generation).</param>
public sealed record StartGenerationCommand(string? Mode, int? TimeLimitSeconds, bool? Deterministic, int? Seed, int? Workers, long? LockFromVersionId = null);

/// <summary>The latest version of a year when it is a manual edit: how many lessons the owner moved by hand.</summary>
public sealed record ManualEditsDto(long VersionId, int Number, int Lessons);

public sealed record ManualEditsResponse(ManualEditsDto? Edits);

public sealed record EngineStatusDto(bool Available, string? Version, int DefaultWorkers, int MaxWorkers, int DefaultTimeLimit, int MinTimeLimit, int MaxTimeLimit);

/// <summary>Real counters of the active run (from solver events only).</summary>
public sealed record LiveDto(string Phase, double? ElapsedSeconds, int Improvements, long? BestObjective, long? BestBound, double? FirstSolutionSeconds);

public sealed record GenerationRunDto(
    long Id,
    long AcademicYearId,
    string Status,
    string Mode,
    int TimeLimitSeconds,
    int Seed,
    int Workers,
    bool Deterministic,
    string SolverVersion,
    string InputHash,
    int ProfileVersion,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    double? ElapsedSeconds,
    long? Objective,
    long? Bound,
    bool Optimal,
    int Improvements,
    double? FirstSolutionSeconds,
    int LessonsPlaced,
    TimetableScore? Score,
    SolverDiagnostics? Diagnostics,
    string? ErrorCode,
    long? TimetableVersionId,
    LiveDto? Live,
    long? LockedFromVersionId = null,
    int LockedLessons = 0,
    int LocksDropped = 0);

/// <summary>JSON of stored scores, diagnostics and input snapshots (camelCase, enums as camelCase text).</summary>
public static class GenerationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string? json) => string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, Options);
}

/// <summary>
/// «التوليد» (Phase 4 §6): starts one generation at a time, runs it in the background worker, reports real
/// progress, cancels, saves the verified timetable as a new version and recovers runs left active by a closed app.
/// </summary>
public sealed class GenerationService(IDataStore store, TimeProvider clock, ISolver solver, ISolverInfo engine, GenerationRegistry registry)
{
    private const string ModeField = "mode";
    private const string TimeLimitField = "timeLimitSeconds";
    private const string WorkersField = "workers";
    private const string SeedField = "seed";
    private const string LockField = "lockFromVersionId";

    public EngineStatusDto Engine() => new(engine.Available, engine.Version, GenerationSettings.DefaultWorkers, Environment.ProcessorCount,
        GenerationSettings.DefaultTimeLimit, GenerationSettings.MinTimeLimit, GenerationSettings.MaxTimeLimit);

    /// <summary>Validates the settings and the data, then queues the run. Refuses while another run is active (409).</summary>
    public async Task<OperationResult<GenerationRunDto>> StartAsync(long yearId, StartGenerationCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!engine.Available)
            return OperationResult.Failure<GenerationRunDto>(ErrorCodes.SolverUnavailable);
        var errors = new InputErrors();
        var mode = string.IsNullOrWhiteSpace(command.Mode) ? GenerationModes.Standard : command.Mode.Trim();
        if (!GenerationModes.All.Contains(mode))
            errors.Add(ModeField, ErrorCodes.InvalidOption);
        var seconds = command.TimeLimitSeconds ?? GenerationSettings.DefaultTimeLimit;
        if (seconds is < GenerationSettings.MinTimeLimit or > GenerationSettings.MaxTimeLimit)
            errors.Add(TimeLimitField, ErrorCodes.ValueOutOfRange);
        var workers = command.Workers ?? GenerationSettings.DefaultWorkers;
        if (workers < 1 || workers > Environment.ProcessorCount)
            errors.Add(WorkersField, ErrorCodes.ValueOutOfRange);
        if (command.Seed is < 0)
            errors.Add(SeedField, ErrorCodes.ValueOutOfRange);
        IReadOnlyList<TimetableLesson> manual = [];
        if (command.LockFromVersionId is { } lockId)
        {
            var source = await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == lockId), token);
            if (source is null || source.AcademicYearId != yearId)
                errors.Add(LockField, ErrorCodes.InvalidOption);
            else
                manual = await ManualEditsAsync(store, source, token);
        }
        if (errors.Any)
            return errors.ToResult<GenerationRunDto>();

        if (await SchedulingInputBuilder.BuildAsync(store, yearId, token) is not { } input)
            return OperationResult.Failure<GenerationRunDto>(ErrorCodes.NotFound);
        var settings = new GenerationSettings(mode, seconds, command.Seed ?? Random.Shared.Next(1, int.MaxValue), workers, command.Deterministic == true);
        if (!PreSolveValidator.Validate(input, new ValidatorOptions(settings.DoublePeriodsRequired)).Ready)
            return OperationResult.Failure<GenerationRunDto>(ErrorCodes.GenerationNotReady);

        await registry.StartLock.WaitAsync(token);
        try
        {
            var active = GenerationRun.ActiveStatuses.ToArray();
            if (await store.AnyAsync(store.Read<GenerationRun>().Where(run => active.Contains(run.Status)), token))
                return OperationResult.Failure<GenerationRunDto>(ErrorCodes.GenerationActive);
            var run = GenerationRun.Queue(yearId, settings.Mode, settings.TimeLimitSeconds, settings.Seed, settings.EffectiveWorkers, settings.Deterministic,
                engine.Version ?? string.Empty, solver.Describe(settings), SchedulingInputHash.Compute(input), input.Profile.ProfileVersion, clock.GetUtcNow(),
                manual.Count > 0 ? command.LockFromVersionId : null, manual.Count);
            store.Add(run);
            AuditTrail.Record(store, clock, AuditEvents.GenerationStarted, $"academic-year:{yearId}", "Timetable generation queued.",
                new { mode = settings.Mode, locked = manual.Count });
            await store.SaveChangesAsync(token);
            registry.Enqueue(run.Id);
            return OperationResult.Success(ToDto(run));
        }
        finally
        {
            registry.StartLock.Release();
        }
    }


    /// <summary>
    /// The lessons the owner moved by hand in <paramref name="version"/>: those that are not in the generated (or restored)
    /// version it descends from through manual edits. Empty for a version that is not a manual edit.
    /// </summary>
    public static async Task<IReadOnlyList<TimetableLesson>> ManualEditsAsync(IDataStore store, TimetableVersion version, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(version);
        if (version.Source != TimetableSource.Edited)
            return [];
        var root = version;
        for (var hops = 0; root.Source == TimetableSource.Edited && root.ParentVersionId is { } parentId && hops < 10_000; hops++)
        {
            var parent = await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == parentId), token);
            if (parent is null)
                break;
            root = parent;
        }
        var baseline = root.Lessons.ToHashSet();
        return version.Lessons.Where(lesson => !baseline.Contains(lesson)).ToArray();
    }

    /// <summary>The year's latest version when it is a manual edit (the generation screen asks what to do with those edits).</summary>
    public async Task<ManualEditsResponse> ManualEditsOfLatestAsync(long yearId, CancellationToken token)
    {
        var latest = await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(version => version.AcademicYearId == yearId)
            .OrderByDescending(version => version.Number), token);
        if (latest is null)
            return new ManualEditsResponse(null);
        var manual = await ManualEditsAsync(store, latest, token);
        return new ManualEditsResponse(manual.Count == 0 ? null : new ManualEditsDto(latest.Id, latest.Number, manual.Count));
    }

    public async Task<OperationResult<GenerationRunDto>> GetAsync(long runId, CancellationToken token) =>
        await store.FirstOrDefaultAsync(store.Read<GenerationRun>().Where(run => run.Id == runId), token) is { } run
            ? OperationResult.Success(ToDto(run))
            : OperationResult.Failure<GenerationRunDto>(ErrorCodes.NotFound);

    /// <summary>The latest run of the year (active or finished), so a reopened page shows where things stand.</summary>
    public async Task<GenerationRunDto?> CurrentAsync(long yearId, CancellationToken token) =>
        await store.FirstOrDefaultAsync(store.Read<GenerationRun>().Where(run => run.AcademicYearId == yearId).OrderByDescending(run => run.Id), token) is { } run
            ? ToDto(run)
            : null;

    public async Task<PagedResult<GenerationRunDto>> ListAsync(long yearId, ListQuery query, CancellationToken token) =>
        await store.ToPageAsync(store.Read<GenerationRun>().Where(run => run.AcademicYearId == yearId).OrderByDescending(run => run.Id), query, ToDto, token);

    /// <summary>«إيقاف»: stops the search; the worker keeps the best verified timetable (DECISIONS_PENDING #73).</summary>
    public async Task<OperationResult<GenerationRunDto>> CancelAsync(long runId, CancellationToken token)
    {
        if (await store.FirstOrDefaultAsync(store.Query<GenerationRun>().Where(run => run.Id == runId), token) is not { } run)
            return OperationResult.Failure<GenerationRunDto>(ErrorCodes.NotFound);
        if (!run.IsActive)
            return OperationResult.Success(ToDto(run));
        if (!registry.Cancel(runId))
        {
            // Not running in this process (should not happen after the start-up recovery): close it honestly.
            run.Finish(GenerationStatus.Cancelled, clock.GetUtcNow(), 0, null, null, false, 0, null, 0, null, null, null, null);
            await store.SaveChangesAsync(token);
        }
        AuditTrail.Record(store, clock, AuditEvents.GenerationCancelled, $"generation:{runId}", "Timetable generation stopped by the owner.");
        await store.SaveChangesAsync(token);
        return OperationResult.Success(ToDto(run));
    }

    /// <summary>At start-up: runs left active by a closed or crashed app become «انقطع» (Interrupted).</summary>
    public async Task<int> RecoverInterruptedAsync(CancellationToken token)
    {
        var active = GenerationRun.ActiveStatuses.ToArray();
        var runs = await store.ListAsync(store.Query<GenerationRun>().Where(run => active.Contains(run.Status)), token);
        foreach (var run in runs)
        {
            run.Interrupt(clock.GetUtcNow());
            AuditTrail.Record(store, clock, AuditEvents.GenerationInterrupted, $"generation:{run.Id}", "Generation interrupted by an application stop.");
        }
        if (runs.Count > 0)
            await store.SaveChangesAsync(token);
        return runs.Count;
    }

    /// <summary>Runs one queued generation to the end (called by the background worker, never on a request thread).</summary>
    public async Task ExecuteAsync(long runId, CancellationToken stopping)
    {
        if (await store.FirstOrDefaultAsync(store.Query<GenerationRun>().Where(item => item.Id == runId), stopping) is not { } run || run.Status != GenerationStatus.Queued)
        {
            registry.Finish(runId);
            return;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopping, registry.TokenFor(runId));
        var clockWatch = Stopwatch.StartNew();
        try
        {
            run.MarkValidating(clock.GetUtcNow());
            await store.SaveChangesAsync(stopping);
            registry.Report(runId, GenerationPhases.Validating);
            if (await SchedulingInputBuilder.BuildAsync(store, run.AcademicYearId, stopping) is not { } input)
            {
                await FinishAsync(run, GenerationStatus.Failed, clockWatch, null, null, ErrorCodes.NotFound, stopping);
                return;
            }
            var settings = new GenerationSettings(run.Mode, run.TimeLimitSeconds, run.Seed, run.Workers, run.Deterministic);
            var droppedBefore = 0;
            if (run.LockedFromVersionId is { } lockedFrom
                && await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == lockedFrom), stopping) is { } lockSource)
            {
                // Keep a manual lesson only while its assignment (section, line, teacher) still exists.
                var assignments = input.Assignments.Select(row => (row.SectionId, row.LineId, row.TeacherId)).ToHashSet();
                var wanted = (await ManualEditsAsync(store, lockSource, stopping))
                    .Select(lesson => new PlacedLesson(lesson.SectionId, lesson.CurriculumEntryId, lesson.TeacherId, lesson.Day, lesson.LessonNumber)).ToArray();
                var kept = wanted.Where(lesson => assignments.Contains((lesson.SectionId, lesson.LineId, lesson.TeacherId))).ToArray();
                droppedBefore = wanted.Length - kept.Length;
                settings = settings with { Locked = kept };
            }
            run.RecordInput(SchedulingInputHash.Compute(input), input.Profile.ProfileVersion);
            var progress = new Progress(registry, runId);
            var outcome = await GenerationEngine.RunAsync(solver, input, settings, progress, () => registry.Report(runId, GenerationPhases.Generating), linked.Token);
            if (outcome.Blocked)
            {
                await FinishAsync(run, GenerationStatus.Failed, clockWatch, outcome, null, ErrorCodes.GenerationNotReady, stopping, droppedBefore);
                return;
            }
            if (run.Status == GenerationStatus.Validating)
                run.MarkGenerating();
            registry.Report(runId, GenerationPhases.Saving);
            var result = outcome.Result;
            long? versionId = null;
            if (outcome.Score is not null && result.HasTimetable)
                versionId = await SaveVersionAsync(run, input, settings, outcome, stopping);
            var status = result.Status switch
            {
                SolverStatus.Optimal or SolverStatus.Feasible => GenerationStatus.Completed,
                SolverStatus.Cancelled => GenerationStatus.Cancelled,
                SolverStatus.Infeasible => GenerationStatus.Infeasible,
                SolverStatus.TimedOut => GenerationStatus.TimedOut,
                _ => GenerationStatus.Failed,
            };
            await FinishAsync(run, status, clockWatch, outcome, versionId, result.ErrorCode, stopping, droppedBefore + result.LocksDropped);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // The app is stopping: the next start marks the run Interrupted.
        }
        finally
        {
            registry.Finish(runId);
        }
    }

    private async Task<long> SaveVersionAsync(GenerationRun run, SchedulingInput input, GenerationSettings settings, GenerationOutcome outcome, CancellationToken token)
    {
        var number = await NextNumberAsync(store, run.AcademicYearId, token);
        var version = TimetableVersion.Create(run.AcademicYearId, number, TimetableSource.Generated, run.Id, null, settings.Mode, outcome.InputHash,
            GenerationJson.Serialize(input), outcome.Score!.Total, GenerationJson.Serialize(outcome.Score),
            null, outcome.Result.Lessons.Select(lesson => new TimetableLesson(lesson.SectionId, lesson.LineId, lesson.TeacherId, lesson.Day, lesson.Lesson)),
            clock.GetUtcNow());
        store.Add(version);
        AuditTrail.Record(store, clock, AuditEvents.TimetableGenerated, $"generation:{run.Id}", $"Timetable version {number} saved from a generation.", new { number });
        await store.SaveChangesAsync(token);
        return version.Id;
    }

    public static async Task<int> NextNumberAsync(IDataStore store, long yearId, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(store);
        var last = await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(version => version.AcademicYearId == yearId)
            .OrderByDescending(version => version.Number).Select(version => (int?)version.Number), token);
        return (last ?? 0) + 1;
    }

    private async Task FinishAsync(GenerationRun run, GenerationStatus status, Stopwatch watch, GenerationOutcome? outcome, long? versionId, string? errorCode,
        CancellationToken token, int locksDropped = 0)
    {
        var result = outcome?.Result;
        run.Finish(status, clock.GetUtcNow(), watch.Elapsed.TotalSeconds, result?.Objective, result?.Bound, result?.Status == SolverStatus.Optimal,
            result?.Improvements ?? 0, result?.FirstSolutionSeconds, versionId is null ? 0 : result?.Lessons.Count ?? 0,
            outcome?.Score is null ? null : GenerationJson.Serialize(outcome.Score),
            result?.Diagnostics is null ? null : GenerationJson.Serialize(result.Diagnostics), errorCode, versionId, locksDropped);
        AuditTrail.Record(store, clock, AuditEvents.GenerationFinished, $"generation:{run.Id}", $"Timetable generation ended: {status}.",
            new { status = JsonNamingPolicy.CamelCase.ConvertName(status.ToString()) });
        await store.SaveChangesAsync(token);
    }

    private GenerationRunDto ToDto(GenerationRun run)
    {
        var live = run.IsActive && registry.ProgressOf(run.Id) is { } progress
            ? new LiveDto(progress.Phase, progress.Solver?.ElapsedSeconds, progress.Solver?.Improvements ?? 0, progress.Solver?.BestObjective,
                progress.Solver?.BestBound, progress.Solver?.FirstSolutionSeconds)
            : null;
        return new GenerationRunDto(run.Id, run.AcademicYearId, JsonNamingPolicy.CamelCase.ConvertName(run.Status.ToString()), run.Mode, run.TimeLimitSeconds, run.Seed,
            run.Workers, run.Deterministic, run.SolverVersion, run.InputHash, run.ProfileVersion, run.QueuedAt, run.StartedAt, run.FinishedAt, run.ElapsedSeconds,
            run.Objective, run.Bound, run.Optimal, run.Improvements, run.FirstSolutionSeconds, run.LessonsPlaced,
            GenerationJson.Deserialize<TimetableScore>(run.ScoreJson), GenerationJson.Deserialize<SolverDiagnostics>(run.DiagnosticsJson), run.ErrorCode,
            run.TimetableVersionId, live, run.LockedFromVersionId, run.LockedLessons, run.LocksDropped);
    }

    /// <summary>Forwards solver events to the registry (no estimated values).</summary>
    private sealed class Progress(GenerationRegistry registry, long runId) : IProgress<SolverProgress>
    {
        public void Report(SolverProgress value) => registry.Report(runId, GenerationPhases.Generating, value);
    }
}
