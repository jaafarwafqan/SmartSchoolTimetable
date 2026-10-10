using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// The soft rules S1–S5 (Phase 4 §3): with one rule enabled, the solver reaches zero penalty where it is possible, and
/// the scorer's total equals the solver's proven-optimal objective (the two definitions agree).
/// </summary>
public sealed class SoftRuleTests
{
    private static async Task<(IReadOnlyList<PlacedLesson> Lessons, TimetableScore Score, SolverResult Result)> Optimise(SchedulingInput input, string mode = GenerationModes.Standard)
    {
        var outcome = await SolverTests.Generate(input, TestSchool.Settings(mode, seconds: 20));
        Assert.Equal(SolverStatus.Optimal, outcome.Result.Status);
        Assert.Empty(outcome.Violations);
        Assert.Equal(outcome.Result.Objective, outcome.Score!.Total);
        return (outcome.Result.Lessons, outcome.Score, outcome.Result);
    }

    private static long Penalty(TimetableScore score, string key) => score.Rules.Single(rule => rule.Key == key).Penalty;

    [Fact]
    public async Task S1SpreadSubjectsLandOnceADay()
    {
        var school = new TestSchool().Rules((SchedulingRuleKeys.SpreadSubjectsAcrossDays, true, 20));
        var shift = school.Shift(2);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 2);
        var spread = school.Line(stage, school.Subject(spread: true), 5);
        school.Assign(section, spread, school.Teacher());
        school.Assign(section, school.Line(stage, school.Subject(), 5), school.Teacher());
        var (lessons, score, _) = await Optimise(school.Build());
        Assert.Equal(0, Penalty(score, SchedulingRuleKeys.SpreadSubjectsAcrossDays));
        Assert.All(lessons.Where(lesson => lesson.LineId == spread).GroupBy(lesson => lesson.Day), day => Assert.Single(day));
    }

    [Fact]
    public async Task S2TeachersGetNoGapsWhenPossible()
    {
        var school = new TestSchool().Rules((SchedulingRuleKeys.AvoidTeacherGaps, true, 30));
        // Ten lessons a week in three-lesson days: the shared teacher could land at lesson 1 in one section and
        // lesson 3 in the other; without gaps the two lessons a day are adjacent.
        var shift = school.Shift(3);
        var stage = school.Stage();
        var shared = school.Teacher();
        var line = school.Line(stage, school.Subject(), 5);
        var filler = school.Line(stage, school.Subject(), 5);
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var section = school.Section(shift, stage, 3);
            school.Assign(section, line, shared);
            school.Assign(section, filler, school.Teacher());
        }
        var (lessons, score, _) = await Optimise(school.Build());
        Assert.Equal(0, Penalty(score, SchedulingRuleKeys.AvoidTeacherGaps));
        foreach (var day in lessons.Where(lesson => lesson.TeacherId == shared).GroupBy(lesson => lesson.Day))
        {
            var used = day.Select(lesson => lesson.Lesson).Order().ToArray();
            Assert.Equal(used.Length, used[^1] - used[0] + 1);
        }
    }

    [Fact]
    public async Task S3HeavySubjectsComeFirst()
    {
        var school = new TestSchool().Rules((SchedulingRuleKeys.HeavySubjectsEarly, true, 15));
        var shift = school.Shift(3);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 3);
        var heavy = school.Line(stage, school.Subject(heavy: true), 5);
        school.Assign(section, heavy, school.Teacher());
        school.Assign(section, school.Line(stage, school.Subject(), 10), school.Teacher());
        var (lessons, score, _) = await Optimise(school.Build());
        Assert.Equal(0, Penalty(score, SchedulingRuleKeys.HeavySubjectsEarly));
        Assert.All(lessons.Where(lesson => lesson.LineId == heavy), lesson => Assert.Equal(1, lesson.Lesson));
    }

    [Fact]
    public async Task S4TheSameSubjectIsNotRepeatedBackToBack()
    {
        var school = new TestSchool().Rules((SchedulingRuleKeys.AvoidSameSubjectRepeated, true, 25));
        var shift = school.Shift(2);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 2);
        school.Assign(section, school.Line(stage, school.Subject(), 5), school.Teacher());
        school.Assign(section, school.Line(stage, school.Subject(), 5), school.Teacher());
        var (lessons, score, _) = await Optimise(school.Build());
        Assert.Equal(0, Penalty(score, SchedulingRuleKeys.AvoidSameSubjectRepeated));
        Assert.All(lessons.GroupBy(lesson => lesson.Day), day => Assert.Equal(2, day.Select(lesson => lesson.LineId).Distinct().Count()));
    }

    [Fact]
    public async Task S5DoubleSubjectsArePreferredAsPairsInTheStandardMode()
    {
        var school = new TestSchool().Rules((SchedulingRuleKeys.KeepDoubleLessonsTogether, true, 10));
        var shift = school.Shift(3);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 3);
        var pairs = school.Line(stage, school.Subject(doublePeriod: true), 4);
        school.Assign(section, pairs, school.Teacher());
        school.Assign(section, school.Line(stage, school.Subject(), 6), school.Teacher());
        var (lessons, score, _) = await Optimise(school.Build());
        Assert.Equal(0, Penalty(score, SchedulingRuleKeys.KeepDoubleLessonsTogether));
        Assert.All(lessons.Where(lesson => lesson.LineId == pairs).GroupBy(lesson => lesson.Day), day => Assert.Equal(2, day.Count()));
    }

    [Fact]
    public async Task AllRulesTogetherGiveAnOptimalScoreEqualToTheBreakdown()
    {
        var input = SyntheticSchools.Realistic(sections: 1, teachers: 10, seed: 3);
        var (_, score, result) = await Optimise(input);
        Assert.Equal(result.Objective, score.Rules.Sum(rule => rule.Weighted));
        Assert.Equal(SchedulingRuleKeys.Defaults.Select(rule => rule.Key), score.Rules.Select(rule => rule.Key));
    }

    [Fact]
    public void ADisabledOrZeroWeightRuleCostsNothing()
    {
        var school = new TestSchool().Rules((SchedulingRuleKeys.HeavySubjectsEarly, false, 15));
        var shift = school.Shift(2);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 2);
        var heavy = school.Line(stage, school.Subject(heavy: true), 1);
        var teacher = school.Teacher();
        school.Assign(section, heavy, teacher);
        var score = TimetableScorer.Score(school.Build(), [new PlacedLesson(section, heavy, teacher, TestSchool.Week[0], 2)], false);
        var rule = score.Rules.Single(item => item.Key == SchedulingRuleKeys.HeavySubjectsEarly);
        Assert.Equal((1L, 0L, 0L), (rule.Penalty, rule.Weighted, score.Total));
    }
}
