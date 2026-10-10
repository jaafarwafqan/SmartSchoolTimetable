using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.Generation;

/// <summary>Lifecycle of one generation (Phase 4 §6). Stored as an integer; never reorder.</summary>
public enum GenerationStatus
{
    Queued = 1,
    Validating = 2,
    Generating = 3,
    Completed = 4,
    Cancelled = 5,
    Failed = 6,
    Infeasible = 7,
    TimedOut = 8,

    /// <summary>The app stopped while the run was active (set at the next start).</summary>
    Interrupted = 9,
}

/// <summary>
/// One timetable generation and everything needed to reproduce and explain it (Phase 4 §4, §6): the settings, seed,
/// solver version and parameters, the input hash and profile version, the real progress counters, the score breakdown
/// and the diagnostics (JSON). At most one run is active at a time (<see cref="IsActive"/>, checked before a new one).
/// </summary>
public sealed class GenerationRun : VersionedEntity
{
    public const int ModeMaxLength = 20;
    public const int HashMaxLength = 64;
    public const int CodeMaxLength = 64;
    public const int SolverVersionMaxLength = 64;
    public const int ParametersMaxLength = 256;

    private GenerationRun()
    {
    }

    public long AcademicYearId { get; private set; }
    public GenerationStatus Status { get; private set; }
    public string Mode { get; private set; } = string.Empty;
    public int TimeLimitSeconds { get; private set; }
    public int Seed { get; private set; }
    public int Workers { get; private set; }
    public bool Deterministic { get; private set; }
    public string SolverVersion { get; private set; } = string.Empty;
    public string SolverParameters { get; private set; } = string.Empty;
    public string InputHash { get; private set; } = string.Empty;
    public int ProfileVersion { get; private set; }
    public DateTimeOffset QueuedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public double? ElapsedSeconds { get; private set; }
    public long? Objective { get; private set; }
    public long? Bound { get; private set; }
    public bool Optimal { get; private set; }
    public int Improvements { get; private set; }
    public double? FirstSolutionSeconds { get; private set; }
    public int LessonsPlaced { get; private set; }
    public string? ScoreJson { get; private set; }
    public string? DiagnosticsJson { get; private set; }
    public string? ErrorCode { get; private set; }
    public long? TimetableVersionId { get; private set; }

    /// <summary>The version whose manual edits were kept as locked lessons («إبقاء تعديلاتي»), or null.</summary>
    public long? LockedFromVersionId { get; private set; }

    /// <summary>How many manual lessons were asked to stay in place.</summary>
    public int LockedLessons { get; private set; }

    /// <summary>How many of them could not be kept (the assignment or the slot changed since the edit).</summary>
    public int LocksDropped { get; private set; }

    public static readonly IReadOnlyList<GenerationStatus> ActiveStatuses = [GenerationStatus.Queued, GenerationStatus.Validating, GenerationStatus.Generating];

    public bool IsActive => ActiveStatuses.Contains(Status);

    public static GenerationRun Queue(long academicYearId, string mode, int timeLimitSeconds, int seed, int workers, bool deterministic,
        string solverVersion, string solverParameters, string inputHash, int profileVersion, DateTimeOffset now,
        long? lockedFromVersionId = null, int lockedLessons = 0)
    {
        new DomainErrors()
            .When(academicYearId <= 0, nameof(AcademicYearId), DomainErrorCode.Required)
            .When(string.IsNullOrWhiteSpace(mode) || mode.Length > ModeMaxLength, nameof(Mode), DomainErrorCode.InvalidOption)
            .When(timeLimitSeconds <= 0, nameof(TimeLimitSeconds), DomainErrorCode.OutOfRange)
            .When(workers <= 0, nameof(Workers), DomainErrorCode.OutOfRange)
            .ThrowIfAny();
        return new GenerationRun
        {
            AcademicYearId = academicYearId,
            Status = GenerationStatus.Queued,
            Mode = mode,
            TimeLimitSeconds = timeLimitSeconds,
            Seed = seed,
            Workers = workers,
            Deterministic = deterministic,
            SolverVersion = Truncate(solverVersion, SolverVersionMaxLength),
            SolverParameters = Truncate(solverParameters, ParametersMaxLength),
            InputHash = Truncate(inputHash, HashMaxLength),
            ProfileVersion = profileVersion,
            QueuedAt = now,
            LockedFromVersionId = lockedFromVersionId,
            LockedLessons = lockedLessons,
        };
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    public void MarkValidating(DateTimeOffset now)
    {
        Status = GenerationStatus.Validating;
        StartedAt ??= now;
        Touch();
    }

    public void MarkGenerating()
    {
        Status = GenerationStatus.Generating;
        Touch();
    }

    /// <summary>Records the input that was actually solved (the data may have changed since the run was queued).</summary>
    public void RecordInput(string inputHash, int profileVersion)
    {
        InputHash = Truncate(inputHash, HashMaxLength);
        ProfileVersion = profileVersion;
        Touch();
    }

    /// <summary>Ends the run with its final status and the real counters of the solve.</summary>
    public void Finish(GenerationStatus status, DateTimeOffset now, double elapsedSeconds, long? objective, long? bound, bool optimal, int improvements,
        double? firstSolutionSeconds, int lessonsPlaced, string? scoreJson, string? diagnosticsJson, string? errorCode, long? timetableVersionId,
        int locksDropped = 0)
    {
        new DomainErrors().When(ActiveStatuses.Contains(status), nameof(Status), DomainErrorCode.InvalidOption).ThrowIfAny();
        Status = status;
        FinishedAt = now;
        StartedAt ??= now;
        ElapsedSeconds = elapsedSeconds;
        Objective = objective;
        Bound = bound;
        Optimal = optimal;
        Improvements = improvements;
        FirstSolutionSeconds = firstSolutionSeconds;
        LessonsPlaced = lessonsPlaced;
        ScoreJson = scoreJson;
        DiagnosticsJson = diagnosticsJson;
        ErrorCode = errorCode is null ? null : Truncate(errorCode, CodeMaxLength);
        TimetableVersionId = timetableVersionId;
        LocksDropped = locksDropped;
        Touch();
    }

    /// <summary>At start-up: a run left active by a closed or crashed app.</summary>
    public void Interrupt(DateTimeOffset now)
    {
        if (!IsActive)
            return;
        Status = GenerationStatus.Interrupted;
        FinishedAt = now;
        Touch();
    }
}
