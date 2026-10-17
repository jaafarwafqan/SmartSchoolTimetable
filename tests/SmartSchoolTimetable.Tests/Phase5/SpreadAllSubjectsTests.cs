using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Infrastructure;
using SmartSchoolTimetable.Infrastructure.Solver;
using SmartSchoolTimetable.Tests.Phase4;
using Xunit.Abstractions;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>
/// «توزيع حصص المادة على أيام الأسبوع» for every subject (owner's analysis before 1.0.0): the fair share per day from the days the teacher is there,
/// the S1 penalty, the split of same-day repeats into unavoidable and improvable, and the corrective migration. Synthetic schools only.
/// </summary>
[Collection(SerialSolverRuns.Name)]
public sealed class SpreadAllSubjectsTests(ITestOutputHelper output)
{
    private static readonly int[] Week = TestSchool.Week;

    private static long Penalty(TimetableScore score, string key) => score.Rules.Single(rule => rule.Key == key).Penalty;

    private static TestSchool SpreadOnly() => new TestSchool().Rules((SchedulingRuleKeys.SpreadSubjectsAcrossDays, true, 40));

    [Fact]
    public void TheShareComesFromTheDaysTheTeacherIsThere()
    {
        var school = SpreadOnly();
        var shift = school.Shift(6);
        var stage = school.Stage();
        var section = school.Section(shift, stage);
        var math = school.Subject(spread: true);
        var science = school.Subject(spread: true);
        var lab = school.Subject(spread: true, doublePeriod: true);
        var art = school.Subject(spread: false);
        school.Assign(section, school.Line(stage, math, 6), school.Teacher(offDays: [Week[0]]));
        school.Assign(section, school.Line(stage, science, 3), school.Teacher());
        school.Assign(section, school.Line(stage, lab, 4, doublePeriod: true), school.Teacher());
        school.Assign(section, school.Line(stage, art, 2), school.Teacher());
        var shares = SubjectSpread.Of(school.Build());

        var first = shares[(section, math)];
        Assert.Equal((6, 1, 4, 5, 2, 2), (first.Weekly, first.Base, first.Days, first.SectionDays, first.Share, first.Unavoidable));
        Assert.Equal(SpreadReasons.TeacherAbsent, first.Reason);
        Assert.Equal(3, first.Penalty(3)); // two repeats, and one lesson above the fair share of two
        Assert.Equal(1, first.Penalty(2));
        var second = shares[(section, science)];
        Assert.Equal((5, 1, 0), (second.Days, second.Share, second.Unavoidable));
        var third = shares[(section, lab)];
        Assert.Equal((2, 2, 0), (third.Base, third.Share, third.Unavoidable)); // a double lesson is one sitting
        Assert.False(shares.ContainsKey((section, art))); // the subject's own switch turns the rule off
    }

    [Fact]
    public void MoreLessonsThanDaysIsAnUnavoidableRepeatWithItsReason()
    {
        var school = SpreadOnly();
        var shift = school.Shift(7);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 7);
        var subject = school.Subject(spread: true);
        var line = school.Line(stage, subject, 7);
        var teacher = school.Teacher();
        school.Assign(section, line, teacher);
        var input = school.Build();
        var share = SubjectSpread.Of(input)[(section, subject)];
        Assert.Equal((5, 2, 2, SpreadReasons.MoreLessonsThanDays), (share.Days, share.Share, share.Unavoidable, share.Reason));

        // Four lessons on the first day, one on each of three others, none on the last: three repeats, two of them unavoidable; two lessons above the share.
        var lessons = new[] { (Week[0], 1), (Week[0], 2), (Week[0], 3), (Week[0], 4), (Week[1], 1), (Week[2], 1), (Week[3], 1) }
            .Select(slot => new PlacedLesson(section, line, teacher, slot.Item1, slot.Item2)).ToArray();
        var score = TimetableScorer.Score(input, lessons, false);
        Assert.Equal(5, Penalty(score, SchedulingRuleKeys.SpreadSubjectsAcrossDays));
        Assert.Equal(new RepeatInsight(3, 2, 1, score.Repeats!.Forced), score.Repeats);
        var forced = Assert.Single(score.Repeats.Forced);
        Assert.Equal((SpreadReasons.MoreLessonsThanDays, 7, 5, 2, 1), (forced.Reason, forced.Weekly, forced.Days, forced.Count, forced.Sections));
    }

    [Fact]
    public async Task TheSolverSpreadsEverySubjectAndAgreesWithTheScorer()
    {
        // Six lessons with a teacher off on one day: two days get two lessons, never one day three. A plain subject of four gets four days.
        var school = SpreadOnly();
        var shift = school.Shift(4);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 4);
        var six = school.Subject(spread: true);
        var four = school.Subject(spread: true);
        var sixLine = school.Line(stage, six, 6);
        var fourLine = school.Line(stage, four, 4);
        school.Assign(section, sixLine, school.Teacher(offDays: [Week[2]]));
        school.Assign(section, fourLine, school.Teacher());
        school.Assign(section, school.Line(stage, school.Subject(spread: false, dailyCap: 4), 10), school.Teacher()); // switched off, no daily limit: it fills the rest
        var input = school.Build();
        var outcome = await SolverTests.Generate(input, TestSchool.Settings(seconds: 20));
        Assert.Equal(SolverStatus.Optimal, outcome.Result.Status);
        Assert.Equal(outcome.Result.Objective, outcome.Score!.Total);
        var perDay = outcome.Result.Lessons.Where(lesson => lesson.LineId == sixLine).GroupBy(lesson => lesson.Day).Select(day => day.Count()).Order().ToArray();
        Assert.Equal([1, 1, 2, 2], perDay);
        Assert.All(outcome.Result.Lessons.Where(lesson => lesson.LineId == fourLine).GroupBy(lesson => lesson.Day), day => Assert.Single(day));
        Assert.Equal(0, outcome.Score.Repeats!.Improvable);
    }

    /// <summary>
    /// The owner's case, made up (no real names): 11 sections, 30 lessons in 30 periods, teachers with a day off. Before the fix (no subject spread) the
    /// solver piled lessons freely; with it the improvable same-day repeats mostly go, and the unavoidable ones are reported with their reason. Here with a
    /// 30-second limit (the test suite's budget); the performance gate checks the owner's 60 seconds on three seeds (PerformanceTests.Phase5RulesKeepTheGate).
    /// </summary>
    [Fact]
    public async Task AnOwnerShapedSchoolKeepsOnlyTheUnavoidableRepeats()
    {
        var input = SyntheticSchools.OwnerShaped(1, sectionCount: 11, spreadAll: true);
        var before = await new CpSatSolver().SolveAsync(input with { Subjects = input.Subjects.Select(subject => subject with { SpreadAcrossDays = false }).ToArray() },
            new GenerationSettings(GenerationModes.Standard, 10, 12345, 2, false), null, CancellationToken.None);
        var after = await new CpSatSolver().SolveAsync(input, new GenerationSettings(GenerationModes.Standard, 30, 12345, 2, false), null, CancellationToken.None);
        Assert.True(before.HasTimetable && after.HasTimetable);
        Assert.Empty(TimetableVerifier.Verify(input, after.Lessons, false));
        var old = TimetableScorer.Score(input, before.Lessons, false).Repeats!;
        var score = TimetableScorer.Score(input, after.Lessons, false);
        var now = score.Repeats!;
        output.WriteLine($"before: {old.Total} repeats, {old.Unavoidable} unavoidable, {old.Improvable} improvable; after: {now.Total}, {now.Unavoidable}, {now.Improvable}");
        Assert.True(now.Improvable <= 10 && now.Improvable * 3 <= old.Improvable, $"before {old.Improvable}, after {now.Improvable} improvable repeats");
        Assert.Equal(old.Unavoidable, now.Unavoidable); // what cannot be avoided does not depend on the timetable
        Assert.Contains(now.Forced, item => item.Reason == SpreadReasons.MoreLessonsThanDays && item.Weekly == 6 && item.Days == 5);
        Assert.Contains(now.Forced, item => item.Reason == SpreadReasons.TeacherAbsent && item.Days == 4);
        Assert.All(now.Forced, item => Assert.False(string.IsNullOrWhiteSpace(item.Teacher) || string.IsNullOrWhiteSpace(item.Subject)));
        Assert.Equal(now.Unavoidable, now.Forced.Sum(item => item.Count));
        Assert.True(score.LastPeriods!.FullWeek); // 30 lessons in 30 periods: every section has a last lesson every day
        Assert.Equal(11 * 5, score.LastPeriods.LastLessons);
    }

    /// <summary>With only this priority on, the same school reaches no improvable repeat at all: what remains with every priority on is a trade-off.</summary>
    [Fact]
    public async Task AloneTheRuleRemovesEveryImprovableRepeat()
    {
        var input = SyntheticSchools.OwnerShaped(1, sectionCount: 11, spreadAll: true);
        input = input with { Profile = input.Profile with { Rules = input.Profile.Rules.Select(rule => rule.Key == SchedulingRuleKeys.SpreadSubjectsAcrossDays ? rule : rule with { Enabled = false }).ToArray() } };
        var result = await new CpSatSolver().SolveAsync(input, new GenerationSettings(GenerationModes.Standard, 30, 12345, 2, false), null, CancellationToken.None);
        Assert.True(result.HasTimetable);
        var repeats = TimetableScorer.Score(input, result.Lessons, false).Repeats!;
        Assert.Equal((0, repeats.Unavoidable), (repeats.Improvable, repeats.Total));
    }

    [Theory]
    [InlineData(20, 40)] // the old default becomes «مهم جداً»
    [InlineData(30, 30)] // a weight the owner chose is kept
    public async Task TheMigrationSpreadsEveryExistingSubject(int weightBefore, int weightAfter)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"smart-school-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite($"Data Source={Path.Combine(directory, "old.db")};Pooling=False").Options;
            await using (var old = new LocalDbContext(options))
            {
                var previous = old.Database.GetMigrations().Last(name => name.EndsWith("_Phase5ConsecutivePriority", StringComparison.Ordinal));
                await old.GetService<IMigrator>().MigrateAsync(previous);
                await old.Database.ExecuteSqlRawAsync("INSERT INTO Subjects (Name, NormalizedName, ColorIndex, Priority, DistributionEnabled, SpreadAcrossDays, Heavy, RequiresDoublePeriod, IsArchived, Version) VALUES ('مادة أ', 'مادة أ', 1, 3, 1, 0, 0, 0, 0, 1)");
                await old.Database.ExecuteSqlRawAsync("INSERT INTO Subjects (Name, NormalizedName, ColorIndex, Priority, DistributionEnabled, SpreadAcrossDays, Heavy, RequiresDoublePeriod, IsArchived, Version) VALUES ('مادة ب', 'مادة ب', 2, 3, 1, 1, 0, 0, 0, 1)");
                await old.Database.ExecuteSqlRawAsync("INSERT INTO SchedulingProfile (Id, ProfileVersion, Version, DefaultMaxConsecutive, BalanceHard, ConsecutiveHard) VALUES (1, 6, 6, 3, 0, 0)");
                await old.Database.ExecuteSqlAsync($"INSERT INTO SchedulingProfileRules (ProfileId, Key, Enabled, Weight) VALUES (1, 'spreadSubjectsAcrossDays', 1, {weightBefore})");
            }
            await using var upgraded = new LocalDbContext(options);
            await upgraded.Database.MigrateAsync();
            Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync());
            var subjects = await upgraded.Set<Subject>().AsNoTracking().OrderBy(subject => subject.Id).ToListAsync();
            Assert.All(subjects, subject => Assert.True(subject.SpreadAcrossDays));
            Assert.Equal([2, 1], subjects.Select(subject => subject.Version)); // only the changed subject gets a new version
            var profile = await upgraded.Set<SchedulingProfile>().AsNoTracking().SingleAsync();
            Assert.Equal(7, profile.ProfileVersion);
            Assert.Equal(weightAfter, profile.Rules.Single(rule => rule.Key == SchedulingRuleKeys.SpreadSubjectsAcrossDays).Weight);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
