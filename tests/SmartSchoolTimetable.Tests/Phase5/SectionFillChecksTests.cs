using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Tests.Phase3;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>
/// The «ملء الشعبة» pre-solve checks (the owner's real-school test: 12 sections, 30 lessons a week in 30 slots, so every section is
/// full every day). Each check has its smallest counter-example, a school that must stay clean, and the owner's daily-limit override.
/// </summary>
public sealed class SectionFillChecksTests
{
    private const int Sunday = 7;
    private const int Monday = 1;

    private static ValidationFinding Single(ValidationReport report, string code) => Assert.Single(report.Findings, finding => finding.Code == code);

    private static bool Has(ValidationReport report, string code) => report.Findings.Any(finding => finding.Code == code);

    [Fact]
    public void ASubjectNeedingFiveDaysWithATeacherFreeOnFourIsAnError()
    {
        // Arabic: 10 lessons, the daily limit is 2, so it needs 5 days; its teacher is off on Sunday: «يحتاج ٥ أيام (١٠ حصص، حدّها حصتان يومياً) والمعلم متاح ٤ أيام فقط».
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1, lessonsByDay: [2, 2, 2, 2, 2]);
        var arabic = factory.Subject("العربية");
        factory.Assign(section, factory.Line(1, arabic, 10), factory.Teacher("رغد", offDays: [Sunday]));
        var finding = Single(PreSolveValidator.Validate(factory.Build()), FindingCodes.SubjectDaysShort);
        Assert.Equal((PreSolveValidator.Error, 5, 4), (finding.Severity, finding.Required, finding.Available));
        Assert.Equal(section, finding.Entity.Id);
        Assert.Equal(arabic, finding.Related[0].Id);
        Assert.Equal("رغد", finding.Related[1].Name);
        Assert.Equal(["10", "2", "7"], finding.Details);
    }

    [Fact]
    public void TheOwnerCanRaiseTheDailyLimitOfASubject()
    {
        // 5 days of 3 lessons: Arabic 10 (limit 2 needs 5 days) and Math 5. Arabic's teacher is off on Sunday: an error with the automatic limit, none with a limit of 3.
        static ValidationReport Run(int? arabicCap, int? mathCap)
        {
            var factory = new InputFactory();
            var section = factory.Section(factory.Shift(), 1, lessonsByDay: [3, 3, 3, 3, 3]);
            factory.Assign(section, factory.Line(1, factory.Subject("العربية", dailyCap: arabicCap), 10), factory.Teacher("رغد", offDays: [Sunday]));
            factory.Assign(section, factory.Line(1, factory.Subject("الرياضيات", dailyCap: mathCap), 5), factory.Teacher("سلمى"));
            return PreSolveValidator.Validate(factory.Build());
        }

        Assert.True(Has(Run(null, null), FindingCodes.SubjectDaysShort));
        var raised = Run(3, 3);
        Assert.False(Has(raised, FindingCodes.SubjectDaysShort));
        Assert.True(raised.Ready);
    }

    [Fact]
    public void TheDailyLimitIsTheSameRuleTheSolverUses()
    {
        var subject = new SubjectInput(1, "العربية", 3, true, false, false, false, null, [], 1, 3);
        Assert.Equal(3, DailyCaps.For(subject, 10, 5));
        Assert.Equal(2, DailyCaps.For(subject with { DailyCap = null }, 10, 5));
        Assert.Equal(3, DailyCaps.For(subject with { DailyCap = null }, 11, 5));
        Assert.Equal(7, DailyCaps.For(null, 7, 1));
    }

    [Fact]
    public void ADayThatTheOtherTeachersCannotFillIsAnErrorNamingTheTeachersOff()
    {
        // 3 lessons a day = 15 slots = 8 + 7 lessons, so no slot may stay empty. Math's teacher is off on Sunday and Arabic gives at most 2 a day: Sunday keeps a gap.
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1, lessonsByDay: [3, 3, 3, 3, 3]);
        factory.Assign(section, factory.Line(1, factory.Subject("العربية"), 8), factory.Teacher("رغد"));
        factory.Assign(section, factory.Line(1, factory.Subject("الرياضيات"), 7), factory.Teacher("سلمى", offDays: [Sunday]));
        var report = PreSolveValidator.Validate(factory.Build());
        var finding = Single(report, FindingCodes.SectionDayUnderfilled);
        Assert.Equal((PreSolveValidator.Error, 3, 2), (finding.Severity, finding.Required, finding.Available));
        Assert.Equal(["7", "3"], finding.Details);
        Assert.Equal("سلمى", Assert.Single(finding.Related).Name);
        Assert.False(report.Ready);
    }

    [Fact]
    public void AnEmptySlotIsToleratedWhenTheSectionHasSlackForIt()
    {
        // The same teacher off, but only 14 lessons in 15 slots: one empty slot is possible and Sunday needs 3 - 1 = 2 lessons, which Arabic can give.
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1, lessonsByDay: [3, 3, 3, 3, 3]);
        factory.Assign(section, factory.Line(1, factory.Subject("العربية"), 8), factory.Teacher("رغد"));
        factory.Assign(section, factory.Line(1, factory.Subject("الرياضيات"), 6), factory.Teacher("سلمى", offDays: [Sunday]));
        Assert.False(Has(PreSolveValidator.Validate(factory.Build()), FindingCodes.SectionDayUnderfilled));
    }

    [Fact]
    public void ASlotWithNoFreeTeacherIsAnErrorWhenEverySlotMustBeFilled()
    {
        // 2 lessons a day = 10 slots = 5 + 5 lessons. Both teachers are blocked at Sunday lesson 1: nobody can teach that slot.
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1, lessonsByDay: [2, 2, 2, 2, 2]);
        var blocked = new[] { new SlotRef(Sunday, 1) };
        factory.Assign(section, factory.Line(1, factory.Subject("العربية"), 5), factory.Teacher("رغد", blocked: blocked));
        factory.Assign(section, factory.Line(1, factory.Subject("الرياضيات"), 5), factory.Teacher("سلمى", blocked: blocked));
        var finding = Single(PreSolveValidator.Validate(factory.Build()), FindingCodes.SectionSlotUncovered);
        Assert.Equal((PreSolveValidator.Error, 1, 0), (finding.Severity, finding.Required, finding.Available));
        Assert.Equal(["7", "1"], finding.Details);
        Assert.Equal(section, finding.Entity.Id);
    }

    [Fact]
    public void ASlotWithNoFreeTeacherIsToleratedWithEnoughSlack()
    {
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1, lessonsByDay: [2, 2, 2, 2, 2]);
        var blocked = new[] { new SlotRef(Sunday, 2) };
        factory.Assign(section, factory.Line(1, factory.Subject("العربية"), 5), factory.Teacher("رغد", blocked: blocked));
        factory.Assign(section, factory.Line(1, factory.Subject("الرياضيات"), 4), factory.Teacher("سلمى", blocked: blocked));
        Assert.False(Has(PreSolveValidator.Validate(factory.Build()), FindingCodes.SectionSlotUncovered));
    }

    [Fact]
    public void TwoTeachersOffTheSameDayHoldingMostOfTheSectionIsAWarning()
    {
        // 15 slots, 13 lessons (2 slots may stay empty). Arabic and Math (12 lessons) are both off on Sunday, so only Science (1 lesson) can use that day.
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1, lessonsByDay: [3, 3, 3, 3, 3]);
        factory.Assign(section, factory.Line(1, factory.Subject("العربية"), 6), factory.Teacher("رغد", offDays: [Sunday]));
        factory.Assign(section, factory.Line(1, factory.Subject("الرياضيات"), 6), factory.Teacher("سلمى", offDays: [Sunday]));
        factory.Assign(section, factory.Line(1, factory.Subject("العلوم"), 1), factory.Teacher("هدى"));
        var report = PreSolveValidator.Validate(factory.Build());
        var finding = Single(report, FindingCodes.TeachersSameDayOff);
        Assert.Equal(PreSolveValidator.Warning, finding.Severity);
        Assert.Equal(12, finding.Required);
        Assert.Equal(["رغد", "سلمى"], finding.Related.Select(teacher => teacher.Name));
        Assert.Equal(["7", "13"], finding.Details);
        Assert.True(report.Ready); // a warning never blocks generation
    }

    [Fact]
    public void TwoTeachersOffTheSameDayWithPlentyOfOthersIsClean()
    {
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1, lessonsByDay: [3, 3, 3, 3, 3]);
        factory.Assign(section, factory.Line(1, factory.Subject("العربية"), 3), factory.Teacher("رغد", offDays: [Sunday]));
        factory.Assign(section, factory.Line(1, factory.Subject("الرياضيات"), 3), factory.Teacher("سلمى", offDays: [Sunday]));
        factory.Assign(section, factory.Line(1, factory.Subject("العلوم"), 9), factory.Teacher("هدى"));
        Assert.False(Has(PreSolveValidator.Validate(factory.Build()), FindingCodes.TeachersSameDayOff));
    }

    [Fact]
    public void AFullyStaffedFullSectionStaysClean()
    {
        // The owner's school in miniature: 5 days x 6 lessons, six subjects of 5 lessons, one teacher each, nobody off: no error, no warning.
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1);
        foreach (var name in new[] { "العربية", "الرياضيات", "العلوم", "الإنكليزية", "الإسلامية", "الاجتماعيات" })
            factory.Assign(section, factory.Line(1, factory.Subject(name), 5), factory.Teacher("معلم " + name));
        var report = PreSolveValidator.Validate(factory.Build());
        Assert.Equal((true, 0, 0), (report.Ready, report.Errors, report.Warnings));
    }

    [Fact]
    public void ACompletelyAssignedPlanWithAnUnassignedLineStaysSilentHere()
    {
        // The unassigned line is reported by its own finding; the fill checks do not guess.
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1, lessonsByDay: [2, 2, 2, 2, 2]);
        factory.Assign(section, factory.Line(1, factory.Subject("العربية"), 5), factory.Teacher("رغد", offDays: [Sunday]));
        factory.Line(1, factory.Subject("الرياضيات"), 5);
        var report = PreSolveValidator.Validate(factory.Build());
        Assert.True(Has(report, FindingCodes.UnassignedLines));
        Assert.False(Has(report, FindingCodes.SectionDayUnderfilled) || Has(report, FindingCodes.SubjectDaysShort) || Has(report, FindingCodes.SectionSlotUncovered));
    }
}
