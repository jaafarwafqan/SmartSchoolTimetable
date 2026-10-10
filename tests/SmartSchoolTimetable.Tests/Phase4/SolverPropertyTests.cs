using FsCheck;
using FsCheck.Xunit;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// Property-based completeness and soundness of the solver (Phase 4 §7, FsCheck ADR 0033). A random VALID timetable is
/// built first (one or two shifts that may overlap in time, packed days, daily subject caps, teachers reused only
/// without clashes) and the input is derived from it with restrictions it satisfies. The school can be timetabled, so
/// (1) the solver must find a timetable and the independent verifier must report zero violations; (2) under random
/// settings (mode, seed, workers) every timetable it returns has zero violations.
/// </summary>
public sealed class SolverPropertyTests
{
    public const int Runs = 20;

    [Fact]
    public void TheGeneratorsWitnessTimetablesAreValid()
    {
        foreach (var seed in Enumerable.Range(1, 60))
        {
            var (input, witness) = SyntheticSchools.FromRandomTimetable(seed);
            Assert.Empty(TimetableVerifier.Verify(input, witness, doublePeriodsRequired: false));
            Assert.True(PreSolveValidator.Validate(input).Ready, $"seed {seed}");
        }
    }

    [Property(MaxTest = Runs)]
    public bool ASchoolThatCanBeTimetabledIsTimetabledWithZeroViolations(PositiveInt seed)
    {
        // Completeness is about finding a timetable: without soft rules the search ends at the first one.
        var (school, _) = SyntheticSchools.FromRandomTimetable(seed.Get);
        var input = school with { Profile = new ProfileInput(1, school.Profile.Rules.Select(rule => rule with { Enabled = false }).ToArray()) };
        var outcome = SolverTests.Generate(input, TestSchool.Settings(seconds: 30)).GetAwaiter().GetResult();
        return outcome.Result.HasTimetable && outcome.Violations.Count == 0
            && TimetableVerifier.Verify(input, outcome.Result.Lessons, false).Count == 0;
    }

    [Property(MaxTest = Runs)]
    public bool EveryTimetableUnderRandomSettingsHasZeroViolations(PositiveInt seed, bool doubles, bool deterministic, PositiveInt workers)
    {
        var (input, _) = SyntheticSchools.FromRandomTimetable(seed.Get);
        var settings = new GenerationSettings(doubles ? GenerationModes.DoublePeriods : GenerationModes.Standard, 2, seed.Get, 1 + workers.Get % 4, deterministic);
        var result = new Infrastructure.Solver.CpSatSolver().SolveAsync(input, settings, null, CancellationToken.None).GetAwaiter().GetResult();
        return !result.HasTimetable || TimetableVerifier.Verify(input, result.Lessons, settings.DoublePeriodsRequired).Count == 0;
    }
}
