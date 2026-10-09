using System.Diagnostics;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Infrastructure.Solver;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// Reproducibility, cancellation, timeout and real progress (Phase 4 §4, §7). These measure time, so they run alone,
/// after the parallel tests, without CPU contention from other test classes.
/// </summary>
[Collection(SerialSolverRuns.Name)]
public sealed class SolverExecutionTests
{
    [Fact]
    public async Task TheDeterministicModeRepeatsTheSameTimetable()
    {
        var input = SyntheticSchools.Realistic(sections: 2, teachers: 10, seed: 11);
        var settings = TestSchool.Settings(seconds: 1, seed: 42);
        var first = await SolverTests.Valid(input, settings);
        var second = await SolverTests.Valid(input, settings);
        Assert.Equal(first.Order(LessonOrder.Instance), second.Order(LessonOrder.Instance));

        var otherSeed = await SolverTests.Valid(input, settings with { Seed = 9 });
        Assert.Equal(first.Count, otherSeed.Count);
    }

    [Fact]
    public async Task ProgressComesFromRealSolverEvents()
    {
        var input = SyntheticSchools.Realistic(sections: 4, teachers: 12, seed: 5);
        var events = new List<SolverProgress>();
        var progress = new SynchronousProgress(events.Add);
        var result = await new CpSatSolver().SolveAsync(input, TestSchool.Settings(seconds: 3), progress, CancellationToken.None);
        Assert.True(result.HasTimetable);
        Assert.NotEmpty(events);
        Assert.Equal(Enumerable.Range(1, events.Count), events.Select(item => item.Improvements));
        Assert.All(events, item => Assert.NotNull(item.FirstSolutionSeconds));
        // Improving solutions only: the penalty never goes up.
        Assert.True(events.Zip(events.Skip(1)).All(pair => pair.Second.BestObjective <= pair.First.BestObjective));
        Assert.Equal(result.Improvements, events.Count);
    }

    [Fact]
    public async Task CancellingStopsWithinTwoSecondsAndKeepsTheBestTimetable()
    {
        var input = SyntheticSchools.Realistic(sections: 24, teachers: 40, seed: 1);
        using var source = new CancellationTokenSource();
        var solve = new CpSatSolver().SolveAsync(input, TestSchool.Settings(seconds: 120, deterministic: false, workers: 4), null, source.Token);
        await Task.Delay(TimeSpan.FromSeconds(3));
        var clock = Stopwatch.StartNew();
        await source.CancelAsync();
        var result = await solve;
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"stopped after {clock.Elapsed.TotalSeconds:0.00} s");
        Assert.Equal(SolverStatus.Cancelled, result.Status);
        if (result.Lessons.Count > 0)
            Assert.Empty(TimetableVerifier.Verify(input, result.Lessons, false));
    }

    [Fact]
    public async Task ATimeLimitWithoutASolutionIsTimedOutNotInfeasible()
    {
        // A one-second budget on a large school: either a valid timetable or an honest timeout with advice.
        var input = SyntheticSchools.Realistic(sections: 40, teachers: 60, seed: 2);
        var result = await new CpSatSolver().SolveAsync(input, TestSchool.Settings(seconds: 1, deterministic: false, workers: 1), null, CancellationToken.None);
        Assert.NotEqual(SolverStatus.Infeasible, result.Status);
        if (result.Status == SolverStatus.TimedOut)
        {
            var advice = Assert.Single(result.Diagnostics!.Findings);
            Assert.Equal(DiagnosticCodes.TimeoutNoSolution, advice.Code);
            Assert.Contains("raiseTimeLimit", advice.Fixes);
            Assert.Empty(result.Lessons);
        }
        else
        {
            Assert.Empty(TimetableVerifier.Verify(input, result.Lessons, false));
        }
    }

    [Fact]
    public void TheEngineLoadsAndReportsItsVersion()
    {
        var info = new OrToolsInfo();
        Assert.True(info.Available);
        Assert.StartsWith("OR-Tools 9.15", info.Version, StringComparison.Ordinal);
    }

    [Fact]
    public void SolverParametersFollowTheSettings()
    {
        var deterministic = CpSatSolver.ParametersFor(TestSchool.Settings(seconds: 30, seed: 5, deterministic: true, workers: 8));
        Assert.Contains("max_deterministic_time:30", deterministic, StringComparison.Ordinal);
        Assert.Contains("num_workers:1", deterministic, StringComparison.Ordinal);
        Assert.Contains("random_seed:5", deterministic, StringComparison.Ordinal);
        var fast = CpSatSolver.ParametersFor(TestSchool.Settings(seconds: 60, seed: 9, deterministic: false, workers: 4));
        Assert.Contains("max_time_in_seconds:60", fast, StringComparison.Ordinal);
        Assert.Contains("num_workers:4", fast, StringComparison.Ordinal);
    }

    private sealed class SynchronousProgress(Action<SolverProgress> report) : IProgress<SolverProgress>
    {
        public void Report(SolverProgress value) => report(value);
    }
}

internal sealed class LessonOrder : IComparer<PlacedLesson>
{
    public static readonly LessonOrder Instance = new();

    public int Compare(PlacedLesson? x, PlacedLesson? y) =>
        (x!.SectionId, x.Day, x.Lesson, x.LineId).CompareTo((y!.SectionId, y.Day, y.Lesson, y.LineId));
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SerialSolverRuns
{
    public const string Name = "Serial solver runs";
}
