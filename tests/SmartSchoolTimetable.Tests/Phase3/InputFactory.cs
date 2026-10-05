using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;

namespace SmartSchoolTimetable.Tests.Phase3;

/// <summary>Builds <see cref="SchedulingInput"/> values for validator tests without a database.</summary>
internal sealed class InputFactory
{
    public static readonly int[] Week = [7, 1, 2, 3, 4];

    private readonly List<ShiftInput> _shifts = [];
    private readonly List<SectionInput> _sections = [];
    private readonly List<SubjectInput> _subjects = [];
    private readonly List<LineInput> _lines = [];
    private readonly List<AssignmentInput> _assignments = [];
    private readonly List<TeacherInput> _teachers = [];
    private readonly List<ResourceInput> _resources = [];
    private long _next = 100;

    public long Shift(int lessonsPerDay = 6, int? start = 480, int? end = 780, string name = "الدوام الصباحي")
    {
        var id = _next++;
        _shifts.Add(new ShiftInput(id, name, Week.Select(day => new DayLessons(day, lessonsPerDay)).ToArray(), start, end));
        return id;
    }

    /// <param name="lessonsByDay">The stage's count per working day (week order), or the same count every day.</param>
    public long Section(long shift, long stage, int[]? lessonsByDay = null, int lessonsPerDay = 6, string label = "أ", string stageName = "الأول المتوسط")
    {
        var id = _next++;
        var counts = lessonsByDay ?? Week.Select(_ => lessonsPerDay).ToArray();
        _sections.Add(new SectionInput(id, stage, shift, stageName, label, Week.Select((day, index) => new DayLessons(day, counts[index])).ToArray()));
        return id;
    }

    public long Subject(string name, SlotRef[]? blocked = null, bool doublePeriod = false, long? resource = null, bool distribution = true)
    {
        var id = _next++;
        _subjects.Add(new SubjectInput(id, name, 3, distribution, false, false, doublePeriod, resource, blocked ?? []));
        return id;
    }

    public long Line(long stage, long subject, int lessons, bool doublePeriod = false)
    {
        var id = _next++;
        _lines.Add(new LineInput(id, stage, subject, null, lessons, doublePeriod));
        return id;
    }

    public long Teacher(string name, int? maxPerWeek = null, int? maxPerDay = null, int[]? offDays = null, SlotRef[]? blocked = null,
        bool released = false, bool partial = false, bool archived = false)
    {
        var id = _next++;
        _teachers.Add(new TeacherInput(id, name, archived, released, partial, offDays ?? [], blocked ?? [], maxPerDay, maxPerWeek, []));
        return id;
    }

    public long Resource(string name, int capacity, bool archived = false)
    {
        var id = _next++;
        _resources.Add(new ResourceInput(id, name, "Field", capacity, archived));
        return id;
    }

    public void Assign(long section, long line, long teacher) => _assignments.Add(new AssignmentInput(_next++, section, line, teacher));

    public SchedulingInput Build() => new(
        SchedulingInput.CurrentFormatVersion, 1, Week, _shifts.ToArray(), _sections.ToArray(), _subjects.ToArray(), _lines.ToArray(),
        _assignments.ToArray(), _teachers.ToArray(), _resources.ToArray(),
        new ProfileInput(1, SchedulingRuleKeys.Defaults.Select(rule => new RuleInput(rule.Key, rule.Enabled, rule.Weight)).ToArray()));
}
