using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>Builds small synthetic <see cref="SchedulingInput"/> values for solver tests (test data only, never product data).</summary>
internal sealed class TestSchool
{
    public static readonly int[] Week = [7, 1, 2, 3, 4];

    private readonly List<ShiftInput> _shifts = [];
    private readonly List<SectionInput> _sections = [];
    private readonly List<SubjectInput> _subjects = [];
    private readonly List<LineInput> _lines = [];
    private readonly List<AssignmentInput> _assignments = [];
    private readonly List<TeacherInput> _teachers = [];
    private readonly List<ResourceInput> _resources = [];
    private readonly List<StageInput> _stages = [];
    private List<RuleInput> _rules = SchedulingRuleKeys.Defaults.Select(rule => new RuleInput(rule.Key, rule.Enabled, rule.Weight)).ToList();
    private long _next = 100;

    public int[] Days { get; init; } = Week;

    /// <summary>A shift with real period rows: lessons of <paramref name="minutes"/> from <paramref name="start"/>, an optional break after a lesson.</summary>
    public long Shift(int lessonsPerDay = 6, int start = 480, int minutes = 45, int? breakAfter = null, int breakMinutes = 20)
    {
        var id = _next++;
        var periods = new List<PeriodInput>();
        var time = start;
        var position = 1;
        for (var lesson = 1; lesson <= lessonsPerDay; lesson++)
        {
            periods.Add(new PeriodInput(position++, "Lesson", time, time + minutes, true, true));
            time += minutes;
            if (breakAfter == lesson && lesson < lessonsPerDay)
            {
                periods.Add(new PeriodInput(position++, "Break", time, time + breakMinutes, false, false));
                time += breakMinutes;
            }
        }
        _shifts.Add(new ShiftInput(id, $"دوام {id}", Days.Select(day => new DayLessons(day, lessonsPerDay)).ToArray(), start, time, periods));
        return id;
    }

    public long Stage(string name = "مرحلة")
    {
        var id = _next++;
        _stages.Add(new StageInput(id, $"{name} {id}"));
        return id;
    }

    public long Section(long shift, long stage, int lessonsPerDay = 6, int[]? lessonsByDay = null)
    {
        var id = _next++;
        var counts = lessonsByDay ?? Days.Select(_ => lessonsPerDay).ToArray();
        var stageName = _stages.FirstOrDefault(item => item.Id == stage)?.Name ?? "مرحلة";
        _sections.Add(new SectionInput(id, stage, shift, stageName, $"ش{id}", Days.Select((day, index) => new DayLessons(day, counts[index])).ToArray()));
        return id;
    }

    public long Subject(SlotRef[]? blocked = null, bool doublePeriod = false, long? resource = null, bool heavy = false, bool spread = false)
    {
        var id = _next++;
        _subjects.Add(new SubjectInput(id, $"مادة {id}", 3, true, spread, heavy, doublePeriod, resource, blocked ?? []));
        return id;
    }

    public long Line(long stage, long subject, int lessons, bool doublePeriod = false)
    {
        var id = _next++;
        _lines.Add(new LineInput(id, stage, subject, null, lessons, doublePeriod));
        return id;
    }

    public long Teacher(int? maxPerWeek = null, int? maxPerDay = null, int[]? offDays = null, SlotRef[]? blocked = null, bool released = false)
    {
        var id = _next++;
        _teachers.Add(new TeacherInput(id, $"معلم {id}", false, released, false, offDays ?? [], blocked ?? [], maxPerDay, maxPerWeek, []));
        return id;
    }

    public long Resource(int capacity)
    {
        var id = _next++;
        _resources.Add(new ResourceInput(id, $"مورد {id}", "Laboratory", capacity, false));
        return id;
    }

    public void Assign(long section, long line, long teacher) => _assignments.Add(new AssignmentInput(_next++, section, line, teacher));

    public TestSchool Rules(params (string Key, bool Enabled, int Weight)[] rules)
    {
        _rules = SchedulingRuleKeys.Defaults.Select(rule =>
        {
            var match = rules.FirstOrDefault(item => item.Key == rule.Key);
            return match.Key is null ? new RuleInput(rule.Key, false, 0) : new RuleInput(rule.Key, match.Enabled, match.Weight);
        }).ToList();
        return this;
    }

    public SchedulingInput Build() => SchedulingInputHash.Canonical(new SchedulingInput(
        SchedulingInput.CurrentFormatVersion, 1, Days, _shifts.ToArray(), _sections.ToArray(), _subjects.ToArray(), _lines.ToArray(),
        _assignments.ToArray(), _teachers.ToArray(), _resources.ToArray(), new ProfileInput(1, _rules.ToArray()), _stages.ToArray()));

    public static GenerationSettings Settings(string mode = GenerationModes.Standard, int seconds = 10, int seed = 7, bool deterministic = true, int workers = 1) =>
        new(mode, seconds, seed, workers, deterministic);
}
