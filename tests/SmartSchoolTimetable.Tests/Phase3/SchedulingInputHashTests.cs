using SmartSchoolTimetable.Application.Scheduling;

namespace SmartSchoolTimetable.Tests.Phase3;

/// <summary>
/// InputHash on full generated schools (several shifts, sections, subjects, teachers, resources): the order rows are
/// read in and display names never matter; every scheduling-relevant value does (Phase 3 §6, C4).
/// </summary>
public sealed class SchedulingInputHashTests
{
    private static SchedulingInput School(int seed)
    {
        // A seed whose school has at least two of every list, so reordering is really tested.
        for (var candidate = seed; ; candidate++)
        {
            var input = ValidatorPropertyTests.Generate(candidate).Input;
            if (input.Shifts.Count > 1 && input.Sections.Count > 1 && input.Teachers.Count > 1 && input.Resources.Count > 0 && input.Assignments.Count > 1)
                return input;
        }
    }

    private static SchedulingInput Reversed(SchedulingInput input) => input with
    {
        Shifts = input.Shifts.Reverse().Select(shift => shift with { LessonsByDay = shift.LessonsByDay.Reverse().ToArray() }).ToArray(),
        Sections = input.Sections.Reverse().Select(section => section with { AllowedByDay = section.AllowedByDay.Reverse().ToArray() }).ToArray(),
        Subjects = input.Subjects.Reverse().Select(subject => subject with { Blocked = subject.Blocked.Reverse().ToArray() }).ToArray(),
        Lines = input.Lines.Reverse().ToArray(),
        Assignments = input.Assignments.Reverse().ToArray(),
        Teachers = input.Teachers.Reverse().Select(teacher => teacher with
        {
            OffDays = teacher.OffDays.Reverse().ToArray(),
            Blocked = teacher.Blocked.Reverse().ToArray(),
            SpecializationIds = teacher.SpecializationIds.Reverse().ToArray(),
        }).ToArray(),
        Resources = input.Resources.Reverse().ToArray(),
        Profile = input.Profile with { Rules = input.Profile.Rules.Reverse().ToArray() },
    };

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(42)]
    public void ReadingOrderAndNamesDoNotChangeTheHash(int seed)
    {
        var input = School(seed);
        var hash = SchedulingInputHash.Compute(input);
        Assert.Equal(hash, SchedulingInputHash.Compute(Reversed(input)));
        var renamed = input with
        {
            Teachers = input.Teachers.Select(teacher => teacher with { Name = $"اسم {teacher.Id}" }).ToArray(),
            Subjects = input.Subjects.Select(subject => subject with { Name = "مادة أخرى" }).ToArray(),
            Sections = input.Sections.Select(section => section with { StageName = "مرحلة أخرى", Label = "ي" }).ToArray(),
            Resources = input.Resources.Select(resource => resource with { Name = "مورد" }).ToArray(),
        };
        Assert.Equal(hash, SchedulingInputHash.Compute(renamed));
    }

    [Fact]
    public void ResourceCapacityAndAssignmentsChangeTheHash()
    {
        var input = School(1);
        var hash = SchedulingInputHash.Compute(input);
        var capacity = input with { Resources = input.Resources.Select((resource, index) => index == 0 ? resource with { Capacity = resource.Capacity + 1 } : resource).ToArray() };
        var other = input.Teachers.First(teacher => teacher.Id != input.Assignments[0].TeacherId).Id;
        var reassigned = input with { Assignments = input.Assignments.Select((row, index) => index == 0 ? row with { TeacherId = other } : row).ToArray() };
        var removed = input with { Assignments = input.Assignments.Skip(1).ToArray() };
        Assert.Equal(4, new[] { hash, SchedulingInputHash.Compute(capacity), SchedulingInputHash.Compute(reassigned), SchedulingInputHash.Compute(removed) }.Distinct().Count());
    }
}
