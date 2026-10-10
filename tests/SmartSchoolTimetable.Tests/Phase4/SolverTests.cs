using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Infrastructure.Solver;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// One test per hard constraint H1–H10 (Phase 4 §7): the generated timetable passes the independent verifier, and a
/// variant that could only be met by breaking the constraint is proven infeasible by the solver.
/// </summary>
public sealed class SolverTests
{
    internal static async Task<SolverResult> Solve(SchedulingInput input, GenerationSettings? settings = null, CancellationToken token = default) =>
        await new CpSatSolver().SolveAsync(input, settings ?? TestSchool.Settings(), null, token);

    /// <summary>Solves through the engine: validator first, then the solver, then the verifier.</summary>
    internal static async Task<GenerationOutcome> Generate(SchedulingInput input, GenerationSettings? settings = null) =>
        await GenerationEngine.RunAsync(new CpSatSolver(), input, settings ?? TestSchool.Settings(), null, null, CancellationToken.None);

    internal static async Task<IReadOnlyList<PlacedLesson>> Valid(SchedulingInput input, GenerationSettings? settings = null)
    {
        var outcome = await Generate(input, settings);
        Assert.True(outcome.Readiness.Ready, string.Join(", ", outcome.Readiness.Findings.Select(finding => finding.Code)));
        Assert.True(outcome.Result.HasTimetable, outcome.Result.Status.ToString());
        Assert.Empty(outcome.Violations);
        Assert.Empty(TimetableVerifier.Verify(input, outcome.Result.Lessons, settings?.DoublePeriodsRequired ?? false));
        return outcome.Result.Lessons;
    }

    internal static async Task Infeasible(SchedulingInput input, GenerationSettings? settings = null) =>
        Assert.Equal(SolverStatus.Infeasible, (await Solve(input, settings)).Status);

    /// <summary>One section in a fresh shift with one line taught by one teacher.</summary>
    private static (TestSchool School, long Section, long Line, long Teacher, long Subject) OneLine(int lessonsPerDay, int weekly,
        int[]? offDays = null, SlotRef[]? teacherBlocked = null, SlotRef[]? subjectBlocked = null, int? maxPerDay = null, int? maxPerWeek = null)
    {
        var school = new TestSchool();
        var shift = school.Shift(lessonsPerDay);
        var stage = school.Stage();
        var section = school.Section(shift, stage, lessonsPerDay);
        var subject = school.Subject(subjectBlocked);
        var line = school.Line(stage, subject, weekly);
        var teacher = school.Teacher(maxPerWeek, maxPerDay, offDays, teacherBlocked);
        school.Assign(section, line, teacher);
        return (school, section, line, teacher, subject);
    }

    [Fact]
    public async Task H1EveryLineGetsExactlyItsWeeklyLessons()
    {
        var (school, section, line, _, _) = OneLine(2, 7);
        var lessons = await Valid(school.Build());
        Assert.Equal(7, lessons.Count(lesson => lesson.SectionId == section && lesson.LineId == line));
        await Infeasible(OneLine(2, 11).School.Build());
    }

    [Fact]
    public async Task H2ASectionHasOneLessonPerSlot()
    {
        static SchedulingInput Two(int first, int second)
        {
            var school = new TestSchool();
            var shift = school.Shift(2);
            var stage = school.Stage();
            var section = school.Section(shift, stage, 2);
            foreach (var weekly in new[] { first, second })
                school.Assign(section, school.Line(stage, school.Subject(), weekly), school.Teacher());
            return school.Build();
        }
        var lessons = await Valid(Two(5, 5));
        Assert.Equal(10, lessons.Select(lesson => (lesson.Day, lesson.Lesson)).Distinct().Count());
        await Infeasible(Two(6, 5));
    }

    [Fact]
    public async Task H3LessonsFillTheFirstPeriodsAndFreePeriodsFallAtTheEnd()
    {
        var school = new TestSchool();
        var shift = school.Shift(4);
        var stage = school.Stage();
        // The stage has 4, 4, 4, 4 and 2 lessons: the last day never uses lesson 3.
        var section = school.Section(shift, stage, lessonsByDay: [4, 4, 4, 4, 2]);
        school.Assign(section, school.Line(stage, school.Subject(), 6), school.Teacher());
        school.Assign(section, school.Line(stage, school.Subject(), 5), school.Teacher());
        var lessons = await Valid(school.Build());
        foreach (var day in lessons.GroupBy(lesson => lesson.Day))
            Assert.Equal(Enumerable.Range(1, day.Count()), day.Select(lesson => lesson.Lesson).Order());
        Assert.DoesNotContain(lessons, lesson => lesson.Day == TestSchool.Week[4] && lesson.Lesson > 2);

        // The only teacher cannot teach lesson 1: packing makes every later lesson impossible.
        var blocked = TestSchool.Week.Select(day => new SlotRef(day, 1)).ToArray();
        await Infeasible(OneLine(3, 5, teacherBlocked: blocked).School.Build());
    }

    /// <summary>Two shifts overlapping in time: 8:00–9:30 and 8:45–10:15, one teacher in both.</summary>
    private static SchedulingInput CrossShift(int morningLessons)
    {
        var school = new TestSchool();
        var morning = school.Shift(2, start: 480);
        var later = school.Shift(2, start: 525);
        var first = school.Stage();
        var second = school.Stage();
        var a = school.Section(morning, first, 2);
        var b = school.Section(later, second, 2);
        var teacher = school.Teacher();
        school.Assign(a, school.Line(first, school.Subject(), morningLessons), teacher);
        school.Assign(b, school.Line(second, school.Subject(), 5), teacher);
        return school.Build();
    }

    [Fact]
    public async Task H4ATeacherNeverTeachesTwoLessonsAtOverlappingTimesAcrossShifts()
    {
        var lessons = await Valid(CrossShift(5));
        Assert.Equal(10, lessons.Count);
        // Morning lesson 2 (8:45) and later lesson 1 (8:45) overlap: the morning section cannot fill both lessons a day.
        await Infeasible(CrossShift(10));

        var school = new TestSchool();
        var shift = school.Shift(3);
        var stage = school.Stage();
        var teacher = school.Teacher();
        foreach (var _ in Enumerable.Range(0, 2))
            school.Assign(school.Section(shift, stage, 3), school.Line(stage, school.Subject(), 8), teacher);
        await Infeasible(school.Build());
    }

    [Fact]
    public async Task H5OffDaysAndBlockedPeriodsAreNeverUsed()
    {
        var off = TestSchool.Week[0];
        var blocked = new SlotRef(TestSchool.Week[1], 2);
        var (school, _, _, _, _) = OneLine(3, 7, offDays: [off], teacherBlocked: [blocked]);
        var lessons = await Valid(school.Build());
        Assert.DoesNotContain(lessons, lesson => lesson.Day == off);
        Assert.DoesNotContain(lessons, lesson => lesson.Day == blocked.Day && lesson.Lesson == blocked.Lesson);
        await Infeasible(OneLine(2, 10, offDays: [off]).School.Build());
    }

    [Fact]
    public async Task H6SubjectBlockedPeriodsAreNeverUsed()
    {
        var blocked = new SlotRef(TestSchool.Week[2], 1);
        var (school, _, _, _, _) = OneLine(3, 7, subjectBlocked: [blocked, new SlotRef(TestSchool.Week[3], 3)]);
        var lessons = await Valid(school.Build());
        Assert.DoesNotContain(lessons, lesson => lesson.Day == blocked.Day && lesson.Lesson == blocked.Lesson);
        await Infeasible(OneLine(2, 10, subjectBlocked: [blocked]).School.Build());
    }

    [Fact]
    public async Task H7TeacherDailyAndWeeklyLimitsHold()
    {
        var lessons = await Valid(OneLine(3, 5, maxPerDay: 1).School.Build());
        Assert.All(lessons.GroupBy(lesson => lesson.Day), day => Assert.Single(day));
        await Infeasible(OneLine(3, 6, maxPerDay: 1).School.Build());
        await Infeasible(OneLine(3, 6, maxPerWeek: 5).School.Build());
    }

    /// <summary>Two sections sharing a laboratory subject, each also with another subject so they can take turns.</summary>
    private static SchedulingInput Lab(int capacity, int lessonsPerDay, int labWeekly, int otherWeekly)
    {
        var school = new TestSchool();
        var shift = school.Shift(lessonsPerDay);
        var stage = school.Stage();
        var lab = school.Resource(capacity);
        var line = school.Line(stage, school.Subject(resource: lab), labWeekly);
        var other = otherWeekly > 0 ? school.Line(stage, school.Subject(), otherWeekly) : (long?)null;
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var section = school.Section(shift, stage, lessonsPerDay);
            school.Assign(section, line, school.Teacher());
            if (other is { } id)
                school.Assign(section, id, school.Teacher());
        }
        return school.Build();
    }

    [Fact]
    public async Task H8ResourceCapacityIsNeverExceeded()
    {
        var input = Lab(1, 3, 5, 5);
        var labLine = input.Lines.Single(line => input.Subjects.Single(subject => subject.Id == line.SubjectId).RequiredResourceId is not null).Id;
        var lessons = await Valid(input);
        Assert.All(lessons.Where(lesson => lesson.LineId == labLine).GroupBy(lesson => (lesson.Day, lesson.Lesson)), slot => Assert.Single(slot));
        Assert.NotEmpty(await Valid(Lab(2, 2, 10, 0)));
        await Infeasible(Lab(1, 2, 10, 0));
    }

    [Fact]
    public async Task H9ASubjectHasAtMostTheDailyCapInASection()
    {
        // Four lessons over five days: at most max(2, ceil(4 ÷ 5)) = 2 a day.
        var free = TestSchool.Week.Take(2).ToArray();
        var offDays = TestSchool.Week.Except(free).ToArray();
        var lessons = await Valid(OneLine(3, 4, offDays: offDays).School.Build());
        Assert.All(lessons.GroupBy(lesson => lesson.Day), day => Assert.Equal(2, day.Count()));
        await Infeasible(OneLine(4, 4, offDays: TestSchool.Week.Skip(1).ToArray()).School.Build());
    }

    private static SchedulingInput Doubles(int? breakAfter)
    {
        var school = new TestSchool();
        var shift = school.Shift(3, breakAfter: breakAfter);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 3);
        school.Assign(section, school.Line(stage, school.Subject(doublePeriod: true), 5), school.Teacher());
        school.Assign(section, school.Line(stage, school.Subject(), 5), school.Teacher());
        return school.Build();
    }

    [Fact]
    public async Task H10DoubleLinesArePlacedAsAdjacentPairsInTheDoubleMode()
    {
        var settings = TestSchool.Settings(GenerationModes.DoublePeriods);
        var input = Doubles(breakAfter: null);
        var lessons = await Valid(input, settings);
        var doubleLine = input.Lines.Single(line => input.Subjects.Single(subject => subject.Id == line.SubjectId).RequiresDoublePeriod).Id;
        var pairs = lessons.Where(lesson => lesson.LineId == doubleLine).GroupBy(lesson => lesson.Day)
            .Count(day => day.Count() == 2 && Math.Abs(day.First().Lesson - day.Last().Lesson) == 1);
        Assert.Equal(2, pairs);

        // A break after every lesson 1 and 2 leaves no adjacent pair: impossible in the double mode, fine in the standard one.
        var school = new TestSchool();
        var shift = school.Shift(2, breakAfter: 1);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 2);
        school.Assign(section, school.Line(stage, school.Subject(doublePeriod: true), 4), school.Teacher());
        var split = school.Build();
        await Infeasible(split, settings);
        Assert.NotEmpty(await Valid(split));
    }

    [Fact]
    public async Task ShortTimeAndValidatorErrorsAreReportedHonestly()
    {
        var blocked = await Generate(OneLine(2, 11).School.Build());
        Assert.True(blocked.Blocked);
        Assert.Equal(Application.ErrorCodes.GenerationNotReady, blocked.Result.ErrorCode);
        Assert.Empty(blocked.Result.Lessons);
    }
}
