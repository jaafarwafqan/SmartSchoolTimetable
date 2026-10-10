using System.Text.Json;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// R3 daily sessions (دوام مزدوج), domain and scheduler side: every session has the same number of lessons, every
/// working day is mapped once per semester, «اعكس للفصل الثاني» flips the mapping, and a break in ANY session splits
/// a double lesson in the validator, the solver, the verifier and the scorer alike. A single session changes nothing.
/// </summary>
public sealed class SessionPlanTests
{
    private static readonly int[] Week = [7, 1, 2, 3, 4];

    private static PeriodDraft[] Timing(int lessons, int start, int minutes = 40, int? breakAfter = null, int breakMinutes = 10)
    {
        var rows = new List<PeriodDraft>();
        var time = new TimeOnly(0, 0).AddMinutes(start);
        for (var lesson = 1; lesson <= lessons; lesson++)
        {
            rows.Add(new PeriodDraft(PeriodKind.Lesson, time, time.AddMinutes(minutes)));
            time = time.AddMinutes(minutes);
            if (breakAfter == lesson)
            {
                rows.Add(new PeriodDraft(PeriodKind.Break, time, time.AddMinutes(breakMinutes)));
                time = time.AddMinutes(breakMinutes);
            }
        }
        return rows.ToArray();
    }

    /// <summary>The owner's example: S1 = Sunday and Monday morning, Tuesday to Thursday evening; S2 reversed.</summary>
    private static SessionDay[] OwnerExample()
    {
        var first = Week.Select(day => new SessionDay(1, day, day is 7 or 1 ? SessionKind.Morning : SessionKind.Evening)).ToArray();
        return [.. first, .. SessionPlan.Reversed(first, 2)];
    }

    private static SessionPlan Plan(PeriodDraft[] evening, IReadOnlyCollection<SessionDay> days, int lessonCount = 6)
    {
        var plan = SessionPlan.Create(1, 1);
        plan.Replace(SessionSystem.TwoSessions, new Dictionary<SessionKind, IReadOnlyList<PeriodDraft>> { [SessionKind.Evening] = evening }, days, lessonCount, Week);
        return plan;
    }

    [Fact]
    public void TheDayMappingIsPerSemesterAndReversesInOneStep()
    {
        var plan = Plan(Timing(6, 13 * 60), OwnerExample());
        Assert.Equal(SessionKind.Morning, plan.SessionOn(1, 7));
        Assert.Equal(SessionKind.Morning, plan.SessionOn(1, 1));
        Assert.Equal(SessionKind.Evening, plan.SessionOn(1, 2));
        Assert.Equal(SessionKind.Evening, plan.SessionOn(2, 7));
        Assert.Equal(SessionKind.Morning, plan.SessionOn(2, 4));
        Assert.Equal(10, plan.Days.Count);
        Assert.All(Week, day => Assert.NotEqual(plan.SessionOn(1, day), plan.SessionOn(2, day)));
    }

    [Fact]
    public void TheCurrentSemesterComesFromTheTermDatesWhenTheyTellIt()
    {
        var first = (new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 15));
        var second = (new DateOnly(2027, 2, 1), new DateOnly(2027, 6, 30));
        Assert.Equal(1, SessionPlan.SemesterOn([second, first], new DateOnly(2026, 10, 3)));
        Assert.Equal(1, SessionPlan.SemesterOn([first, second], new DateOnly(2027, 1, 15)));
        Assert.Equal(2, SessionPlan.SemesterOn([first, second], new DateOnly(2027, 2, 1)));
        Assert.Null(SessionPlan.SemesterOn([first, second], new DateOnly(2027, 1, 20)));
        Assert.Null(SessionPlan.SemesterOn([first, second], new DateOnly(2027, 8, 1)));
        Assert.Null(SessionPlan.SemesterOn([], new DateOnly(2026, 10, 3)));
        var third = (new DateOnly(2027, 7, 1), new DateOnly(2027, 8, 30));
        Assert.Null(SessionPlan.SemesterOn([first, second, third], new DateOnly(2027, 8, 1)));
    }

    [Fact]
    public void SessionsWithADifferentLessonCountAreRefused()
    {
        var error = Assert.Throws<DomainValidationException>(() => Plan(Timing(5, 13 * 60), OwnerExample()));
        Assert.Contains(error.Errors, item => item.Code == DomainErrorCode.SessionLessonCountMismatch && item.Field == "Periods");
    }

    [Fact]
    public void EveryWorkingDayIsMappedOncePerSemesterToASessionOfTheSystem()
    {
        var days = OwnerExample();
        var missing = Assert.Throws<DomainValidationException>(() => Plan(Timing(6, 780), days.Where(day => !(day.Term == 2 && day.Day == 4)).ToArray()));
        Assert.Contains(missing.Errors, item => item.Field == "Days" && item.Code == DomainErrorCode.Required);
        var twice = Assert.Throws<DomainValidationException>(() => Plan(Timing(6, 780), [.. days, new SessionDay(1, 7, SessionKind.Evening)]));
        Assert.Contains(twice.Errors, item => item.Field == "Days" && item.Code == DomainErrorCode.Duplicate);
        var noon = Assert.Throws<DomainValidationException>(() => Plan(Timing(6, 780), days.Select(day => day.Day == 2 ? day with { Session = SessionKind.Noon } : day).ToArray()));
        Assert.Contains(noon.Errors, item => item.Field == "Days" && item.Code == DomainErrorCode.InvalidOption);
        var notWorking = Assert.Throws<DomainValidationException>(() => Plan(Timing(6, 780), [.. days, new SessionDay(1, 5, SessionKind.Morning)]));
        Assert.Contains(notWorking.Errors, item => item.Field == "Days" && item.Code == DomainErrorCode.InvalidOption);
        var overlapping = Assert.Throws<DomainValidationException>(() => Plan(
            [new PeriodDraft(PeriodKind.Lesson, new TimeOnly(13, 0), new TimeOnly(13, 40)), new PeriodDraft(PeriodKind.Lesson, new TimeOnly(13, 30), new TimeOnly(14, 10))],
            days, lessonCount: 2));
        Assert.Contains(overlapping.Errors, item => item.Code == DomainErrorCode.PeriodsOverlap);
    }

    [Fact]
    public void TheModelAllowsThreeSessionsAndOneSessionClearsThePlan()
    {
        var plan = SessionPlan.Create(1, 1);
        var days = Week.SelectMany(day => new[] { new SessionDay(1, day, SessionKind.Noon), new SessionDay(2, day, SessionKind.Evening) }).ToArray();
        plan.Replace(SessionSystem.ThreeSessions, new Dictionary<SessionKind, IReadOnlyList<PeriodDraft>>
        {
            [SessionKind.Noon] = Timing(6, 11 * 60), [SessionKind.Evening] = Timing(6, 15 * 60, breakAfter: 2),
        }, days, 6, Week);
        Assert.Equal(SessionKind.Noon, plan.SessionOn(1, 3));
        Assert.Equal([2], plan.BreaksAfterLessons());

        plan.Replace(SessionSystem.OneSession, new Dictionary<SessionKind, IReadOnlyList<PeriodDraft>>(), [], 6, Week);
        Assert.Empty(plan.Periods);
        Assert.Empty(plan.Days);
        Assert.Empty(plan.BreaksAfterLessons());
        Assert.Equal(SessionKind.Morning, plan.SessionOn(2, 3));
    }

    [Fact]
    public void BreaksOfTheOtherSessionsAreGapsBetweenLessonNumbers()
    {
        Assert.Equal([3], Plan(Timing(6, 780, breakAfter: 3), OwnerExample()).BreaksAfterLessons());
        Assert.Empty(Plan(Timing(6, 780), OwnerExample()).BreaksAfterLessons());
    }

    private static SchedulingInput TwoLessonDoubles(IReadOnlyList<int>? sessionBreaks)
    {
        var school = new TestSchool();
        var shift = school.Shift(2);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 2);
        school.Assign(section, school.Line(stage, school.Subject(doublePeriod: true), 4), school.Teacher());
        var input = school.Build();
        return input with { Shifts = input.Shifts.Select(item => item with { SessionBreaksAfter = sessionBreaks }).ToArray() };
    }

    [Fact]
    public void ASingleSessionKeepsTheHashOfBeforeR3AndASessionBreakChangesIt()
    {
        var single = TwoLessonDoubles(null);
        var json = JsonSerializer.Serialize(SchedulingInputHash.Canonical(single));
        Assert.DoesNotContain(nameof(ShiftInput.SessionBreaksAfter), json, StringComparison.Ordinal);
        Assert.Equal(SchedulingInputHash.Compute(single), SchedulingInputHash.Compute(TwoLessonDoubles([])));
        Assert.NotEqual(SchedulingInputHash.Compute(single), SchedulingInputHash.Compute(TwoLessonDoubles([1])));

        // A stored snapshot without the field reads back as a single session.
        var restored = JsonSerializer.Deserialize<SchedulingInput>(json)!;
        Assert.Null(restored.Shifts.Single().SessionBreaksAfter);
        Assert.Equal(SchedulingInputHash.Compute(single), SchedulingInputHash.Compute(restored));
    }

    [Fact]
    public void AdjacencyHonoursTheBreaksOfEverySession()
    {
        var shift = TwoLessonDoubles([1]).Shifts.Single();
        Assert.False(shift.Adjacent(1));
        Assert.True((shift with { SessionBreaksAfter = null }).Adjacent(1));
    }

    [Fact]
    public async Task ABreakInAnotherSessionSplitsADoubleLessonEverywhere()
    {
        var doubles = TestSchool.Settings(GenerationModes.DoublePeriods);
        Assert.NotEmpty(await SolverTests.Valid(TwoLessonDoubles(null), doubles));

        // The evening session has a break after lesson 1: (1, 2) is no double. Impossible in the double mode; the
        // standard mode still timetables it, and the scorer and the verifier agree with the solver.
        var split = TwoLessonDoubles([1]);
        await SolverTests.Infeasible(split, doubles);
        var lessons = await SolverTests.Valid(split);
        var score = TimetableScorer.Score(split, lessons, doublePeriodsRequired: false);
        Assert.Equal(2, score.Rules.Single(rule => rule.Key == Domain.Scheduling.SchedulingRuleKeys.KeepDoubleLessonsTogether).Penalty);
        Assert.Contains(TimetableVerifier.Verify(split, lessons, doublePeriodsRequired: true), item => item.Code == ViolationCodes.DoublePeriodBroken);
        var readiness = PreSolveValidator.Validate(split, new ValidatorOptions(DoublePeriodsRequired: true));
        Assert.Contains(readiness.Findings, finding => finding.Code == FindingCodes.DoublePeriodImpossible);
    }
}
