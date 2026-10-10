using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// Infeasibility diagnostics (Phase 4 §5): schools that PASS the pre-solve validator but cannot be timetabled. The
/// conflict core names exactly the expected entities and no unrelated ones, with fixes and relaxation hints.
/// </summary>
public sealed class DiagnosticsTests
{
    private static async Task<SolverDiagnostics> Explain(SchedulingInput input, GenerationSettings? settings = null)
    {
        var outcome = await SolverTests.Generate(input, settings ?? TestSchool.Settings(seconds: 20));
        Assert.True(outcome.Readiness.Ready, "The validator must pass: " + string.Join(", ", outcome.Readiness.Findings.Where(f => f.Severity == "error").Select(f => f.Code)));
        Assert.Equal(SolverStatus.Infeasible, outcome.Result.Status);
        Assert.NotNull(outcome.Result.Diagnostics);
        Assert.All(outcome.Result.Diagnostics!.Findings, finding => Assert.Contains(finding.Code, DiagnosticCodes.All));
        Assert.All(outcome.Result.Diagnostics.Findings, finding => Assert.NotEmpty(finding.Fixes));
        return outcome.Result.Diagnostics;
    }

    [Fact]
    public async Task AOneTeacherShortOfSlotsBecauseOfPacking()
    {
        // The section has 5 lessons a week, all with T, who cannot teach lesson 1; lessons must start at lesson 1.
        var school = new TestSchool();
        var shift = school.Shift(3);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 3);
        var teacher = school.Teacher(blocked: TestSchool.Week.Select(day => new SlotRef(day, 1)).ToArray());
        school.Assign(section, school.Line(stage, school.Subject(), 5), teacher);
        // An unrelated, healthy section and teacher must not appear.
        var otherStage = school.Stage();
        var other = school.Section(shift, otherStage, 3);
        var unrelated = school.Teacher();
        school.Assign(other, school.Line(otherStage, school.Subject(), 5), unrelated);

        var diagnostics = await Explain(school.Build());
        Assert.True(diagnostics.Minimal);
        Assert.Equal(
            [(DiagnosticCodes.SectionPacking, section), (DiagnosticCodes.TeacherAvailability, teacher)],
            diagnostics.Findings.Select(finding => (finding.Code, finding.Entity.Id)).Order().ToArray());
        Assert.All(diagnostics.Findings, finding => Assert.True(finding.RelaxationHelps));
        Assert.DoesNotContain(diagnostics.Findings, finding => finding.Entity.Id == unrelated || finding.Entity.Id == other);
        var availability = diagnostics.Findings.Single(finding => finding.Code == DiagnosticCodes.TeacherAvailability);
        Assert.Equal((5, 10), (availability.Required, availability.Available));
    }

    [Fact]
    public async Task BTwoTeachersAndASharedLaboratoryWithBlockedPeriods()
    {
        // Two sections of 2 lessons a day; lab science 5 a week by T1 (A) and T2 (B), both blocked at lesson 2:
        // both need the single laboratory at lesson 1 every day.
        var school = new TestSchool();
        var shift = school.Shift(2);
        var stage = school.Stage();
        var lab = school.Resource(1);
        var science = school.Line(stage, school.Subject(resource: lab), 5);
        var reading = school.Line(stage, school.Subject(), 5);
        var lessonTwo = TestSchool.Week.Select(day => new SlotRef(day, 2)).ToArray();
        var t1 = school.Teacher(blocked: lessonTwo);
        var t2 = school.Teacher(blocked: lessonTwo);
        var helpers = new List<long>();
        foreach (var scienceTeacher in new[] { t1, t2 })
        {
            var section = school.Section(shift, stage, 2);
            school.Assign(section, science, scienceTeacher);
            var helper = school.Teacher();
            helpers.Add(helper);
            school.Assign(section, reading, helper);
        }

        var diagnostics = await Explain(school.Build());
        Assert.Equal(
            new[] { (DiagnosticCodes.ResourceCapacity, lab), (DiagnosticCodes.TeacherAvailability, t1), (DiagnosticCodes.TeacherAvailability, t2) }.Order(),
            diagnostics.Findings.Select(finding => (finding.Code, finding.Entity.Id)).Order());
        Assert.DoesNotContain(diagnostics.Findings, finding => helpers.Contains(finding.Entity.Id));
        var resource = diagnostics.Findings.Single(finding => finding.Code == DiagnosticCodes.ResourceCapacity);
        Assert.Equal((10, 1), (resource.Required, resource.Available));
        Assert.Contains("raiseResourceCapacity", resource.Fixes);
        Assert.All(diagnostics.Findings, finding => Assert.True(finding.RelaxationHelps));
    }

    [Fact]
    public async Task CDoublePeriodsThatCannotAllFitInTheDoubleMode()
    {
        // Three lessons a day hold at most one pair a day: two double subjects need 3 + 3 = 6 pairs in 5 days.
        var school = new TestSchool();
        var shift = school.Shift(3);
        var stage = school.Stage();
        var section = school.Section(shift, stage, 3);
        var first = school.Subject(doublePeriod: true);
        var second = school.Subject(doublePeriod: true);
        school.Assign(section, school.Line(stage, first, 6), school.Teacher());
        school.Assign(section, school.Line(stage, second, 6), school.Teacher());
        var input = school.Build();

        var diagnostics = await Explain(input, TestSchool.Settings(GenerationModes.DoublePeriods, seconds: 20));
        Assert.Equal([first, second], diagnostics.Findings.Where(finding => finding.Code == DiagnosticCodes.DoublePeriods).Select(finding => finding.Entity.Id).Order());
        Assert.All(diagnostics.Findings, finding => Assert.Contains(finding.Code, new[] { DiagnosticCodes.DoublePeriods, DiagnosticCodes.SubjectDailyCap, DiagnosticCodes.SectionPacking }));
        // The standard mode only prefers doubles: the same school is fine there.
        Assert.NotEmpty(await SolverTests.Valid(input));
    }

    [Fact]
    public void EveryDiagnosticCodeIsUpperSnakeAndUnique()
    {
        Assert.Equal(DiagnosticCodes.All.Count, DiagnosticCodes.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(DiagnosticCodes.All, code => Assert.Matches("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+$", code));
    }
}
