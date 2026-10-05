using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace SmartSchoolTimetable.Application.Scheduling;

/// <summary>
/// The deterministic <c>InputHash</c> (ADR 0034): SHA-256 of a canonical JSON form. Every list is put in a fixed order
/// here (by id, day and lesson), whatever order the rows were read in; values marked <see cref="NotHashedAttribute"/>
/// are left out. Same data → same hash; any change that matters to scheduling → a different hash.
/// </summary>
public static class SchedulingInputHash
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SkipNotHashed } },
    };

    public static string Compute(SchedulingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(Canonical(input), Options);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>The same input with every list in canonical order (working days keep the week order: it matters).</summary>
    public static SchedulingInput Canonical(SchedulingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input with
        {
            Shifts = input.Shifts.OrderBy(shift => shift.Id).Select(shift => shift with
            {
                LessonsByDay = ByDay(shift.LessonsByDay),
                Periods = shift.Periods?.OrderBy(period => period.Position).ToArray(),
            }).ToArray(),
            Sections = input.Sections.OrderBy(section => section.Id).Select(section => section with { AllowedByDay = ByDay(section.AllowedByDay) }).ToArray(),
            Subjects = input.Subjects.OrderBy(subject => subject.Id).Select(subject => subject with { Blocked = Slots(subject.Blocked) }).ToArray(),
            Lines = input.Lines.OrderBy(line => line.Id).ToArray(),
            Assignments = input.Assignments.OrderBy(row => row.SectionId).ThenBy(row => row.LineId).ThenBy(row => row.TeacherId).ToArray(),
            Teachers = input.Teachers.OrderBy(teacher => teacher.Id).Select(teacher => teacher with
            {
                OffDays = teacher.OffDays.Order().ToArray(),
                Blocked = Slots(teacher.Blocked),
                SpecializationIds = teacher.SpecializationIds.Order().ToArray(),
            }).ToArray(),
            Resources = input.Resources.OrderBy(resource => resource.Id).ToArray(),
            Profile = input.Profile with { Rules = input.Profile.Rules.OrderBy(rule => rule.Key, StringComparer.Ordinal).ToArray() },
            Stages = input.Stages?.OrderBy(stage => stage.Id).ToArray(),
        };
    }

    private static DayLessons[] ByDay(IEnumerable<DayLessons> days) => days.OrderBy(day => day.Day).ToArray();

    private static SlotRef[] Slots(IEnumerable<SlotRef> slots) => slots.Distinct().OrderBy(slot => slot.Day).ThenBy(slot => slot.Lesson).ToArray();

    private static void SkipNotHashed(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties.ToArray())
        {
            if (property.AttributeProvider is MemberInfo member && member.IsDefined(typeof(NotHashedAttribute), inherit: true))
                typeInfo.Properties.Remove(property);
        }
    }
}
