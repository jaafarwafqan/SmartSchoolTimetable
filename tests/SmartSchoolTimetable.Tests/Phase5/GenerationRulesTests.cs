using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Infrastructure.Solver;
using SmartSchoolTimetable.Tests.Phase3;
using SmartSchoolTimetable.Tests.Phase4;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>
/// The owner's generation rules (Phase 5, ADR 0053): the daily limit per subject, grade and section, the teacher's most lessons in a row (H11),
/// spread with gaps (S6), balanced teacher days (S7, H12) and fair last periods (S8). Synthetic schools only; each rule has its unit, solver and verifier tests.
/// </summary>
[Collection(SerialSolverRuns.Name)]
public sealed class GenerationRulesTests
{
    private const int Sunday = 7;
    private const int Monday = 1;
    private static readonly int[] Week = [7, 1, 2, 3, 4];

    private static ScheduleGrid Grid => ScheduleGrid.Uniform(Week, 6);

    private static long Penalty(TimetableScore score, string key) => score.Rules.Single(rule => rule.Key == key).Penalty;

    private static PlacedLesson At(long section, long line, long teacher, int day, int lesson) => new(section, line, teacher, day, lesson);

    private static async Task<GenerationOutcome> Optimal(SchedulingInput input, int seconds = 20)
    {
        var outcome = await SolverTests.Generate(input, TestSchool.Settings(seconds: seconds));
        Assert.True(outcome.Readiness.Ready, string.Join(", ", outcome.Readiness.Findings.Select(finding => finding.Code)));
        Assert.Equal(SolverStatus.Optimal, outcome.Result.Status);
        Assert.Empty(outcome.Violations);
        Assert.Equal(outcome.Result.Objective, outcome.Score!.Total); // the scorer and the solver define every rule the same way
        return outcome;
    }

    // ---- 1–2: the daily limit per subject, grade and section ----------------------------------------------------------

    [Fact]
    public void ADailyLimitOfOneLessonIsAllowedAndNothingBelowOrAboveTheRange()
    {
        var subject = Subject.Create(new SubjectDetails("الفيزياء", 1, 3, true, false, false, false, null, null), Grid);
        subject.SetDailyCap(1);
        Assert.Equal(1, subject.DailyCap);
        Assert.Equal(DomainErrorCode.OutOfRange, Assert.Throws<DomainValidationException>(() => subject.SetDailyCap(0)).Errors.Single().Code);
        Assert.Equal(DomainErrorCode.OutOfRange, Assert.Throws<DomainValidationException>(() => subject.SetDailyCap(7)).Errors.Single().Code);
        subject.SetDailyCap(null);
        Assert.Null(subject.DailyCap);
    }

    [Fact]
    public void AGradeOrSectionLimitIsSetReplacedAndRemovedWithoutTouchingTheRest()
    {
        var subject = Subject.Create(new SubjectDetails("الفيزياء", 1, 3, true, false, false, false, null, null), Grid);
        Assert.True(subject.SetCapOverride(CapScope.Stage, 5, 1));
        Assert.True(subject.SetCapOverride(CapScope.Section, 9, 3));
        Assert.False(subject.SetCapOverride(CapScope.Section, 9, 3)); // the same value: no new version
        Assert.True(subject.SetCapOverride(CapScope.Section, 9, 2));
        Assert.Equal([new SubjectCapOverride(CapScope.Stage, 5, 1), new SubjectCapOverride(CapScope.Section, 9, 2)], subject.CapOverrides.OrderBy(item => item.Scope));
        Assert.True(subject.SetCapOverride(CapScope.Stage, 5, null));
        Assert.False(subject.SetCapOverride(CapScope.Stage, 5, null)); // already automatic
        Assert.Single(subject.CapOverrides);
        Assert.Equal(DomainErrorCode.OutOfRange, Assert.Throws<DomainValidationException>(() => subject.SetCapOverride(CapScope.Section, 9, 0)).Errors.Single().Code);
        Assert.Equal(DomainErrorCode.InvalidOption, Assert.Throws<DomainValidationException>(() => subject.SetCapOverride(CapScope.Section, 0, 2)).Errors.Single().Code);
        // Editing the subject's other fields keeps its limits.
        subject.Update(new SubjectDetails("الفيزياء", 2, 3, true, false, false, false, null, null), Grid);
        Assert.Single(subject.CapOverrides);
    }

    [Fact]
    public void TheMostSpecificLimitWins()
    {
        var factory = new InputFactory();
        var shift = factory.Shift();
        var first = factory.Section(shift, 1, label: "أ");
        var second = factory.Section(shift, 1, label: "ب");
        var other = factory.Section(shift, 2, label: "أ", stageName: "الثاني");
        var subject = factory.Subject("العربية", dailyCap: 3);
        var plain = factory.Subject("الرياضيات");
        factory.CapForStage(subject, 1, 4).CapForSection(subject, second, 1);
        var input = factory.Build();
        var caps = new DailyCaps(input);
        var arabic = input.Subjects.Single(item => item.Id == subject);
        var sectionOf = (long id) => input.Sections.Single(section => section.Id == id);

        Assert.Equal(1, caps.For(arabic, sectionOf(second), 10, 5)); // the section's own limit
        Assert.Equal(4, caps.For(arabic, sectionOf(first), 10, 5)); // the grade's limit beats the subject's
        Assert.Equal(3, caps.For(arabic, sectionOf(other), 10, 5)); // another grade: the subject's limit
        Assert.Equal(2, caps.For(input.Subjects.Single(item => item.Id == plain), sectionOf(first), 10, 5)); // none: automatic
        Assert.Equal(1, new DailyCaps(input).For(arabic, sectionOf(second), 0, 0));
    }

    [Fact]
    public void ALimitOfOneBelowTheLessonsOverTheDaysIsAReadinessErrorNamingTheSectionAndSubject()
    {
        // Six lessons a week, five days, one lesson a day at most: 1 × 5 < 6.
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1, label: "أ");
        var subject = factory.Subject("الفيزياء", dailyCap: 1);
        var teacher = factory.Teacher("سلمى");
        factory.Assign(section, factory.Line(1, subject, 6), teacher);
        factory.Assign(section, factory.Line(1, factory.Subject("اللغة"), 24), factory.Teacher("رغد"));
        var finding = Assert.Single(PreSolveValidator.Validate(factory.Build()).Findings, item => item.Code == FindingCodes.SubjectDaysShort);
        Assert.Equal((PreSolveValidator.Error, 6, 5), (finding.Severity, finding.Required, finding.Available));
        Assert.Equal((section, subject), (finding.Entity.Id, finding.Related[0].Id));
        Assert.Equal(["6", "1"], finding.Details.Take(2));

        // The same subject with a section limit of 2 is fine for that section.
        var fixedFactory = new InputFactory();
        var fixedSection = fixedFactory.Section(fixedFactory.Shift(), 1, label: "أ");
        var fixedSubject = fixedFactory.Subject("الفيزياء", dailyCap: 1);
        fixedFactory.Assign(fixedSection, fixedFactory.Line(1, fixedSubject, 6), fixedFactory.Teacher("سلمى"));
        fixedFactory.Assign(fixedSection, fixedFactory.Line(1, fixedFactory.Subject("اللغة"), 24), fixedFactory.Teacher("رغد"));
        Assert.DoesNotContain(PreSolveValidator.Validate(fixedFactory.CapForSection(fixedSubject, fixedSection, 2).Build()).Findings, item => item.Code == FindingCodes.SubjectDaysShort);
    }

    [Fact]
    public async Task TheSolverAndTheVerifierUseTheSectionLimitOverTheGradeAndSubjectLimits()
    {
        // Two sections of one grade, Physics 5 lessons a week in 6-lesson days. The grade allows 3 a day, the second section only 1.
        var school = new TestSchool().Rules();
        var shift = school.Shift();
        var stage = school.Stage();
        var firstSection = school.Section(shift, stage);
        var secondSection = school.Section(shift, stage);
        var physics = school.Subject(dailyCap: 3);
        var physicsLine = school.Line(stage, physics, 5);
        var rest = school.Line(stage, school.Subject(), 25);
        school.Assign(firstSection, physicsLine, school.Teacher());
        school.Assign(secondSection, physicsLine, school.Teacher());
        school.Assign(firstSection, rest, school.Teacher());
        school.Assign(secondSection, rest, school.Teacher());
        school.CapForSection(physics, secondSection, 1);
        var input = school.Build();

        var lessons = await SolverTests.Valid(input);
        var perDay = lessons.Where(lesson => lesson.LineId == physicsLine && lesson.SectionId == secondSection).GroupBy(lesson => lesson.Day).Select(day => day.Count()).ToArray();
        Assert.Equal([1, 1, 1, 1, 1], perDay); // one a day in the limited section: five lessons need five days
        // Three physics lessons on one day pass the grade's limit but not the section's.
        var stacked = lessons.Where(lesson => lesson.SectionId != secondSection || lesson.LineId != physicsLine).ToList();
        var day = Week[0];
        stacked.AddRange(Enumerable.Range(1, 5).Select(number => At(secondSection, physicsLine, input.Assignments.Single(row => row.SectionId == secondSection && row.LineId == physicsLine).TeacherId, day, number)));
        Assert.Contains(TimetableVerifier.Verify(input, stacked, false), violation => violation.Code == ViolationCodes.SubjectDailyCap && violation.SectionId == secondSection);
    }

    [Fact]
    public void TheLimitsAndTheTeacherRuleChangeTheInputHashButTheEveningMappingDoesNot()
    {
        SchedulingInput Build(Action<TestSchool, long, long, long>? change = null, int[]? evening = null)
        {
            var school = new TestSchool();
            var shift = school.Shift();
            var stage = school.Stage();
            var section = school.Section(shift, stage);
            var subject = school.Subject();
            var teacher = school.Teacher();
            school.Assign(section, school.Line(stage, subject, 5), teacher);
            change?.Invoke(school, subject, section, teacher);
            if (evening is not null)
                school.Evening(evening);
            return school.Build();
        }

        var plain = SchedulingInputHash.Compute(Build());
        Assert.NotEqual(plain, SchedulingInputHash.Compute(Build((school, subject, section, _) => school.CapForSection(subject, section, 1))));
        Assert.NotEqual(plain, SchedulingInputHash.Compute(Build((school, _, _, _) => school.Consecutive(3))));
        Assert.NotEqual(plain, SchedulingInputHash.Compute(Build((school, _, _, _) => school.HardBalance())));
        Assert.Equal(plain, SchedulingInputHash.Compute(Build(evening: [Sunday]))); // a soft preference only: a saved timetable stays current (DECISIONS_PENDING #81)
    }

    [Fact]
    public void ChangedLimitsAndTeacherRulesAreNamedInTheChangeList()
    {
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1);
        var subject = factory.Subject("الفيزياء");
        var teacher = factory.Teacher("سلمى");
        factory.Assign(section, factory.Line(1, subject, 5), teacher);
        var before = factory.Build();
        var after = before with
        {
            CapOverrides = [new CapOverrideInput(subject, null, section, 1)],
            Teachers = before.Teachers.Select(item => item with { MaxConsecutive = 2 }).ToArray(),
        };
        var codes = InputDiff.Compare(before, after).Select(change => change.Code).ToArray();
        Assert.Contains(InputChangeCodes.SubjectRulesChanged, codes);
        Assert.Contains(InputChangeCodes.TeacherConsecutiveChanged, codes);
    }

    // ---- 3: the teacher's most lessons in a row ------------------------------------------------------------------------

    [Theory]
    [InlineData(2, 4)] // P = 6: ⌊7·2/3⌋ = 4
    [InlineData(3, 5)] // ⌊7·3/4⌋ = 5
    public void ATeacherCanTeachFourOrFiveOfSixLessonsWhenTwoOrThreeMayFollowEachOther(int consecutive, int perDay)
    {
        var factory = new InputFactory().Consecutive(consecutive);
        var section = factory.Section(factory.Shift(), 1);
        factory.Assign(section, factory.Line(1, factory.Subject("اللغة"), 30), factory.Teacher("رغد"));
        var info = TeacherDays.Of(factory.Build()).Values.Single();
        Assert.All(info.Days, day => Assert.Equal(perDay, day.Capacity));
        Assert.Equal(perDay * 5, info.Capacity);
        Assert.Equal(perDay, (7 * consecutive) / (consecutive + 1)); // the owner's formula ⌊(P + 1)·k ÷ (k + 1)⌋
    }

    [Fact]
    public void ALoadThatDoesNotFitUnderTheLimitIsAnErrorWithTheNumbersAndTheSuggestion()
    {
        SchedulingInput Build(int? own = null, int school = 2)
        {
            // Six lessons a day, five days. The teacher holds 24 of the section's 30 lessons.
            var factory = new InputFactory().Consecutive(school);
            var section = factory.Section(factory.Shift(), 1);
            factory.Assign(section, factory.Line(1, factory.Subject("اللغة"), 24), factory.Teacher("رغد", maxConsecutive: own));
            factory.Assign(section, factory.Line(1, factory.Subject("الرياضيات"), 6), factory.Teacher("سلمى"));
            return factory.Build();
        }

        var finding = Assert.Single(PreSolveValidator.Validate(Build()).Findings, item => item.Code == FindingCodes.TeacherConsecutiveShort);
        Assert.Equal((PreSolveValidator.Error, 24, 20), (finding.Severity, finding.Required, finding.Available));
        Assert.Equal("رغد", finding.Entity.Name);
        Assert.Equal(["2", "5"], finding.Details);
        Assert.Contains("raiseTeacherConsecutive", finding.Fixes); // «اجعلها ٣» is offered while the limit is 2
        // With three in a row (the school's default, or the teacher's own) the load fits: 5 × 5 = 25.
        Assert.DoesNotContain(PreSolveValidator.Validate(Build(school: 3)).Findings, item => item.Code == FindingCodes.TeacherConsecutiveShort);
        Assert.DoesNotContain(PreSolveValidator.Validate(Build(own: 3, school: 2)).Findings, item => item.Code == FindingCodes.TeacherConsecutiveShort);
        // At 3, a load of 26 still does not fit and the suggestion is only to reduce the load.
        var factory = new InputFactory().Consecutive(3);
        var section = factory.Section(factory.Shift(), 1);
        factory.Assign(section, factory.Line(1, factory.Subject("اللغة"), 26), factory.Teacher("رغد"));
        factory.Assign(section, factory.Line(1, factory.Subject("الرياضيات"), 4), factory.Teacher("سلمى"));
        var three = Assert.Single(PreSolveValidator.Validate(factory.Build()).Findings, item => item.Code == FindingCodes.TeacherConsecutiveShort);
        Assert.DoesNotContain("raiseTeacherConsecutive", three.Fixes);
        Assert.Equal((26, 25), (three.Required, three.Available));
    }

    [Fact]
    public void WithoutTheRuleTheValidatorStaysSilent()
    {
        // Hand-built inputs have no school default: no consecutive-lessons rule applies.
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1);
        factory.Assign(section, factory.Line(1, factory.Subject("اللغة"), 30), factory.Teacher("رغد"));
        Assert.DoesNotContain(PreSolveValidator.Validate(factory.Build()).Findings, item => item.Code == FindingCodes.TeacherConsecutiveShort);
    }

    [Fact]
    public async Task ATeacherNeverTeachesMoreThanHisLimitInARowAndTheLimitIsTight()
    {
        // Two sections of six lessons a day; the shared teacher holds 10 + 10 = 20 lessons: exactly 4 a day at a limit of 2 in a row.
        var school = new TestSchool().Rules().Consecutive(2);
        var shift = school.Shift();
        var stage = school.Stage();
        var shared = school.Teacher();
        var line = school.Line(stage, school.Subject(), 10);
        var filler = school.Line(stage, school.Subject(), 20);
        var sections = new[] { school.Section(shift, stage), school.Section(shift, stage) };
        foreach (var section in sections)
        {
            school.Assign(section, line, shared);
            school.Assign(section, filler, school.Teacher());
        }
        var input = school.Build();
        var lessons = await SolverTests.Valid(input);
        foreach (var day in lessons.Where(lesson => lesson.TeacherId == shared).GroupBy(lesson => lesson.Day))
        {
            var used = day.Select(lesson => lesson.Lesson).Order().ToHashSet();
            Assert.DoesNotContain(used, number => used.Contains(number + 1) && used.Contains(number + 2)); // never three in a row
            Assert.Equal(4, used.Count);
        }
        // Without the limit the same school may stack six.
        Assert.Empty(TimetableVerifier.Verify(school.Consecutive(null).Build(), lessons, false));
    }

    [Fact]
    public void TheVerifierFlagsThreeInARowAtTwoAndAcceptsThemAtThreeAndATeachersOwnLimitBeatsTheSchools()
    {
        var school = new TestSchool().Rules().Consecutive(3);
        var shift = school.Shift();
        var stage = school.Stage();
        var section = school.Section(shift, stage, 6);
        var subject = school.Subject();
        var line = school.Line(stage, subject, 3);
        var teacher = school.Teacher(maxConsecutive: 2);
        var other = school.Teacher();
        school.Assign(section, line, teacher);
        var rest = school.Line(stage, school.Subject(), 27);
        school.Assign(section, rest, other);
        var lessons = new[] { At(section, line, teacher, Sunday, 1), At(section, line, teacher, Sunday, 2), At(section, line, teacher, Sunday, 3) };
        var input = school.Build();
        Assert.Contains(TimetableVerifier.Verify(input, lessons, false), violation => violation is { Code: ViolationCodes.TeacherConsecutive, Rule: "H11", Count: 3, Limit: 2 } && violation.TeacherId == teacher);
        // The teacher without his own limit follows the school's three: three in a row are allowed.
        var free = input with { Teachers = input.Teachers.Select(item => item.Id == teacher ? item with { MaxConsecutive = null } : item).ToArray() };
        Assert.DoesNotContain(TimetableVerifier.Verify(free, lessons, false), violation => violation.Code == ViolationCodes.TeacherConsecutive);
        // Lessons separated by a free lesson are not in a row.
        var spaced = new[] { At(section, line, teacher, Sunday, 1), At(section, line, teacher, Sunday, 2), At(section, line, teacher, Sunday, 4) };
        Assert.DoesNotContain(TimetableVerifier.Verify(input, spaced, false), violation => violation.Code == ViolationCodes.TeacherConsecutive);
    }

    [Fact]
    public async Task AnImpossibleRunIsExplainedWithTheConsecutiveRuleAsTheCause()
    {
        // One section of four lessons a day taught by one teacher: four in a row every day, but at most three are allowed.
        var school = new TestSchool().Rules().Consecutive(3);
        var shift = school.Shift(4);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 4);
        var teacher = school.Teacher();
        school.Assign(section, school.Line(stage, school.Subject(), 20), teacher);
        var input = school.Build();
        Assert.Contains(PreSolveValidator.Validate(input).Findings, finding => finding.Code == FindingCodes.TeacherConsecutiveShort);
        var result = await SolverTests.Solve(input);
        Assert.Equal(SolverStatus.Infeasible, result.Status);
        var core = Assert.Single(result.Diagnostics!.Findings, finding => finding.Code == DiagnosticCodes.TeacherConsecutive);
        Assert.Equal(teacher, core.Entity.Id);
        Assert.True(core.RelaxationHelps);
        Assert.Contains("raiseTeacherConsecutive", core.Fixes);
        // Without the rule the school is possible.
        Assert.Equal(20, (await SolverTests.Valid(school.Consecutive(null).Build())).Count);
    }

    // ---- 3 (soft): spread with gaps, mutually exclusive with consecutive ---------------------------------------------

    [Fact]
    public void SpreadAndConsecutiveCannotBothBeOn()
    {
        var profile = SchedulingProfile.CreateDefault();
        Assert.True(profile.Rules.Single(rule => rule.Key == SchedulingRuleKeys.SpreadTeacherLessons).Enabled);
        Assert.False(profile.Rules.Single(rule => rule.Key == SchedulingRuleKeys.AvoidTeacherGaps).Enabled);
        var both = profile.Rules.Select(rule => rule.Key == SchedulingRuleKeys.AvoidTeacherGaps ? rule with { Enabled = true } : rule).ToArray();
        Assert.Equal(DomainErrorCode.InvalidOption, Assert.Throws<DomainValidationException>(() => profile.Update(both)).Errors.Single().Code);
        // Switching: «متتالية» on, «بفواصل» off.
        var switched = profile.Rules.Select(rule => rule.Key switch
        {
            SchedulingRuleKeys.AvoidTeacherGaps => rule with { Enabled = true },
            SchedulingRuleKeys.SpreadTeacherLessons => rule with { Enabled = false },
            _ => rule,
        }).ToArray();
        Assert.True(profile.Update(switched));
        // The school default is 2 or 3 only.
        Assert.Equal(DomainErrorCode.OutOfRange, Assert.Throws<DomainValidationException>(() => profile.Update(switched, defaultMaxConsecutive: 4)).Errors.Single().Code);
        Assert.Equal(DomainErrorCode.OutOfRange, Assert.Throws<DomainValidationException>(() => profile.Update(switched, defaultMaxConsecutive: 1)).Errors.Single().Code);
        Assert.True(profile.Update(switched, defaultMaxConsecutive: 2, balanceHard: true));
        Assert.Equal((2, true), (profile.DefaultMaxConsecutive, profile.BalanceHard));
        Assert.True(profile.RestoreDefaults());
        Assert.Equal((3, false), (profile.DefaultMaxConsecutive, profile.BalanceHard));
    }

    private static TestSchool SharedTeacherSchool(params (string Key, bool Enabled, int Weight)[] rules)
    {
        // Two three-lesson sections share a teacher for 5 lessons each (10 of the section's 10 lessons are the shared teacher's and a filler's).
        var school = new TestSchool().Rules(rules);
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
        return school;
    }

    [Fact]
    public async Task SpreadLeavesTheTeachersLessonsApartWhereConsecutivePacksThemTogether()
    {
        var spread = await Optimal(SharedTeacherSchool((SchedulingRuleKeys.SpreadTeacherLessons, true, 30)).Build());
        var apart = Penalty(spread.Score!, SchedulingRuleKeys.SpreadTeacherLessons);

        var packedSchool = SharedTeacherSchool((SchedulingRuleKeys.AvoidTeacherGaps, true, 30));
        var packed = await Optimal(packedSchool.Build());
        // The packed timetable, judged by the spread rule, has adjacent lessons: that is exactly what «متتالية» asks for.
        var judged = TimetableScorer.Score(SharedTeacherSchool((SchedulingRuleKeys.SpreadTeacherLessons, true, 30)).Build(), packed.Result.Lessons, false);
        Assert.True(Penalty(judged, SchedulingRuleKeys.SpreadTeacherLessons) > apart, $"{apart} vs {Penalty(judged, SchedulingRuleKeys.SpreadTeacherLessons)}");
    }

    [Fact]
    public void ThePairsOfADoubleLessonAreNotCountedAsAdjacent()
    {
        var school = new TestSchool().Rules((SchedulingRuleKeys.SpreadTeacherLessons, true, 30));
        var shift = school.Shift(4);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 4);
        var teacher = school.Teacher();
        var doubled = school.Line(stage, school.Subject(doublePeriod: true), 2, doublePeriod: true);
        var plain = school.Line(stage, school.Subject(), 2);
        school.Assign(section, doubled, teacher);
        school.Assign(section, plain, teacher);
        var input = school.Build();
        // A double lesson at 1-2: no adjacency. A plain lesson at 3 next to it: one.
        var apart = new[] { At(section, doubled, teacher, Sunday, 1), At(section, doubled, teacher, Sunday, 2), At(section, plain, teacher, Monday, 1), At(section, plain, teacher, Monday, 3) };
        Assert.Equal(0, Penalty(TimetableScorer.Score(input, apart, false), SchedulingRuleKeys.SpreadTeacherLessons));
        var touching = new[] { At(section, doubled, teacher, Sunday, 1), At(section, doubled, teacher, Sunday, 2), At(section, plain, teacher, Sunday, 3), At(section, plain, teacher, Monday, 1) };
        Assert.Equal(1, Penalty(TimetableScorer.Score(input, touching, false), SchedulingRuleKeys.SpreadTeacherLessons));
    }

    // ---- 4: balanced teacher days ---------------------------------------------------------------------------------------

    private static TestSchool BalanceSchool(bool hard, params (string Key, bool Enabled, int Weight)[] rules)
    {
        // Three sections of six lessons a day; one teacher teaches 6 lessons a week in each: 18 lessons over 5 free days (3 or 4 a day).
        var school = new TestSchool().Rules(rules).HardBalance(hard);
        var shift = school.Shift();
        var stage = school.Stage();
        var balanced = school.Teacher();
        var line = school.Line(stage, school.Subject(), 6);
        var filler = school.Line(stage, school.Subject(), 24);
        foreach (var _ in Enumerable.Range(0, 3))
        {
            var section = school.Section(shift, stage);
            school.Assign(section, line, balanced);
            school.Assign(section, filler, school.Teacher());
        }
        return school;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EighteenLessonsOverFiveFreeDaysAreThreeOrFourADayNeverFourAndOne(bool hard)
    {
        var input = BalanceSchool(hard, (SchedulingRuleKeys.BalanceTeacherDays, true, 40)).Build();
        var teacher = input.Assignments.GroupBy(row => row.TeacherId).Single(group => group.Count() == 3).Key;
        var info = TeacherDays.Of(input)[teacher];
        Assert.Equal((18, 5), (info.Load, info.FullDays.Count));
        var outcome = hard ? await SolveHard(input) : await Optimal(input);
        var counts = Week.Select(day => outcome.Result.Lessons.Count(lesson => lesson.TeacherId == teacher && lesson.Day == day)).ToArray();
        Assert.Equal(18, counts.Sum());
        Assert.All(counts, count => Assert.InRange(count, 3, 4));
        if (!hard)
            Assert.Equal(0, Penalty(outcome.Score!, SchedulingRuleKeys.BalanceTeacherDays));
    }

    private static async Task<GenerationOutcome> SolveHard(SchedulingInput input)
    {
        var outcome = await SolverTests.Generate(input, TestSchool.Settings(seconds: 20));
        Assert.True(outcome.Result.HasTimetable, outcome.Result.Status.ToString());
        Assert.Empty(outcome.Violations);
        return outcome;
    }

    [Fact]
    public void TheScorerCountsTheGapBetweenTheBusiestAndTheQuietestFullDayMinusOne()
    {
        var school = new TestSchool().Rules((SchedulingRuleKeys.BalanceTeacherDays, true, 40));
        var shift = school.Shift();
        var stage = school.Stage();
        var section = school.Section(shift, stage);
        var teacher = school.Teacher();
        var line = school.Line(stage, school.Subject(), 10);
        school.Assign(section, line, teacher);
        var input = school.Build();
        PlacedLesson[] Days(params int[] perDay) => perDay.SelectMany((count, index) => Enumerable.Range(1, count).Select(lesson => At(section, line, teacher, Week[index], lesson))).ToArray();
        long Unevenness(params int[] perDay) => Penalty(TimetableScorer.Score(input, Days(perDay), false), SchedulingRuleKeys.BalanceTeacherDays);

        Assert.Equal(0, Unevenness(2, 2, 2, 2, 2));
        Assert.Equal(0, Unevenness(3, 2, 2, 2, 1) - 1); // the busiest is two lessons above the quietest: 3 − 1 − 1 = 1
        Assert.Equal(1, Unevenness(3, 2, 2, 2, 1));
        Assert.Equal(0, Unevenness(3, 3, 2, 1, 1) - 1); // 3 − 1 − 1 = 1 as well
        Assert.Equal(3, Unevenness(4, 4, 2, 0, 0)); // 4 − 0 − 1
    }

    [Fact]
    public void APartlyAvailableDayTakesAProportionalShareAndIsNotBalancedAgainstTheFullDays()
    {
        // Monday's lessons 4–6 are blocked for the teacher: 3 usable slots against 6 on the other days.
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1);
        var teacher = factory.Teacher("رغد", blocked: [new SlotRef(Monday, 4), new SlotRef(Monday, 5), new SlotRef(Monday, 6)]);
        factory.Assign(section, factory.Line(1, factory.Subject("اللغة"), 15), teacher);
        factory.Assign(section, factory.Line(1, factory.Subject("الرياضيات"), 15), factory.Teacher("سلمى"));
        var info = TeacherDays.Of(factory.Build())[teacher];
        Assert.Equal([Sunday, 2, 3, 4], info.FullDays);
        Assert.Equal(3, info.Days.Single(day => day.Day == Monday).Usable);
        Assert.False(info.Days.Single(day => day.Day == Monday).Full);
        Assert.Equal(2, info.PartialAllowance(Monday)); // 15 lessons × 3 of 27 usable slots = 1.67, rounded up
        Assert.Null(info.PartialAllowance(Sunday));
    }

    [Fact]
    public void TheVerifierEnforcesHardBalanceOnlyWhenTheSchoolMadeItHard()
    {
        static PlacedLesson[] Unbalanced(long section, long line, long teacher) =>
            Enumerable.Range(1, 6).Select(lesson => At(section, line, teacher, Sunday, lesson)).Concat(Enumerable.Range(1, 6).Select(lesson => At(section, line, teacher, Monday, lesson))).ToArray();
        SchedulingInput Build(bool hard, out PlacedLesson[] lessons)
        {
            var school = new TestSchool().Rules().HardBalance(hard);
            var shift = school.Shift();
            var stage = school.Stage();
            var section = school.Section(shift, stage);
            var teacher = school.Teacher();
            var line = school.Line(stage, school.Subject(), 12);
            school.Assign(section, line, teacher);
            lessons = Unbalanced(section, line, teacher);
            return school.Build();
        }

        var soft = Build(false, out var softLessons);
        Assert.DoesNotContain(TimetableVerifier.Verify(soft, softLessons, false), violation => violation.Code == ViolationCodes.TeacherBalance);
        var hard = Build(true, out var hardLessons);
        Assert.Contains(TimetableVerifier.Verify(hard, hardLessons, false), violation => violation is { Code: ViolationCodes.TeacherBalance, Rule: "H12" });
    }

    [Fact]
    public async Task SubjectsStillSpreadAcrossDaysInEverySection()
    {
        // S1 per section: two sections each get their five physics lessons on five different days.
        var school = new TestSchool().Rules((SchedulingRuleKeys.SpreadSubjectsAcrossDays, true, 20), (SchedulingRuleKeys.BalanceTeacherDays, true, 40));
        var shift = school.Shift(3);
        var stage = school.Stage();
        var physics = school.Line(stage, school.Subject(spread: true), 5);
        var rest = school.Line(stage, school.Subject(), 10);
        var sections = new[] { school.Section(shift, stage, 3), school.Section(shift, stage, 3) };
        var shared = school.Teacher();
        foreach (var section in sections)
        {
            school.Assign(section, physics, shared);
            school.Assign(section, rest, school.Teacher());
        }
        var outcome = await Optimal(school.Build());
        Assert.Equal(0, Penalty(outcome.Score!, SchedulingRuleKeys.SpreadSubjectsAcrossDays));
        foreach (var section in sections)
            Assert.All(outcome.Result.Lessons.Where(lesson => lesson.SectionId == section && lesson.LineId == physics).GroupBy(lesson => lesson.Day), day => Assert.Single(day));
    }

    // ---- 7: fair last periods ---------------------------------------------------------------------------------------------

    private static (TestSchool School, long Section, long[] Teachers) LastPeriodSchool(params (string Key, bool Enabled, int Weight)[] rules)
    {
        // One three-lesson section with three subjects of five lessons. Two of them are heavy (the heavy-early rule wants them in lessons 1 and 2),
        // so without a fairness rule the third teacher gets every last period.
        var school = new TestSchool().Rules(rules);
        var shift = school.Shift(3);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 3);
        var first = school.Teacher();
        var second = school.Teacher();
        var third = school.Teacher();
        school.Assign(section, school.Line(stage, school.Subject(heavy: true), 5), first);
        school.Assign(section, school.Line(stage, school.Subject(heavy: true), 5), second);
        school.Assign(section, school.Line(stage, school.Subject(), 5), third);
        return (school, section, [first, second, third]);
    }

    private static int[] LastCounts(IEnumerable<PlacedLesson> lessons, IEnumerable<long> teachers, int last = 3) =>
        teachers.Select(teacher => lessons.Count(lesson => lesson.TeacherId == teacher && lesson.Lesson == last)).ToArray();

    [Fact]
    public async Task WithoutFairnessOneTeacherGetsEveryLastPeriodAndWithItTheSpreadDrops()
    {
        var (unfair, _, teachers) = LastPeriodSchool((SchedulingRuleKeys.HeavySubjectsEarly, true, 15));
        var without = await Optimal(unfair.Build());
        var before = LastCounts(without.Result.Lessons, teachers);
        Assert.Equal(5, before.Max()); // one teacher holds all five last periods
        Assert.Equal(5, before.Max() - before.Min());

        var (fair, _, sameTeachers) = LastPeriodSchool((SchedulingRuleKeys.HeavySubjectsEarly, true, 15), (SchedulingRuleKeys.FairLastPeriods, true, 40));
        var with = await Optimal(fair.Build());
        var after = LastCounts(with.Result.Lessons, sameTeachers);
        Assert.Equal(5, after.Sum());
        Assert.True(after.Max() <= 2, string.Join(",", after)); // each teacher's share by load: ⌈5 × 5 ÷ 15⌉ = 2
        Assert.True(after.Max() - after.Min() < before.Max() - before.Min());
        Assert.Equal(0, Penalty(with.Score!, SchedulingRuleKeys.FairLastPeriods));
    }

    [Fact]
    public async Task TheNoteNamesTheTeacherWithTheMostAndTheLeastLastPeriods()
    {
        var (school, _, teachers) = LastPeriodSchool((SchedulingRuleKeys.HeavySubjectsEarly, true, 15));
        var outcome = await Optimal(school.Build());
        var insight = outcome.Score!.LastPeriods;
        Assert.NotNull(insight);
        Assert.Equal((5, 0), (insight.MostCount, insight.LeastCount));
        Assert.Equal($"معلم {teachers[2]}", insight.MostTeacher);
        Assert.NotEqual(insight.MostTeacher, insight.LeastTeacher);
    }

    [Fact]
    public void AnEveningLastPeriodWeighsDoubleSoHoldingBothEveningDaysAlreadyTipsTheScale()
    {
        // Sunday and Monday are evening days. Teacher A holds the last period on both (weight 2 + 2 = 4); the weekly share by load is 3 (⌈7 × 5 ÷ 15⌉).
        var (school, section, teachers) = LastPeriodSchool((SchedulingRuleKeys.FairLastPeriods, true, 40));
        var plain = school.Build();
        var evening = school.Evening(Sunday, Monday).Build();
        var lines = plain.Assignments.ToDictionary(row => row.TeacherId, row => row.LineId);
        long HolderOn(int day) => day switch { Sunday or Monday => teachers[0], 2 or 3 => teachers[1], _ => teachers[2] };
        var lessons = Week.Select(day => At(section, lines[HolderOn(day)], HolderOn(day), day, 3)).ToArray();
        // Unweighted, the share is 2 and teacher A holds 2: no penalty. Weighted, A holds 4 against a share of 3: one.
        Assert.Equal(2, LastPeriodFairness.Allowances(plain)[teachers[0]]);
        Assert.Equal(3, LastPeriodFairness.Allowances(evening)[teachers[0]]);
        Assert.Equal(0, Penalty(TimetableScorer.Score(plain, lessons, false), SchedulingRuleKeys.FairLastPeriods));
        Assert.Equal(1, Penalty(TimetableScorer.Score(evening, lessons, false), SchedulingRuleKeys.FairLastPeriods));
    }

    [Fact]
    public async Task TheSolverKeepsEveryTeachersWeightedLastPeriodsWithinHisShareOnEveningDays()
    {
        var (school, _, teachers) = LastPeriodSchool((SchedulingRuleKeys.HeavySubjectsEarly, true, 15), (SchedulingRuleKeys.FairLastPeriods, true, 40));
        var input = school.Evening(Sunday, Monday).Build();
        var outcome = await Optimal(input);
        var share = LastPeriodFairness.Allowances(input);
        foreach (var teacher in teachers)
        {
            var weighted = outcome.Result.Lessons.Where(lesson => lesson.TeacherId == teacher && lesson.Lesson == 3).Sum(lesson => LastPeriodFairness.WeightOf(input, lesson.Day));
            Assert.True(weighted <= share[teacher], $"{weighted} > {share[teacher]}");
        }
        // Nobody holds both evening last periods (that would weigh 4, above the share of 3).
        Assert.All(teachers, teacher => Assert.True(outcome.Result.Lessons.Count(lesson => lesson.TeacherId == teacher && lesson.Lesson == 3 && lesson.Day is Sunday or Monday) <= 1));
    }

    // ---- the engine as a whole ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task EveryNewRuleCanBeSwitchedOffAndTheTimetableStillVerifies()
    {
        var input = BalanceSchool(false).Build();
        var plain = await SolverTests.Valid(input);
        Assert.NotEmpty(plain);
        // Every default priority on, the school's limit at two in a row: the timetable is still valid and the shared teacher never has three in a row.
        var ruled = SharedTeacherSchool(SchedulingRuleKeys.Defaults.Select(rule => (rule.Key, rule.Enabled, rule.Weight)).ToArray()).Consecutive(2).Build();
        var lessons = await SolverTests.Valid(ruled);
        Assert.Empty(TimetableVerifier.Verify(ruled, lessons, false));
    }
}
