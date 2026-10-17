using System.Diagnostics;
using System.Globalization;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Infrastructure.Solver;
using Xunit.Abstractions;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>Runs only when SST_PERFORMANCE=1 (an explicit command, never in the default run; docs/PERFORMANCE.md).</summary>
public sealed class PerformanceFactAttribute : FactAttribute
{
    public PerformanceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SST_PERFORMANCE") != "1")
            Skip = "Performance measurement: set SST_PERFORMANCE=1 and filter Category=Performance.";
    }
}

/// <summary>
/// The measurement gate of the delivery (PHASE_4_MVP): 12 sections / 20 teachers to a valid timetable within 30 s and
/// 24 sections within 60 s. Synthetic schools from <see cref="SyntheticSchools.Realistic"/>; each line printed is a
/// real measurement: status, time to the first timetable, total time, objective, bound and the verifier's verdict.
/// </summary>
[Trait("Category", "Performance")]
public sealed class PerformanceTests(ITestOutputHelper output)
{
    private async Task<(SolverResult Result, int Violations)> Measure(string name, SchedulingInput input, int seconds, int workers, bool deterministic)
    {
        var settings = new GenerationSettings(GenerationModes.Standard, seconds, 12345, workers, deterministic);
        var memoryBefore = Process.GetCurrentProcess().WorkingSet64;
        var clock = Stopwatch.StartNew();
        var result = await new CpSatSolver().SolveAsync(input, settings, null, CancellationToken.None);
        clock.Stop();
        var memoryAfter = Process.GetCurrentProcess().PeakWorkingSet64;
        var violations = result.HasTimetable ? TimetableVerifier.Verify(input, result.Lessons, false).Count : -1;
        var invariant = CultureInfo.InvariantCulture;
        output.WriteLine(string.Create(invariant,
            $"| {name} | {input.Sections.Count} | {input.Teachers.Count} | {settings.EffectiveWorkers} | {result.Status} | {result.FirstSolutionSeconds?.ToString("0.00", invariant) ?? "-"} | {clock.Elapsed.TotalSeconds:0.00} | {result.Objective?.ToString(invariant) ?? "-"} | {result.Bound?.ToString(invariant) ?? "-"} | {(violations < 0 ? "-" : violations.ToString(invariant))} | {memoryBefore / 1048576} → {memoryAfter / 1048576} MB |"));
        return (result, violations);
    }

    /// <summary>The gate, in the owner's default settings (default workers, 60 s), on three synthetic schools per size.</summary>
    [PerformanceFact]
    public async Task TwelveSectionsTwentyTeachersFirstTimetableWithin30Seconds()
    {
        foreach (var seed in new[] { 1, 2, 3 })
        {
            var (result, violations) = await Measure($"12/20 s{seed}", SyntheticSchools.Realistic(12, 20, seed), 60, GenerationSettings.DefaultWorkers, deterministic: false);
            Assert.True(result.HasTimetable);
            Assert.Equal(0, violations);
            Assert.True(result.FirstSolutionSeconds < 30, $"first timetable after {result.FirstSolutionSeconds} s");
        }
    }

    [PerformanceFact]
    public async Task TwentyFourSectionsFirstTimetableWithin60Seconds()
    {
        foreach (var seed in new[] { 1, 2, 3 })
        {
            var (result, violations) = await Measure($"24/40 s{seed}", SyntheticSchools.Realistic(24, 40, seed), 60, GenerationSettings.DefaultWorkers, deterministic: false);
            Assert.True(result.HasTimetable);
            Assert.Equal(0, violations);
            Assert.True(result.FirstSolutionSeconds < 60, $"first timetable after {result.FirstSolutionSeconds} s");
        }
    }

    /// <summary>The production defaults of the profile (Phase 5): at most three lessons in a row for every teacher, as a priority.</summary>
    /// <summary>The 1.0.0 defaults: at most 3 lessons in a row (a priority) and every subject spread across days.</summary>
    private static SchedulingInput Production(SchedulingInput input) => input with
    {
        Profile = input.Profile with { DefaultMaxConsecutive = 3 },
        Subjects = input.Subjects.Select(subject => subject with { SpreadAcrossDays = true }).ToArray(),
    };

    /// <summary>
    /// Phase 5 gate with every new rule on (the default priorities, max 3 in a row): 24/40 with 2 workers (the 4 GB school-PC default), the owner-shaped
    /// school (12 sections, 30/30, 22 teachers) on three seeds, through the validator first so its readiness is reported too.
    /// </summary>
    [PerformanceFact]
    public async Task Phase5RulesKeepTheGate()
    {
        foreach (var seed in new[] { 1, 2, 3 })
        {
            var (result, violations) = await Measure($"24/40 s{seed} P5", Production(SyntheticSchools.Realistic(24, 40, seed)), 60, 2, deterministic: false);
            Assert.True(result.HasTimetable);
            Assert.Equal(0, violations);
            Assert.True(result.FirstSolutionSeconds < 60, $"first timetable after {result.FirstSolutionSeconds} s");
        }
        foreach (var seed in new[] { 1, 2, 3 })
        {
            var input = SyntheticSchools.OwnerShaped(seed, spreadAll: true);
            var readiness = PreSolveValidator.Validate(input);
            output.WriteLine($"owner-shaped s{seed}: ready={readiness.Ready} errors={readiness.Errors} warnings={readiness.Warnings} {string.Join(",", readiness.Findings.Where(item => item.Severity == PreSolveValidator.Error).Select(item => item.Code))}");
            var (result, violations) = await Measure($"owner-shaped s{seed}", input, 60, 2, deterministic: false);
            if (readiness.Ready)
            {
                Assert.True(result.HasTimetable);
                Assert.Equal(0, violations);
                var score = TimetableScorer.Score(input, result.Lessons, false);
                output.WriteLine("  " + string.Join(" ", score.Rules.Select(rule => $"{rule.Key}={rule.Penalty}")) + (score.LastPeriods is { } last ? $" last: {last.MostCount}/{last.LeastCount}" : ""));
            }
        }
    }

    /// <summary>
    /// The owner's 1.0.0 analysis (ADR 0055): a fictional 11-section school shaped like his, every subject spread at weight 80, the default time limit
    /// (120 s) with 2 workers. Reports the same-day repeats (unavoidable / improvable), the consecutive-lessons overruns and the memory, per seed.
    /// </summary>
    [PerformanceFact]
    public async Task SpreadingLeavesOnlyUnavoidableRepeats()
    {
        foreach (var seed in new[] { 1, 2, 3 })
        {
            var input = SyntheticSchools.OwnerShaped(seed, sectionCount: 11, spreadAll: true);
            var (result, violations) = await Measure($"owner-shaped 11 s{seed}", input, GenerationSettings.DefaultTimeLimit, 2, deterministic: false);
            Assert.True(result.HasTimetable);
            Assert.Equal(0, violations);
            Assert.True(result.FirstSolutionSeconds < 60, $"first timetable after {result.FirstSolutionSeconds} s");
            var score = TimetableScorer.Score(input, result.Lessons, false);
            var repeats = score.Repeats!;
            var overruns = score.Rules.Single(rule => rule.Key == SchedulingRuleKeys.MaxConsecutiveLessons).Penalty;
            output.WriteLine($"  same-day repeats: {repeats.Total} ({repeats.Unavoidable} unavoidable, {repeats.Improvable} improvable); consecutive overruns: {overruns} "
                + $"({score.ConsecutiveRuns?.Count ?? 0} teacher-days); " + string.Join(" ", score.Rules.Select(rule => $"{rule.Key}={rule.Penalty}")));
            Assert.True(repeats.Improvable <= LimitImprovable, $"{repeats.Improvable} improvable repeats");
        }
    }

    /// <summary>Measured 0, 1 and 1 on 2026-10-17 (docs/PERFORMANCE.md); a margin for slower machines.</summary>
    private const int LimitImprovable = 3;

    /// <summary>«نتيجة قابلة للإعادة» (one worker, deterministic time): measured and reported, no target.</summary>
    [PerformanceFact]
    public async Task TheDeterministicModeIsMeasuredWithoutAPromise()
    {
        await Measure("12/20 det", SyntheticSchools.Realistic(12, 20, seed: 1), 60, 1, deterministic: true);
        await Measure("24/40 det", SyntheticSchools.Realistic(24, 40, seed: 1), 60, 1, deterministic: true);
    }

    /// <summary>Larger sizes are measured and reported honestly, with no target (PHASE_4_MVP).</summary>
    [PerformanceFact]
    public async Task LargerSchoolsAreMeasuredWithoutAPromise()
    {
        await Measure("40/54", SyntheticSchools.Realistic(40, 54, seed: 1), 120, GenerationSettings.DefaultWorkers, deterministic: false);
        await Measure("40/60", SyntheticSchools.Realistic(40, 60, seed: 1), 120, GenerationSettings.DefaultWorkers, deterministic: false);
    }
}
