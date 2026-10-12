using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Infrastructure.Solver;
using SmartSchoolTimetable.Tests.Phase3;
using SmartSchoolTimetable.Tests.Phase4;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>The owner's daily limit of a subject reaches the solver and the verifier (H9), not only the validator.</summary>
[Collection(SerialSolverRuns.Name)]
public sealed class DailyCapSolverTests
{
    private static SmartSchoolTimetable.Application.Scheduling.SchedulingInput School(int? arabicCap, int? mathCap)
    {
        // 5 days of 3 lessons = 15 slots = Arabic 10 + Math 5. Arabic's teacher is off on Sunday: with the automatic limit (2 a day) Arabic fits on at most 4 days = 8 lessons.
        var factory = new InputFactory();
        var section = factory.Section(factory.Shift(), 1, lessonsByDay: [3, 3, 3, 3, 3]);
        factory.Assign(section, factory.Line(1, factory.Subject("العربية", dailyCap: arabicCap), 10), factory.Teacher("رغد", offDays: [7]));
        factory.Assign(section, factory.Line(1, factory.Subject("الرياضيات", dailyCap: mathCap), 5), factory.Teacher("سلمى"));
        return factory.Build();
    }

    [Fact]
    public async Task WithoutTheOverrideTheSchoolIsInfeasibleAndWithItATimetableIsFoundAndVerified()
    {
        var settings = TestSchool.Settings(seconds: 5);
        var automatic = await new CpSatSolver().SolveAsync(School(null, null), settings, null, CancellationToken.None);
        Assert.False(automatic.HasTimetable);
        Assert.Equal(SolverStatus.Infeasible, automatic.Status);

        var input = School(3, 3);
        var lessons = await SolverTests.Valid(input, settings);
        Assert.Equal(15, lessons.Count);
        // The verifier uses the same limit: three Arabic lessons on one day pass with the override and fail without it.
        Assert.Empty(TimetableVerifier.Verify(input, lessons, false));
        Assert.Contains(TimetableVerifier.Verify(School(null, 3), lessons, false), violation => violation.Code == ViolationCodes.SubjectDailyCap);
    }
}
