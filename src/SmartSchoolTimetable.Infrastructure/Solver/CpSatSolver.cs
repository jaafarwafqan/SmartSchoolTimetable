using System.Diagnostics;
using System.Globalization;
using Google.OrTools.Sat;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;

namespace SmartSchoolTimetable.Infrastructure.Solver;

/// <summary>Loads the OR-Tools native library once and reads its version (Phase 4 §1 self-check).</summary>
public sealed class OrToolsInfo : ISolverInfo
{
    private readonly Lazy<string?> _version = new(() =>
    {
        try
        {
            return "OR-Tools " + Google.OrTools.Init.OrToolsVersion.VersionString();
        }
#pragma warning disable CA1031 // A missing or broken native library must not crash the app: generation reports it instead.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    });

    public bool Available => _version.Value is not null;

    public string? Version => _version.Value;
}

/// <summary>
/// The CP-SAT adapter of <see cref="ISolver"/> (ADR 0037). One solve with the weighted objective; real progress from
/// the solution callback; cancellation stops the search and keeps the best timetable; when the solver proves the
/// timetable impossible, a diagnostic model finds and shrinks a conflict core (docs/SOLVER.md §5).
/// </summary>
public sealed class CpSatSolver : ISolver
{
    /// <summary>Share of the time limit the diagnostics may use after an infeasible answer (Phase 4 §5: at most 25%).</summary>
    public const double DiagnosticsShare = 0.25;

    /// <summary>Wall-clock safety net of the deterministic mode, as a multiple of the limit.</summary>
    public const int DeterministicSafetyFactor = 10;

    public Task<SolverResult> SolveAsync(SchedulingInput input, GenerationSettings settings, IProgress<SolverProgress>? progress, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);
        return Task.Run(() => Solve(input, settings, progress, token), CancellationToken.None);
    }

    public string Describe(GenerationSettings settings) => ParametersFor(settings);

    /// <summary>The CP-SAT parameters of a generation (stored with the run for reproducibility).</summary>
    public static string ParametersFor(GenerationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Parameters(settings, settings.TimeLimitSeconds);
    }

    internal static string Parameters(GenerationSettings settings, double seconds, bool diagnostic = false)
    {
        var invariant = CultureInfo.InvariantCulture;
        var workers = diagnostic ? 1 : settings.EffectiveWorkers;
        var time = Math.Max(0.1, seconds).ToString("0.###", invariant);
        // Deterministic mode bounds the search by deterministic time so the same input stops at the same point
        // (ADR 0007). Deterministic units run slower than seconds (about 3 s each, docs/PERFORMANCE.md), so the wall
        // clock is only a safety net at ten times the limit; reaching it ends reproducibility for that run.
        var limit = settings.Deterministic && !diagnostic
            ? $"max_deterministic_time:{time} max_time_in_seconds:{(Math.Max(0.1, seconds) * DeterministicSafetyFactor).ToString("0.###", invariant)}"
            : $"max_time_in_seconds:{time}";
        return $"{limit} num_workers:{workers} random_seed:{settings.Seed.ToString(invariant)} log_search_progress:false";
    }

    private static SolverResult Solve(SchedulingInput input, GenerationSettings settings, IProgress<SolverProgress>? progress, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        if (token.IsCancellationRequested)
            return new SolverResult(SolverStatus.Cancelled, [], null, null, 0, null, 0, null);
        var builder = CpSatModelBuilder.Build(input, settings.DoublePeriodsRequired, diagnostic: false);
        var solver = new CpSolver { StringParameters = Parameters(settings, settings.TimeLimitSeconds) };
        var callback = new ProgressCallback(clock, progress);
        CpSolverStatus status;
        using (token.Register(solver.StopSearch))
            status = solver.Solve(builder.Model, callback);
        var cancelled = token.IsCancellationRequested;
        var elapsed = clock.Elapsed.TotalSeconds;
        switch (status)
        {
            case CpSolverStatus.Optimal:
            case CpSolverStatus.Feasible:
                var lessons = builder.Lines
                    .SelectMany(line => line.Vars.Where(item => solver.BooleanValue(item.Value))
                        .Select(item => new PlacedLesson(line.Row.SectionId, line.Row.LineId, line.Row.TeacherId, item.Key.Day, item.Key.Lesson)))
                    .ToArray();
                var resultStatus = cancelled ? SolverStatus.Cancelled : status == CpSolverStatus.Optimal ? SolverStatus.Optimal : SolverStatus.Feasible;
                return new SolverResult(resultStatus, lessons, (long)Math.Round(solver.ObjectiveValue), (long)Math.Round(solver.BestObjectiveBound),
                    callback.Improvements, callback.FirstSolutionSeconds, elapsed, null);
            case CpSolverStatus.Infeasible:
                var diagnostics = CpSatDiagnostics.Explain(input, settings, settings.TimeLimitSeconds * DiagnosticsShare, token);
                return new SolverResult(SolverStatus.Infeasible, [], null, null, callback.Improvements, null, clock.Elapsed.TotalSeconds, diagnostics);
            case CpSolverStatus.Unknown:
                if (cancelled)
                    return new SolverResult(SolverStatus.Cancelled, [], null, null, 0, null, elapsed, null);
                var advice = new SolverFinding(DiagnosticCodes.TimeoutNoSolution, new FindingEntity(FindingEntities.School, 0, string.Empty), [],
                    settings.TimeLimitSeconds, null, ["raiseTimeLimit", "reviewWarnings"], null);
                var bound = double.IsFinite(solver.BestObjectiveBound) ? (long?)Math.Round(solver.BestObjectiveBound) : null;
                return new SolverResult(SolverStatus.TimedOut, [], null, bound, 0, null, elapsed, new SolverDiagnostics([advice], Minimal: false));
            default:
                return new SolverResult(SolverStatus.Failed, [], null, null, 0, null, elapsed, null, Application.ErrorCodes.SolverFailed);
        }
    }

    /// <summary>Reports every improving solution: elapsed time, count, objective and bound (no estimated percentage).</summary>
    private sealed class ProgressCallback(Stopwatch clock, IProgress<SolverProgress>? progress) : CpSolverSolutionCallback
    {
        public int Improvements { get; private set; }

        public double? FirstSolutionSeconds { get; private set; }

        public override void OnSolutionCallback()
        {
            Improvements++;
            FirstSolutionSeconds ??= clock.Elapsed.TotalSeconds;
            var bound = BestObjectiveBound();
            progress?.Report(new SolverProgress(clock.Elapsed.TotalSeconds, Improvements, (long)Math.Round(ObjectiveValue()),
                double.IsFinite(bound) ? (long)Math.Round(bound) : null, FirstSolutionSeconds));
        }
    }
}
