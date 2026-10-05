using SmartSchoolTimetable.Application.Scheduling;

namespace SmartSchoolTimetable.Tests.Phase3;

/// <summary>
/// Each pre-solve check with exact numbers (Phase 3 §3, §6), including the owner's examples «المعلم أحمد: المطلوب ٢٨
/// حصة، المتاح ٢٥، ينقص ٣ حصص» and «الفيزياء: المطلوب ٧، المسموح ٥، ينقص ٢» (their Arabic text is rendered by the
/// frontend from these numbers and checked in Vitest), soundness cases, grouping and hash stability.
/// </summary>
public sealed class PreSolveValidatorTests
{
    private static ValidationFinding Single(ValidationReport report, string code) => Assert.Single(report.Findings, finding => finding.Code == code);

    private static (int? Required, int? Available, int? Shortage) Numbers(ValidationFinding finding) => (finding.Required, finding.Available, finding.Shortage);

    /// <summary>A section with one line per subject, every line assigned to the given teacher.</summary>
    private static (InputFactory Factory, long Shift, long Section, long Teacher) OneSection(int lessons, int? maxPerWeek = null, int lessonsPerDay = 6)
    {
        var factory = new InputFactory();
        var shift = factory.Shift(lessonsPerDay);
        var section = factory.Section(shift, 1, lessonsPerDay: lessonsPerDay);
        var teacher = factory.Teacher("أحمد", maxPerWeek);
        factory.Assign(section, factory.Line(1, factory.Subject("الرياضيات"), lessons), teacher);
        return (factory, shift, section, teacher);
    }

    [Fact]
    public void TeacherNeeding28WithOnly25AvailableIsShort3()
    {
        // 30 slots, max 25 a week, 28 lessons assigned: «المعلم أحمد: المطلوب ٢٨ حصة، المتاح ٢٥، ينقص ٣ حصص».
        var (factory, _, _, teacher) = OneSection(28, maxPerWeek: 25);
        var report = PreSolveValidator.Validate(factory.Build());
        var finding = Single(report, FindingCodes.TeacherOverload);
        Assert.Equal(("teacher", teacher, "أحمد"), (finding.Entity.Kind, finding.Entity.Id, finding.Entity.Name));
        Assert.Equal((28, 25, 3), Numbers(finding));
        Assert.Equal(PreSolveValidator.Error, finding.Severity);
        Assert.False(report.Ready);
    }

    [Fact]
    public void PhysicsNeeding7WithOnly5AllowedIsShort2()
    {
        // 5 days × 6 lessons, physics blocked everywhere except lesson 1 each day: «الفيزياء: المطلوب ٧، المسموح ٥، ينقص ٢».
        var factory = new InputFactory();
        var shift = factory.Shift();
        var section = factory.Section(shift, 1);
        var blocked = InputFactory.Week.SelectMany(day => Enumerable.Range(2, 5).Select(lesson => new SlotRef(day, lesson))).ToArray();
        var physics = factory.Subject("الفيزياء", blocked);
        factory.Assign(section, factory.Line(1, physics, 7), factory.Teacher("سعد"));
        var finding = Single(PreSolveValidator.Validate(factory.Build()), FindingCodes.SubjectSlotsShort);
        Assert.Equal(("الفيزياء", 7, 5, 2), (finding.Entity.Name, finding.Required, finding.Available, finding.Shortage));
        Assert.Equal(section, finding.Related.Single().Id);
    }

    [Fact]
    public void AReadySchoolHasNoErrorsAndTheExactCounts()
    {
        var (factory, _, _, _) = OneSection(30);
        var report = PreSolveValidator.Validate(factory.Build());
        Assert.True(report.Ready);
        Assert.Equal((0, 0), (report.Errors, report.Warnings));
    }

    [Fact]
    public void UnassignedLinesAreGroupedPerSection()
    {
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1);
        factory.Line(1, factory.Subject("العربية"), 6);
        factory.Line(1, factory.Subject("الرياضيات"), 5);
        var finding = Single(PreSolveValidator.Validate(factory.Build()), FindingCodes.UnassignedLines);
        Assert.Equal((section, 2), (finding.Entity.Id, finding.Required!.Value));
        Assert.Equal(["العربية", "الرياضيات"], finding.Details);
    }

    [Fact]
    public void SectionCapacityUsesTheStageDayCounts()
    {
        // Stage counts 6,6,6,5,5 = 28 allowed; 30 lessons = over by 2 (error); 26 = under (warning).
        foreach (var (lessons, code, severity) in new[] { (30, FindingCodes.SectionOverCapacity, PreSolveValidator.Error), (26, FindingCodes.SectionUnderCapacity, PreSolveValidator.Warning) })
        {
            var factory = new InputFactory();
            var section = factory.Section(factory.Shift(7), 1, lessonsByDay: [6, 6, 6, 5, 5]);
            factory.Assign(section, factory.Line(1, factory.Subject("العربية"), lessons), factory.Teacher("أحمد"));
            var finding = Single(PreSolveValidator.Validate(factory.Build()), code);
            Assert.Equal((lessons, 28, severity), (finding.Required!.Value, finding.Available!.Value, finding.Severity));
        }
    }

    [Theory]
    [InlineData(new[] { 4 }, 0, null, 24, 25)] // one off day: 4 days × 6 = 24 < 25
    [InlineData(new int[0], 4, null, 26, 27)] // four blocked lessons: 30 − 4 = 26 < 27
    [InlineData(new int[0], 0, 4, 20, 21)] // at most 4 a day: 5 × 4 = 20 < 21
    public void OffDaysBlockedPeriodsAndDailyLimitsLowerAvailability(int[] offDays, int blockedCount, int? maxPerDay, int available, int required)
    {
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1);
        var blocked = Enumerable.Range(1, blockedCount).Select(lesson => new SlotRef(7, lesson)).ToArray();
        var teacher = factory.Teacher("أحمد", maxPerDay: maxPerDay, offDays: offDays, blocked: blocked);
        factory.Assign(section, factory.Line(1, factory.Subject("العربية"), required), teacher);
        var report = PreSolveValidator.Validate(factory.Build());
        Assert.Equal((required, available, 1), Numbers(Single(report, FindingCodes.TeacherOverload)));
        // Exactly the available number fits: no error (soundness at the boundary).
        var fits = new InputFactory();
        var fitSection = fits.Section(fits.Shift(), 1);
        fits.Assign(fitSection, fits.Line(1, fits.Subject("العربية"), available), fits.Teacher("أحمد", maxPerDay: maxPerDay, offDays: offDays, blocked: blocked));
        Assert.DoesNotContain(PreSolveValidator.Validate(fits.Build()).Findings, finding => finding.Severity == PreSolveValidator.Error);
    }

    [Fact]
    public void ReleaseArchiveAndPartialRelease()
    {
        var factory = new InputFactory();
        var shift = factory.Shift();
        var stage = 1;
        var released = factory.Teacher("متفرغ", released: true);
        var archived = factory.Teacher("مؤرشف", archived: true);
        var partial = factory.Teacher("جزئي", partial: true);
        foreach (var teacher in new[] { released, archived, partial })
            factory.Assign(factory.Section(shift, stage, label: teacher.ToString(System.Globalization.CultureInfo.InvariantCulture)), factory.Line(stage, factory.Subject($"مادة {teacher}"), 2), teacher);
        var report = PreSolveValidator.Validate(factory.Build());
        Assert.Equal((released, 0), (Single(report, FindingCodes.TeacherReleased).Entity.Id, Single(report, FindingCodes.TeacherReleased).Available!.Value));
        Assert.Equal(archived, Single(report, FindingCodes.TeacherArchived).Entity.Id);
        Assert.Equal((partial, PreSolveValidator.Warning), (Single(report, FindingCodes.TeacherPartialRelease).Entity.Id, Single(report, FindingCodes.TeacherPartialRelease).Severity));
    }

    [Fact]
    public void ATeachersSectionsInOneShiftShareSlotsAndTwoShiftsAddUp()
    {
        // Two sections in the same shift: the union is still 30 slots (31 lessons = error).
        var same = new InputFactory();
        var morning = same.Shift();
        var teacher = same.Teacher("أحمد");
        same.Assign(same.Section(morning, 1, label: "أ"), same.Line(1, same.Subject("العربية"), 16), teacher);
        same.Assign(same.Section(morning, 2, label: "ب"), same.Line(2, same.Subject("الرياضيات"), 15), teacher);
        Assert.Equal((31, 30, 1), Numbers(Single(PreSolveValidator.Validate(same.Build()), FindingCodes.TeacherOverload)));

        // The same loads in two shifts: 60 slots, no overload (and no overlap warning: 8:00–13:00 and 13:00–18:00).
        var dual = new InputFactory();
        var first = dual.Shift();
        var second = dual.Shift(start: 780, end: 1080, name: "الدوام المسائي");
        var both = dual.Teacher("أحمد");
        dual.Assign(dual.Section(first, 1, label: "أ"), dual.Line(1, dual.Subject("العربية"), 16), both);
        dual.Assign(dual.Section(second, 2, label: "ب"), dual.Line(2, dual.Subject("الرياضيات"), 15), both);
        var report = PreSolveValidator.Validate(dual.Build());
        Assert.DoesNotContain(report.Findings, finding => finding.Code is FindingCodes.TeacherOverload or FindingCodes.TeacherShiftOverlap);
    }

    [Fact]
    public void AssignmentFeasibilityIntersectsSectionTeacherAndSubjectSlots()
    {
        // The teacher is off four days; their section line needs 7 lessons; one day of 6 lessons is left.
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1);
        var teacher = factory.Teacher("أحمد", offDays: [1, 2, 3, 4]);
        factory.Assign(section, factory.Line(1, factory.Subject("العربية"), 7), teacher);
        var finding = Single(PreSolveValidator.Validate(factory.Build()), FindingCodes.AssignmentInfeasible);
        Assert.Equal((7, 6, 1), Numbers(finding));
        Assert.Equal(["section", "subject"], finding.Related.Select(entity => entity.Kind));
    }

    [Fact]
    public void ResourceCapacityIsCheckedPerShift()
    {
        // One lesson a day (5 slots) in the morning; two sections need 3 PE lessons each on a field of capacity 1: 6 > 5.
        var factory = new InputFactory();
        var morning = factory.Shift(1, 480, 525);
        var evening = factory.Shift(1, 780, 825, "الدوام المسائي");
        var field = factory.Resource("الساحة", 1);
        var pe = factory.Subject("الرياضة", resource: field);
        var line = factory.Line(1, pe, 3);
        var coach = factory.Teacher("المدرب");
        var coach2 = factory.Teacher("المدرب الثاني");
        factory.Assign(factory.Section(morning, 1, lessonsPerDay: 1, label: "أ"), line, coach);
        factory.Assign(factory.Section(morning, 1, lessonsPerDay: 1, label: "ب"), line, coach2);
        factory.Assign(factory.Section(evening, 1, lessonsPerDay: 1, label: "ج"), line, coach);
        var report = PreSolveValidator.Validate(factory.Build());
        var finding = Single(report, FindingCodes.ResourceOverCapacity);
        Assert.Equal((6, 5, 1), Numbers(finding));
        Assert.Equal("الدوام الصباحي", finding.Details.Single()); // the evening shift (3 of 5) is fine
    }

    [Fact]
    public void DoublePeriodsNeedConsecutivePairs()
    {
        // 4 lessons in doubles = 2 pairs. Lessons 1–2 and 4–5 on Sunday only: exactly 2 pairs (tight); 1–3 only: 1 pair (error).
        SchedulingInput Build(int[] open)
        {
            var factory = new InputFactory();
            var section = factory.Section(factory.Shift(), 1);
            var blocked = InputFactory.Week.SelectMany(day => Enumerable.Range(1, 6).Where(lesson => day != 7 || !open.Contains(lesson)).Select(lesson => new SlotRef(day, lesson))).ToArray();
            factory.Assign(section, factory.Line(1, factory.Subject("الفنية", blocked, doublePeriod: true), 4), factory.Teacher("أحمد"));
            return factory.Build();
        }
        Assert.Equal((2, 2, PreSolveValidator.Warning), Tuple(Single(PreSolveValidator.Validate(Build([1, 2, 4, 5])), FindingCodes.DoublePeriodTight)));
        Assert.Equal((2, 1, PreSolveValidator.Error), Tuple(Single(PreSolveValidator.Validate(Build([1, 2, 3])), FindingCodes.DoublePeriodImpossible)));
        static (int, int, string) Tuple(ValidationFinding finding) => (finding.Required!.Value, finding.Available!.Value, finding.Severity);
    }

    [Fact]
    public void DoublePeriodCannotCrossABreak()
    {
        var factory = new InputFactory();
        var shift = factory.Shift();
        var section = factory.Section(shift, 1);
        var blocked = InputFactory.Week.SelectMany(day => Enumerable.Range(1, 6)
            .Where(lesson => day != 7 || lesson is not (2 or 3)).Select(lesson => new SlotRef(day, lesson))).ToArray();
        var subject = factory.Subject("العلوم", blocked, doublePeriod: true);
        factory.Assign(section, factory.Line(1, subject, 2), factory.Teacher("أحمد"));
        var baseline = factory.Build();
        var input = baseline with
        {
            Shifts = baseline.Shifts.Select(row => row with
            {
                Periods = [
                    new PeriodInput(1, "Lesson", 480, 520, true, true), new PeriodInput(2, "Lesson", 520, 560, true, true),
                    new PeriodInput(3, "Break", 560, 575, false, false), new PeriodInput(4, "Lesson", 575, 615, true, true),
                    new PeriodInput(5, "Lesson", 615, 655, true, true), new PeriodInput(6, "Lesson", 655, 695, true, true),
                    new PeriodInput(7, "Lesson", 695, 735, true, true),
                ],
            }).ToArray(),
        };

        var finding = Single(PreSolveValidator.Validate(input), FindingCodes.DoublePeriodImpossible);
        Assert.Equal((1, 0, 1), (finding.Required!.Value, finding.Available!.Value, finding.Shortage!.Value));
    }

    [Fact]
    public void ConsistencyWarnings()
    {
        var factory = new InputFactory();
        var morning = factory.Shift(6, 480, 780);
        var overlapping = factory.Shift(6, 720, 1020, "الدوام المسائي"); // 12:00 starts before 13:00
        var teacher = factory.Teacher("أحمد", blocked: [new SlotRef(7, 9)]); // lesson 9 does not exist: orphan
        var noDistribution = factory.Subject("الحاسوب", distribution: false);
        factory.Assign(factory.Section(morning, 1, label: "أ"), factory.Line(1, noDistribution, 2), teacher);
        factory.Assign(factory.Section(overlapping, 2, label: "ب"), factory.Line(2, factory.Subject("العربية"), 2), teacher);
        factory.Section(morning, 3, label: "ج", stageName: "الثاني المتوسط"); // a stage without curriculum
        var report = PreSolveValidator.Validate(factory.Build());
        Assert.Equal(1, Single(report, FindingCodes.OrphanBlockedPeriods).Required);
        Assert.Equal("الحاسوب", Single(report, FindingCodes.DistributionDisabled).Entity.Name);
        Assert.Equal("الثاني المتوسط", Single(report, FindingCodes.StageWithoutCurriculum).Entity.Name);
        Assert.Equal(2, Single(report, FindingCodes.TeacherShiftOverlap).Related.Count);
        Assert.All(report.Findings.Where(finding => finding.Code != FindingCodes.SectionUnderCapacity), finding => Assert.Equal(PreSolveValidator.Warning, finding.Severity));
    }

    [Fact]
    public void StageWithoutSectionsIsStillReportedWhenTheSnapshotContainsIt()
    {
        var factory = new InputFactory();
        var input = factory.Build() with { Stages = [new StageInput(42, "الخامس العلمي")] };
        var finding = Single(PreSolveValidator.Validate(input), FindingCodes.StageWithoutCurriculum);
        Assert.Equal((42, "الخامس العلمي", PreSolveValidator.Warning), (finding.Entity.Id, finding.Entity.Name, finding.Severity));
    }

    [Fact]
    public void IdenticalSectionsAreReportedOnceWithAllOfThemRelated()
    {
        var factory = new InputFactory();
        var shift = factory.Shift();
        var physics = factory.Subject("الفيزياء", InputFactory.Week.SelectMany(day => Enumerable.Range(2, 5).Select(lesson => new SlotRef(day, lesson))).ToArray());
        var line = factory.Line(1, physics, 7);
        var teacher = factory.Teacher("سعد");
        foreach (var label in new[] { "أ", "ب", "ج" })
            factory.Assign(factory.Section(shift, 1, label: label), line, teacher);
        var finding = Single(PreSolveValidator.Validate(factory.Build()), FindingCodes.SubjectSlotsShort);
        Assert.Equal(3, finding.Related.Count);
    }

    [Fact]
    public void TheHashIsStableAndChangesWithEveryRelevantValue()
    {
        var (factory, _, _, _) = OneSection(20, maxPerWeek: 24);
        var input = factory.Build();
        var hash = SchedulingInputHash.Compute(input);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, SchedulingInputHash.Compute(factory.Build())); // same data
        var reordered = input with
        {
            Teachers = input.Teachers.Reverse().ToArray(),
            Sections = input.Sections.Reverse().ToArray(),
            Shifts = input.Shifts.Select(shift => shift with { LessonsByDay = shift.LessonsByDay.Reverse().ToArray() }).ToArray(),
            Profile = input.Profile with { Rules = input.Profile.Rules.Reverse().ToArray() },
            Assignments = input.Assignments.Select(row => row with { Id = row.Id + 1000 }).ToArray(), // assignment row ids do not matter
        };
        Assert.Equal(hash, SchedulingInputHash.Compute(reordered));
        Assert.Equal(hash, SchedulingInputHash.Compute(input with { Teachers = input.Teachers.Select(teacher => teacher with { Name = "اسم آخر" }).ToArray() }));
        var withStages = input with { Stages = [new StageInput(44, "المرحلة الأولى"), new StageInput(45, "المرحلة الثانية")] };
        Assert.Equal(SchedulingInputHash.Compute(withStages), SchedulingInputHash.Compute(withStages with
        {
            Stages = [new StageInput(45, "اسم مختلف"), new StageInput(44, "اسم آخر")],
        }));
        var withPeriods = input with { Shifts = input.Shifts.Select(shift => shift with
        {
            Periods = [new PeriodInput(1, "Lesson", 480, 520, true, true), new PeriodInput(2, "Break", 520, 535, false, false)],
        }).ToArray() };

        var changes = new[]
        {
            input with { Lines = input.Lines.Select(line => line with { WeeklyLessons = 21 }).ToArray() },
            input with { Teachers = input.Teachers.Select(teacher => teacher with { MaxPerWeek = 23 }).ToArray() },
            input with { Teachers = input.Teachers.Select(teacher => teacher with { Blocked = [new SlotRef(7, 1)] }).ToArray() },
            input with { Sections = input.Sections.Select(section => section with { AllowedByDay = section.AllowedByDay.Select(day => day with { Lessons = 5 }).ToArray() }).ToArray() },
            input with { Profile = input.Profile with { ProfileVersion = 2 } },
            input with { Profile = input.Profile with { Rules = input.Profile.Rules.Select(rule => rule with { Weight = rule.Weight + 1 }).ToArray() } },
            input with { WorkingDays = input.WorkingDays.Skip(1).ToArray() },
            input with { Stages = [new StageInput(44, "مرحلة جديدة")] },
            withPeriods with { Shifts = withPeriods.Shifts.Select(shift => shift with
                { Periods = shift.Periods!.Select(period => period.Position == 1 ? period with { StartMinute = 485 } : period).ToArray() }).ToArray() },
        };
        Assert.All(changes, changed => Assert.NotEqual(hash, SchedulingInputHash.Compute(changed)));
        Assert.Equal(changes.Length, changes.Select(SchedulingInputHash.Compute).Distinct().Count());
    }
}
