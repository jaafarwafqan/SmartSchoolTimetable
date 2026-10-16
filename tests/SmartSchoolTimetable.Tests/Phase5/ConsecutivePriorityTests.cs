using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Tests.Phase4;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>
/// The owner's follow-up decision (ADR 0053 §2): «أقصى حصص متتالية للمعلم» is a priority («مهم جداً») by default, made hard by the school or by one
/// teacher; the result notes name every teacher over the limit; a hard run that times out is retried once without it and the owner is told why.
/// </summary>
[Collection(SerialSolverRuns.Name)]
public sealed class ConsecutivePriorityTests
{
    private const int Sunday = 7;
    private const int Monday = 1;
    private static readonly int[] Week = [7, 1, 2, 3, 4];

    private static PlacedLesson At(long section, long line, long teacher, int day, int lesson) => new(section, line, teacher, day, lesson);

    private static long Penalty(TimetableScore score, string key) => score.Rules.Single(rule => rule.Key == key).Penalty;

    [Fact]
    public void TheDefaultIsAVeryImportantPriorityAndTheSchoolCanMakeItHard()
    {
        var profile = SchedulingProfile.CreateDefault();
        Assert.Equal(new SchedulingRule(SchedulingRuleKeys.MaxConsecutiveLessons, true, 40), profile.Rules.Single(rule => rule.Key == SchedulingRuleKeys.MaxConsecutiveLessons));
        Assert.False(profile.ConsecutiveHard);
        Assert.True(profile.Update(profile.Rules.ToArray(), consecutiveHard: true));
        Assert.True(profile.ConsecutiveHard);
        Assert.True(profile.RestoreDefaults());
        Assert.False(profile.ConsecutiveHard);
    }

    private static (SchedulingInput Input, long Teacher) OneTeacherFourADay(bool hard)
    {
        // One section of four lessons a day taught by one teacher: four in a row every day.
        var school = new TestSchool().Rules((SchedulingRuleKeys.MaxConsecutiveLessons, true, 40)).Consecutive(3, hard);
        var shift = school.Shift(4);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 4);
        var teacher = school.Teacher();
        school.Assign(section, school.Line(stage, school.Subject(), 20), teacher);
        return (school.Build(), teacher);
    }

    [Fact]
    public async Task AsAPriorityTheLimitBendsWhenItMustAndTheNotesNameTheTeacherAndTheDays()
    {
        var (input, teacher) = OneTeacherFourADay(hard: false);
        var warning = Assert.Single(PreSolveValidator.Validate(input).Findings, finding => finding.Code == FindingCodes.TeacherConsecutiveShort);
        Assert.Equal(PreSolveValidator.Warning, warning.Severity); // a priority never blocks generation
        var outcome = await SolverTests.Generate(input, TestSchool.Settings(seconds: 20));
        Assert.True(outcome.Result.HasTimetable);
        Assert.Empty(outcome.Violations);
        Assert.Equal(outcome.Result.Objective, outcome.Score!.Total);
        Assert.Equal(5, Penalty(outcome.Score, SchedulingRuleKeys.MaxConsecutiveLessons)); // one window of four per day
        var runs = outcome.Score.ConsecutiveRuns!;
        Assert.All(runs, run => Assert.Equal(($"معلم {teacher}", 4, 3), (run.Teacher, run.Count, run.Limit)));
        Assert.Equal(Week, runs.Select(run => run.Day)); // in week order

        // Hard, the same school is impossible, and the validator says so as an error.
        var (hardInput, _) = OneTeacherFourADay(hard: true);
        Assert.Equal(PreSolveValidator.Error, Assert.Single(PreSolveValidator.Validate(hardInput).Findings, finding => finding.Code == FindingCodes.TeacherConsecutiveShort).Severity);
        Assert.Equal(SolverStatus.Infeasible, (await SolverTests.Solve(hardInput)).Status);
    }

    [Fact]
    public async Task AsAPriorityTheSolverAvoidsRunsWhenItCan()
    {
        // 20 lessons for one teacher in two 6-lesson sections, limit 2 as a priority: no run of three.
        var school = new TestSchool().Rules((SchedulingRuleKeys.MaxConsecutiveLessons, true, 40)).Consecutive(2, hard: false);
        var shift = school.Shift();
        var stage = school.Stage();
        var shared = school.Teacher();
        var line = school.Line(stage, school.Subject(), 10);
        var filler = school.Line(stage, school.Subject(), 20);
        foreach (var section in new[] { school.Section(shift, stage), school.Section(shift, stage) })
        {
            school.Assign(section, line, shared);
            school.Assign(section, filler, school.Teacher());
        }
        var outcome = await SolverTests.Generate(school.Build(), TestSchool.Settings(seconds: 20));
        Assert.Equal(SolverStatus.Optimal, outcome.Result.Status);
        Assert.Equal(0, Penalty(outcome.Score!, SchedulingRuleKeys.MaxConsecutiveLessons));
        Assert.Null(outcome.Score!.ConsecutiveRuns);
    }

    [Fact]
    public void OneTeachersOwnHardLimitBindsOnlyHim()
    {
        var school = new TestSchool().Rules().Consecutive(3, hard: false);
        var shift = school.Shift();
        var stage = school.Stage();
        var section = school.Section(shift, stage);
        var line = school.Line(stage, school.Subject(), 6);
        var other = school.Line(stage, school.Subject(), 6);
        var hardOne = school.Teacher(consecutiveHard: true);
        var softOne = school.Teacher();
        school.Assign(section, line, hardOne);
        school.Assign(section, other, softOne);
        var input = school.Build();
        PlacedLesson[] Four(long lineId, long teacher, int day) => Enumerable.Range(1, 4).Select(number => At(section, lineId, teacher, day, number)).ToArray();
        var violations = TimetableVerifier.Verify(input, [.. Four(line, hardOne, Sunday), .. Four(other, softOne, Monday)], false);
        Assert.Contains(violations, violation => violation.Code == ViolationCodes.TeacherConsecutive && violation.TeacherId == hardOne);
        Assert.DoesNotContain(violations, violation => violation.Code == ViolationCodes.TeacherConsecutive && violation.TeacherId == softOne);
        Assert.Equal(hardOne, Assert.Single(input.Teachers, teacher => input.HardConsecutiveOf(teacher) is not null).Id);
        Assert.False(input.WithSoftConsecutive().AnyHardConsecutive);
    }

    /// <summary>A solver stub: times out on a hard-consecutive input, finds a timetable once the rule is a priority.</summary>
    private sealed class TimesOutWhileHard(IReadOnlyList<PlacedLesson> lessons) : ISolver
    {
        public List<bool> Calls { get; } = [];

        public Task<SolverResult> SolveAsync(SchedulingInput input, GenerationSettings settings, IProgress<SolverProgress>? progress, CancellationToken token)
        {
            Calls.Add(input.AnyHardConsecutive);
            var timeout = new SolverFinding(DiagnosticCodes.TimeoutNoSolution, new FindingEntity(FindingEntities.School, 0, string.Empty), [], settings.TimeLimitSeconds, null, [], null);
            return Task.FromResult(input.AnyHardConsecutive
                ? new SolverResult(SolverStatus.TimedOut, [], null, null, 0, null, settings.TimeLimitSeconds, new SolverDiagnostics([timeout], false))
                : new SolverResult(SolverStatus.Feasible, lessons, 1, 0, 1, 0.1, 1, null));
        }

        public string Describe(GenerationSettings settings) => "stub";
    }

    [Fact]
    public async Task AHardRunThatTimesOutIsRetriedOnceWithoutTheRuleAndTheOwnerIsToldItIsTheLikelyCause()
    {
        var school = new TestSchool().Rules().Consecutive(3, hard: false);
        var shift = school.Shift(4);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 4);
        var teacher = school.Teacher(consecutiveHard: true);
        var line = school.Line(stage, school.Subject(), 4);
        school.Assign(section, line, teacher);
        var input = school.Build();
        var stub = new TimesOutWhileHard(Enumerable.Range(1, 4).Select(number => At(section, line, teacher, Sunday, number)).ToArray());
        var outcome = await GenerationEngine.RunAsync(stub, input, TestSchool.Settings(), null, null, CancellationToken.None);
        Assert.Equal([true, false], stub.Calls); // once as the owner set it, once as a priority
        Assert.Equal(SolverStatus.TimedOut, outcome.Result.Status);
        Assert.False(outcome.Result.HasTimetable); // nothing is saved: it would break the rule the owner made hard
        var cause = Assert.Single(outcome.Result.Diagnostics!.Findings, finding => finding.Code == DiagnosticCodes.ConsecutiveLikelyCause);
        Assert.Equal(teacher, Assert.Single(cause.Related).Id);
        Assert.Contains("relaxConsecutive", cause.Fixes);
        Assert.Contains(outcome.Result.Diagnostics.Findings, finding => finding.Code == DiagnosticCodes.TimeoutNoSolution);

        // Without a hard teacher there is no retry.
        var soft = new TimesOutWhileHard([]);
        await GenerationEngine.RunAsync(soft, input.WithSoftConsecutive(), TestSchool.Settings(), null, null, CancellationToken.None);
        Assert.Equal([false], soft.Calls);
    }
}
