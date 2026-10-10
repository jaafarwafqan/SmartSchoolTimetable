using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// A catalogue of intentionally broken timetables (Phase 4 §7): each is caught by the independent verifier with the
/// exact violation, and the valid timetable it is derived from has none.
/// </summary>
public sealed class VerifierTests
{
    private static readonly int D0 = TestSchool.Week[0];
    private static readonly int D1 = TestSchool.Week[1];
    private static readonly int D2 = TestSchool.Week[2];
    private static readonly int D3 = TestSchool.Week[3];
    private static readonly int D4 = TestSchool.Week[4];

    /// <summary>
    /// Two sections (A, B) in one 3-lesson shift. Math 3 a week (T1 in both, off on D4, math blocked on D4 lesson 3);
    /// art 2 a week needing the laboratory (T2 in A, T3 in B). The lessons, by index:
    /// 0–2 A math D0/D1/D2 lesson 1; 3–4 A art D0 lessons 2–3; 5–6 B math D0/D1 lesson 2; 7 B math D3 lesson 1;
    /// 8–9 B art D0/D1 lesson 1.
    /// </summary>
    private sealed class Fixture
    {
        public Fixture(int labCapacity = 1, int? maxPerDay = null, int? maxPerWeek = null, bool doubleArt = false, bool offDay = true)
        {
            var school = new TestSchool();
            var shift = school.Shift(3);
            var stage = school.Stage();
            A = school.Section(shift, stage, 3);
            B = school.Section(shift, stage, 3);
            var lab = school.Resource(labCapacity);
            Math = school.Subject(blocked: [new SlotRef(D4, 3)]);
            var art = school.Subject(resource: lab, doublePeriod: doubleArt);
            MathLine = school.Line(stage, Math, 3);
            ArtLine = school.Line(stage, art, 2);
            T1 = school.Teacher(maxPerWeek, maxPerDay, offDays: offDay ? [D4] : []);
            T2 = school.Teacher();
            T3 = school.Teacher();
            school.Assign(A, MathLine, T1);
            school.Assign(A, ArtLine, T2);
            school.Assign(B, MathLine, T1);
            school.Assign(B, ArtLine, T3);
            Input = school.Build();
            Lessons =
            [
                new(A, MathLine, T1, D0, 1), new(A, MathLine, T1, D1, 1), new(A, MathLine, T1, D2, 1),
                new(A, ArtLine, T2, D0, 2), new(A, ArtLine, T2, D0, 3),
                new(B, MathLine, T1, D0, 2), new(B, MathLine, T1, D1, 2), new(B, MathLine, T1, D3, 1),
                new(B, ArtLine, T3, D0, 1), new(B, ArtLine, T3, D1, 1),
            ];
        }

        public long A { get; }
        public long B { get; }
        public long Math { get; }
        public long MathLine { get; }
        public long ArtLine { get; }
        public long T1 { get; }
        public long T2 { get; }
        public long T3 { get; }
        public SchedulingInput Input { get; }
        public List<PlacedLesson> Lessons { get; }

        public IReadOnlyList<Violation> Verify(bool doubles = false) => TimetableVerifier.Verify(Input, Lessons, doubles);

        public Fixture Move(int index, int day, int lesson)
        {
            Lessons[index] = Lessons[index] with { Day = day, Lesson = lesson };
            return this;
        }
    }

    private static void AssertOnly(IReadOnlyList<Violation> violations, string code, string rule)
    {
        Assert.NotEmpty(violations);
        Assert.All(violations, violation => Assert.Equal((code, rule), (violation.Code, violation.Rule)));
    }

    [Fact]
    public void TheHandMadeTimetableIsValidInBothModes()
    {
        Assert.Empty(new Fixture().Verify());
        Assert.Empty(new Fixture(labCapacity: 2, maxPerDay: 2, maxPerWeek: 6).Verify());
    }

    [Fact]
    public void AMissingLessonIsCaught()
    {
        var fixture = new Fixture();
        fixture.Lessons.RemoveAt(2);
        var violation = Assert.Single(fixture.Verify());
        Assert.Equal((ViolationCodes.WrongLessonCount, "H1", fixture.A, 2, 3), (violation.Code, violation.Rule, violation.SectionId, violation.Count, violation.Limit));
    }

    [Fact]
    public void ALessonOfAnUnknownLineOrOfTheWrongTeacherIsCaught()
    {
        var fixture = new Fixture();
        fixture.Lessons.Add(new PlacedLesson(fixture.A, 999_999, fixture.T1, D3, 1));
        fixture.Lessons.Add(new PlacedLesson(fixture.A, fixture.MathLine, fixture.T3, D3, 2));
        Assert.Equal(2, fixture.Verify().Count(violation => violation is { Code: ViolationCodes.UnknownLesson, Rule: "H1" }));
    }

    [Fact]
    public void TwoLessonsInOneSectionSlotAreCaught()
    {
        var violations = new Fixture().Move(3, D0, 1).Verify();
        Assert.Contains(violations, violation => violation is { Code: ViolationCodes.SectionConflict, Rule: "H2", Lesson: 1, Count: 2 });
    }

    [Fact]
    public void AFreePeriodBetweenLessonsIsCaught()
    {
        var fixture = new Fixture().Move(9, D3, 2);
        var violation = Assert.Single(fixture.Verify());
        Assert.Equal((ViolationCodes.SectionGap, "H3", fixture.B, D1, 1), (violation.Code, violation.Rule, violation.SectionId, violation.Day, violation.Lesson));
    }

    [Fact]
    public void ALessonBeyondTheStageDayIsCaught()
    {
        var fixture = new Fixture().Move(7, D3, 4);
        Assert.Contains(fixture.Verify(), violation => violation is { Code: ViolationCodes.OutsideSectionDay, Rule: "H3", Lesson: 4, Limit: 3 });
    }

    [Fact]
    public void ATeacherInTwoPlacesIsCaught()
    {
        var fixture = new Fixture().Move(6, D1, 1).Move(9, D1, 2);
        var violations = fixture.Verify();
        AssertOnly(violations, ViolationCodes.TeacherConflict, "H4");
        Assert.Equal(fixture.T1, violations[0].TeacherId);
    }

    [Fact]
    public void ATeacherOnAnOffDayIsCaught()
    {
        var fixture = new Fixture().Move(2, D4, 1);
        var violation = Assert.Single(fixture.Verify());
        Assert.Equal((ViolationCodes.TeacherUnavailable, "H5", fixture.T1, D4), (violation.Code, violation.Rule, violation.TeacherId, violation.Day));
    }

    [Fact]
    public void ASubjectInItsBlockedPeriodIsCaught()
    {
        var fixture = new Fixture(offDay: false).Move(2, D4, 3);
        Assert.Contains(fixture.Verify(), violation => violation is { Code: ViolationCodes.SubjectBlocked, Rule: "H6", Lesson: 3 } && violation.SubjectId == fixture.Math);
    }

    [Fact]
    public void TeacherLimitsAreCaught()
    {
        Assert.Contains(new Fixture(maxPerDay: 1).Verify(), violation => violation is { Code: ViolationCodes.TeacherDayLimit, Rule: "H7", Count: 2, Limit: 1 });
        var violation = Assert.Single(new Fixture(maxPerWeek: 5).Verify());
        Assert.Equal((ViolationCodes.TeacherWeekLimit, "H7", 6, 5), (violation.Code, violation.Rule, violation.Count, violation.Limit));
    }

    [Fact]
    public void AResourceAboveCapacityIsCaught()
    {
        // B on D0: art 1, art 2, math 3; A's art is at lesson 2 too: two laboratory lessons at once.
        static Fixture Clash(int capacity) => new Fixture(labCapacity: capacity).Move(9, D0, 2).Move(5, D0, 3);
        Assert.Contains(Clash(1).Verify(), violation => violation is { Code: ViolationCodes.ResourceCapacity, Rule: "H8", Count: 2, Limit: 1 });
        Assert.DoesNotContain(Clash(2).Verify(), violation => violation.Code == ViolationCodes.ResourceCapacity);
    }

    [Fact]
    public void TooManyLessonsOfASubjectInADayAreCaught()
    {
        // A's three math lessons on D0 (cap max(2, ceil(3 ÷ 5)) = 2).
        var fixture = new Fixture().Move(1, D0, 2).Move(2, D0, 3).Move(3, D1, 1).Move(4, D1, 2);
        Assert.Contains(fixture.Verify(), violation => violation is { Code: ViolationCodes.SubjectDailyCap, Rule: "H9", Count: 3, Limit: 2 } && violation.SectionId == fixture.A);
    }

    [Fact]
    public void ABrokenDoubleIsCaughtOnlyInTheDoubleMode()
    {
        var fixture = new Fixture(doubleArt: true);
        Assert.Empty(fixture.Verify(doubles: false));
        var violation = Assert.Single(fixture.Verify(doubles: true));
        Assert.Equal((ViolationCodes.DoublePeriodBroken, "H10", fixture.B, 0, 1), (violation.Code, violation.Rule, violation.SectionId, violation.Count, violation.Limit));
    }

    [Fact]
    public void ADoubleSplitByABreakIsNotAPair()
    {
        var school = new TestSchool();
        var shift = school.Shift(2, breakAfter: 1);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 2);
        var line = school.Line(stage, school.Subject(doublePeriod: true), 2);
        var teacher = school.Teacher();
        school.Assign(section, line, teacher);
        var violations = TimetableVerifier.Verify(school.Build(), [new(section, line, teacher, D0, 1), new(section, line, teacher, D0, 2)], true);
        Assert.Equal(ViolationCodes.DoublePeriodBroken, Assert.Single(violations).Code);
    }

    [Fact]
    public void TeachersInTwoShiftsAreCheckedOnRealTime()
    {
        // 8:00–9:30 and 8:45–10:15: morning lesson 2 and later lesson 1 overlap; morning 1 and later 1 do not.
        var school = new TestSchool();
        var morning = school.Shift(2, start: 480);
        var later = school.Shift(2, start: 525);
        var first = school.Stage();
        var second = school.Stage();
        var a = school.Section(morning, first, 2);
        var b = school.Section(later, second, 2);
        var teacher = school.Teacher();
        var lineA = school.Line(first, school.Subject(), 2);
        var lineB = school.Line(second, school.Subject(), 1);
        school.Assign(a, lineA, teacher);
        school.Assign(b, lineB, teacher);
        var input = school.Build();
        Assert.Empty(TimetableVerifier.Verify(input, [new(a, lineA, teacher, D0, 1), new(a, lineA, teacher, D1, 1), new(b, lineB, teacher, D0, 1)], false));
        var clash = TimetableVerifier.Verify(input, [new(a, lineA, teacher, D0, 1), new(a, lineA, teacher, D0, 2), new(b, lineB, teacher, D0, 1)], false);
        Assert.Equal(ViolationCodes.TeacherConflict, Assert.Single(clash).Code);
    }

    [Fact]
    public void EveryViolationCodeIsUpperSnakeAndUnique()
    {
        Assert.Equal(ViolationCodes.All.Count, ViolationCodes.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ViolationCodes.All, code => Assert.Matches("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+$", code));
    }
}
