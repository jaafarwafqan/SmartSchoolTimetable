using SmartSchoolTimetable.Application.Scheduling;

namespace SmartSchoolTimetable.Application.Generation;

/// <summary>Generation modes (Phase 4 §4): double lessons are a soft preference or a hard rule (H10).</summary>
public static class GenerationModes
{
    public const string Standard = "standard";
    public const string DoublePeriods = "doublePeriods";

    public static readonly IReadOnlyList<string> All = [Standard, DoublePeriods];
}

/// <param name="Mode">See <see cref="GenerationModes"/>.</param>
/// <param name="TimeLimitSeconds">Total budget, <see cref="MinTimeLimit"/>–<see cref="MaxTimeLimit"/>.</param>
/// <param name="Seed">Stored with the run so the same input and settings can be repeated.</param>
/// <param name="Workers">Search workers; 1 in deterministic mode.</param>
/// <param name="Deterministic">«نتيجة قابلة للإعادة»: one worker and the fixed seed (ADR 0007).</param>
public sealed record GenerationSettings(string Mode, int TimeLimitSeconds, int Seed, int Workers, bool Deterministic)
{
    public const int DefaultTimeLimit = 60;
    public const int MinTimeLimit = 10;
    public const int MaxTimeLimit = 600;

    /// <summary>Manual lessons that must stay exactly where they are («إبقاء تعديلاتي»); empty for a normal generation. Not part of the stored parameters.</summary>
    public IReadOnlyList<PlacedLesson> Locked { get; init; } = [];

    public bool DoublePeriodsRequired => Mode == GenerationModes.DoublePeriods;

    public static int DefaultWorkers => Math.Max(1, Environment.ProcessorCount / 2);

    public int EffectiveWorkers => Deterministic ? 1 : Math.Max(1, Workers);
}

/// <summary>Neutral outcome of one solve (no OR-Tools types).</summary>
public enum SolverStatus
{
    /// <summary>Proven best timetable.</summary>
    Optimal,

    /// <summary>A valid timetable, not proven best (the time ran out while improving).</summary>
    Feasible,

    /// <summary>Proven impossible; <see cref="SolverResult.Diagnostics"/> explains why.</summary>
    Infeasible,

    /// <summary>The time ran out before any valid timetable was found (not a proof of impossibility).</summary>
    TimedOut,

    /// <summary>Stopped by the owner; <see cref="SolverResult.Lessons"/> holds the best timetable found, if any.</summary>
    Cancelled,

    /// <summary>The solver could not run (native library, invalid model).</summary>
    Failed,
}

/// <summary>One placed lesson: a workload assignment line (section, curriculum line, teacher) on a day and lesson number.</summary>
public sealed record PlacedLesson(long SectionId, long LineId, long TeacherId, int Day, int Lesson);

/// <summary>Real solver events only (never an estimated percentage).</summary>
/// <param name="Improvements">Improving solutions found so far.</param>
/// <param name="BestObjective">Penalty of the best timetable so far (lower is better), null before the first one.</param>
/// <param name="BestBound">Proven lower bound of the penalty, when known.</param>
/// <param name="FirstSolutionSeconds">When the first valid timetable was found.</param>
public sealed record SolverProgress(double ElapsedSeconds, int Improvements, long? BestObjective, long? BestBound, double? FirstSolutionSeconds);

/// <summary>Score of one soft rule: its raw penalty count, the weight used and their product.</summary>
public sealed record RuleScore(string Key, bool Enabled, int Weight, long Penalty, long Weighted);

/// <summary>
/// One constraint family of an infeasibility core, or the timeout advice. The required and available numbers are the
/// lessons the family must hold and what it allows (slots, limit or capacity), when computable.
/// </summary>
/// <param name="Fixes">Fix codes shared with the readiness screen (links to the right page).</param>
/// <param name="RelaxationHelps">True when relaxing only this family makes the whole timetable possible; null when untested.</param>
public sealed record SolverFinding(string Code, FindingEntity Entity, IReadOnlyList<FindingEntity> Related, int? Required, int? Available,
    IReadOnlyList<string> Fixes, bool? RelaxationHelps);

/// <param name="Minimal">The conflict set was shrunk to a minimal one inside the budget.</param>
public sealed record SolverDiagnostics(IReadOnlyList<SolverFinding> Findings, bool Minimal);

public sealed record SolverResult(
    SolverStatus Status,
    IReadOnlyList<PlacedLesson> Lessons,
    long? Objective,
    long? Bound,
    int Improvements,
    double? FirstSolutionSeconds,
    double ElapsedSeconds,
    SolverDiagnostics? Diagnostics,
    string? ErrorCode = null,
    int LocksDropped = 0)
{
    public bool HasTimetable => Lessons.Count > 0 && Status is SolverStatus.Optimal or SolverStatus.Feasible or SolverStatus.Cancelled;
}

/// <summary>The scheduling engine port (ADR 0002): the OR-Tools adapter lives only in Infrastructure.</summary>
public interface ISolver
{
    /// <summary>Solves one input. Cancelling <paramref name="token"/> stops the search and returns the best timetable found.</summary>
    Task<SolverResult> SolveAsync(SchedulingInput input, GenerationSettings settings, IProgress<SolverProgress>? progress, CancellationToken token);

    /// <summary>The engine parameters these settings produce (stored with the run for reproducibility).</summary>
    string Describe(GenerationSettings settings);
}

/// <summary>Startup self-check of the solver engine (Phase 4 §1).</summary>
public interface ISolverInfo
{
    /// <summary>True when the native library loaded.</summary>
    bool Available { get; }

    /// <summary>The engine version (for example "OR-Tools 9.15.6755"), or null when it did not load.</summary>
    string? Version { get; }
}
