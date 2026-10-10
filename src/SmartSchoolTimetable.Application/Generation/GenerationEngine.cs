using SmartSchoolTimetable.Application.Scheduling;

namespace SmartSchoolTimetable.Application.Generation;

/// <param name="Readiness">The pre-solve report; generation does not start while it has errors.</param>
/// <param name="Violations">Hard-constraint violations the independent verifier found (must be empty).</param>
/// <param name="Score">The soft-rule breakdown of the timetable, when there is one.</param>
public sealed record GenerationOutcome(
    SolverResult Result,
    ValidationReport Readiness,
    IReadOnlyList<Violation> Violations,
    TimetableScore? Score,
    string InputHash)
{
    public bool Blocked => !Readiness.Ready;
}

/// <summary>
/// One generation, pure orchestration (Phase 4 §1): the pre-solve validator first (errors block), then the solver,
/// then the independent <see cref="TimetableVerifier"/> on every timetable it returns. A violation there is a defect:
/// the result becomes <see cref="SolverStatus.Failed"/> with <see cref="ErrorCodes.TimetableVerificationFailed"/> and
/// nothing is saved.
/// </summary>
public static class GenerationEngine
{
    public static async Task<GenerationOutcome> RunAsync(ISolver solver, SchedulingInput input, GenerationSettings settings,
        IProgress<SolverProgress>? progress, Action? onValidated, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(solver);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);
        var hash = SchedulingInputHash.Compute(input);
        var readiness = PreSolveValidator.Validate(input, new ValidatorOptions(settings.DoublePeriodsRequired));
        if (!readiness.Ready)
            return new GenerationOutcome(new SolverResult(SolverStatus.Failed, [], null, null, 0, null, 0, null, ErrorCodes.GenerationNotReady), readiness, [], null, hash);
        onValidated?.Invoke();
        var result = await solver.SolveAsync(input, settings, progress, token);
        if (!result.HasTimetable)
            return new GenerationOutcome(result, readiness, [], null, hash);
        var violations = TimetableVerifier.Verify(input, result.Lessons, settings.DoublePeriodsRequired);
        if (violations.Count > 0)
        {
            var failed = result with { Status = SolverStatus.Failed, Lessons = [], ErrorCode = ErrorCodes.TimetableVerificationFailed };
            return new GenerationOutcome(failed, readiness, violations, null, hash);
        }
        return new GenerationOutcome(result, readiness, [], TimetableScorer.Score(input, result.Lessons, settings.DoublePeriodsRequired), hash);
    }
}
