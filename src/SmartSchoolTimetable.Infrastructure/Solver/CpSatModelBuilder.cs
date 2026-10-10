using Google.OrTools.Sat;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Infrastructure.Solver;

/// <summary>Constraint families that can be switched off by an assumption literal (diagnostics only, docs/SOLVER.md §5).</summary>
internal static class Families
{
    public const string TeacherAvailability = "teacherAvailability";
    public const string TeacherLimits = "teacherLimits";
    public const string SectionPacking = "sectionPacking";
    public const string StageDays = "stageDays";
    public const string SubjectBlocked = "subjectBlocked";
    public const string ResourceCapacity = "resourceCapacity";
    public const string SubjectDailyCap = "subjectDailyCap";
    public const string DoublePeriods = "doublePeriods";
    public const string LockedLessons = "lockedLessons";
}

internal sealed record Family(string Kind, long Id, BoolVar Literal);

/// <summary>One assignment line (section, curriculum line, teacher) and its lesson variables by (day, lesson).</summary>
internal sealed class LineVars(AssignmentInput row, LineInput line, SectionInput section, SubjectInput subject, TeacherInput teacher)
{
    public AssignmentInput Row { get; } = row;
    public LineInput Line { get; } = line;
    public SectionInput Section { get; } = section;
    public SubjectInput Subject { get; } = subject;
    public TeacherInput Teacher { get; } = teacher;
    public bool Double => Line.NeedsDoublePeriod || Subject.RequiresDoublePeriod;
    public SortedDictionary<(int Day, int Lesson), BoolVar> Vars { get; } = [];
}

/// <summary>
/// Builds the CP-SAT model (docs/SOLVER.md). Boolean x[line, day, lesson] exists only for allowed slots; H1–H10 are
/// hard, S1–S5 form the weighted objective. In DIAGNOSTIC mode the variables cover every lesson of the shift, each
/// restriction family is enforced only under its own assumption literal, and there is no objective, so the solver can
/// return a core of families that together make the timetable impossible.
/// </summary>
internal sealed class CpSatModelBuilder
{
    private const int EstimatedLessonMinutes = 45;
    private const int DefaultStartMinute = 480;

    private readonly SchedulingInput _input;
    private readonly bool _doubles;
    private readonly bool _diagnostic;
    private readonly Dictionary<long, ShiftInput> _shifts;
    private readonly Dictionary<string, Family> _families = new(StringComparer.Ordinal);

    private readonly IReadOnlyList<PlacedLesson> _locked;

    private CpSatModelBuilder(SchedulingInput input, bool doublePeriodsRequired, bool diagnostic, IReadOnlyList<PlacedLesson> locked)
    {
        _locked = locked;
        _input = input;
        _doubles = doublePeriodsRequired;
        _diagnostic = diagnostic;
        _shifts = input.Shifts.ToDictionary(shift => shift.Id);
    }

    public CpModel Model { get; } = new();
    public List<LineVars> Lines { get; } = [];
    public IReadOnlyCollection<Family> AllFamilies => _families.Values;

    /// <summary>Locked lessons that have no variable (their slot is no longer allowed): not kept, and reported.</summary>
    public int LocksDropped { get; private set; }

    public static CpSatModelBuilder Build(SchedulingInput input, bool doublePeriodsRequired, bool diagnostic, IReadOnlyList<PlacedLesson>? locked = null)
    {
        var builder = new CpSatModelBuilder(input, doublePeriodsRequired, diagnostic, locked ?? []);
        builder.CreateVariables();
        builder.AddLocks();
        builder.AddHardConstraints();
        if (!diagnostic)
            builder.AddObjective();
        return builder;
    }

    public Family? FamilyOf(int literalIndex) => _families.Values.FirstOrDefault(family => family.Literal.GetIndex() == literalIndex);

    private BoolVar? Guard(string kind, long id)
    {
        if (!_diagnostic)
            return null;
        var key = $"{kind}:{id}";
        if (!_families.TryGetValue(key, out var family))
        {
            family = new Family(kind, id, Model.NewBoolVar(key));
            _families.Add(key, family);
        }
        return family.Literal;
    }

    private void Enforce(Constraint constraint, string kind, long id)
    {
        if (Guard(kind, id) is { } literal)
            constraint.OnlyEnforceIf(literal);
    }

    private int ShiftLessons(long shiftId, int day) =>
        _shifts.TryGetValue(shiftId, out var shift) ? shift.LessonsByDay.FirstOrDefault(item => item.Day == day)?.Lessons ?? 0 : 0;

    /// <summary>Clock interval of lesson n in a shift (period rows; an estimate without rows, DECISIONS_PENDING #69).</summary>
    private (int Start, int End) Interval(long shiftId, int lesson)
    {
        if (_shifts.TryGetValue(shiftId, out var shift) && shift.Periods is { Count: > 0 } periods)
        {
            var rows = periods.Where(period => period.Kind == nameof(PeriodKind.Lesson)).OrderBy(period => period.Position).ToArray();
            if (lesson >= 1 && lesson <= rows.Length)
                return (rows[lesson - 1].StartMinute, rows[lesson - 1].EndMinute);
        }
        var start = (shift?.StartMinute) ?? DefaultStartMinute;
        return (start + (lesson - 1) * EstimatedLessonMinutes, start + lesson * EstimatedLessonMinutes);
    }

    /// <summary>Lessons n and n+1 can form a double only when no break row lies between them (in any daily session, R3).</summary>
    private bool Adjacent(long shiftId, int lesson) => !_shifts.TryGetValue(shiftId, out var shift) || shift.Adjacent(lesson);

    private void CreateVariables()
    {
        var lines = _input.Lines.ToDictionary(line => line.Id);
        var sections = _input.Sections.ToDictionary(section => section.Id);
        var subjects = _input.Subjects.ToDictionary(subject => subject.Id);
        var teachers = _input.Teachers.ToDictionary(teacher => teacher.Id);
        foreach (var row in _input.Assignments)
        {
            if (!lines.TryGetValue(row.LineId, out var line) || !sections.TryGetValue(row.SectionId, out var section)
                || !subjects.TryGetValue(line.SubjectId, out var subject) || !teachers.TryGetValue(row.TeacherId, out var teacher)
                || line.StageId != section.StageId)
                continue;
            var vars = new LineVars(row, line, section, subject, teacher);
            foreach (var day in _input.WorkingDays)
            {
                var allowed = section.AllowedByDay.FirstOrDefault(item => item.Day == day)?.Lessons ?? 0;
                var last = _diagnostic ? Math.Max(allowed, ShiftLessons(section.ShiftId, day)) : allowed;
                for (var lesson = 1; lesson <= last; lesson++)
                {
                    var reasons = new List<(string Kind, long Id)>();
                    if (lesson > allowed)
                        reasons.Add((Families.StageDays, section.StageId));
                    if (teacher.Released || teacher.OffDays.Contains(day) || teacher.Blocked.Contains(new SlotRef(day, lesson)))
                        reasons.Add((Families.TeacherAvailability, teacher.Id));
                    if (subject.Blocked.Contains(new SlotRef(day, lesson)))
                        reasons.Add((Families.SubjectBlocked, subject.Id));
                    if (reasons.Count > 0 && !_diagnostic)
                        continue;
                    var x = Model.NewBoolVar($"x_{row.SectionId}_{row.LineId}_{day}_{lesson}");
                    foreach (var (kind, id) in reasons)
                        Model.AddImplication(Guard(kind, id)!, x.Not());
                    vars.Vars[(day, lesson)] = x;
                }
            }
            Lines.Add(vars);
        }
    }

    /// <summary>«إبقاء تعديلاتي»: a locked lesson is a variable fixed to 1. Under diagnostics each section's locks sit behind one assumption literal.</summary>
    private void AddLocks()
    {
        foreach (var locked in _locked)
        {
            var line = Lines.FirstOrDefault(item => item.Row.SectionId == locked.SectionId && item.Row.LineId == locked.LineId && item.Row.TeacherId == locked.TeacherId);
            if (line is null || !line.Vars.TryGetValue((locked.Day, locked.Lesson), out var variable))
            {
                LocksDropped++;
                continue;
            }
            Enforce(Model.Add(variable == 1), Families.LockedLessons, locked.SectionId);
        }
    }

    private void AddHardConstraints()
    {
        // H1 workload: exactly the weekly lessons of every assignment line.
        foreach (var line in Lines)
            Model.Add(LinearExpr.Sum(line.Vars.Values) == line.Line.WeeklyLessons);

        // H2 one lesson per section slot; H3 packing: lesson n+1 is used only when lesson n is.
        foreach (var section in Lines.GroupBy(line => line.Section.Id))
        {
            var bySlot = section.SelectMany(line => line.Vars).GroupBy(item => item.Key).ToDictionary(group => group.Key, group => group.Select(item => item.Value).ToArray());
            foreach (var slot in bySlot.Values.Where(vars => vars.Length > 1))
                Model.AddAtMostOne(slot);
            foreach (var day in bySlot.Keys.Select(key => key.Day).Distinct())
            {
                var last = bySlot.Keys.Where(key => key.Day == day).Max(key => key.Lesson);
                for (var lesson = 1; lesson < last; lesson++)
                {
                    var current = bySlot.GetValueOrDefault((day, lesson)) ?? [];
                    var next = bySlot.GetValueOrDefault((day, lesson + 1)) ?? [];
                    if (next.Length > 0)
                        Enforce(Model.Add(LinearExpr.Sum(next) <= LinearExpr.Sum(current)), Families.SectionPacking, section.Key);
                }
            }
        }

        // H4 teacher conflict on real time (cross-shift), H7 limits.
        foreach (var teacher in Lines.GroupBy(line => line.Teacher.Id))
        {
            var info = teacher.First().Teacher;
            foreach (var day in teacher.SelectMany(line => line.Vars.Select(item => (line.Section.ShiftId, item.Key.Day, item.Key.Lesson, Var: item.Value))).GroupBy(item => item.Day))
            {
                foreach (var clique in Cliques(day.Select(item => (item.ShiftId, item.Lesson, item.Var))))
                {
                    if (clique.Length > 1)
                        Model.AddAtMostOne(clique);
                }
                if (info.MaxPerDay is { } maxDay && day.Count() > maxDay)
                    Enforce(Model.Add(LinearExpr.Sum(day.Select(item => item.Var)) <= maxDay), Families.TeacherLimits, info.Id);
            }
            var all = teacher.SelectMany(line => line.Vars.Values).ToArray();
            if (info.MaxPerWeek is { } maxWeek && all.Length > maxWeek)
                Enforce(Model.Add(LinearExpr.Sum(all) <= maxWeek), Families.TeacherLimits, info.Id);
        }

        // H8 resource capacity on real time.
        foreach (var resource in _input.Resources)
        {
            var users = Lines.Where(line => line.Subject.RequiredResourceId == resource.Id).ToArray();
            foreach (var day in users.SelectMany(line => line.Vars.Select(item => (line.Section.ShiftId, item.Key.Day, item.Key.Lesson, Var: item.Value))).GroupBy(item => item.Day))
            {
                foreach (var clique in Cliques(day.Select(item => (item.ShiftId, item.Lesson, item.Var))).Where(clique => clique.Length > resource.Capacity))
                    Enforce(Model.Add(LinearExpr.Sum(clique) <= resource.Capacity), Families.ResourceCapacity, resource.Id);
            }
        }

        // H9 per (section, subject) and day: at most max(2, ceil(weekly ÷ working days)).
        foreach (var group in Lines.GroupBy(line => (line.Section.Id, line.Subject.Id)))
        {
            var section = group.First().Section;
            var weekly = _input.Lines.Where(line => line.StageId == section.StageId && line.SubjectId == group.Key.Item2).Sum(line => line.WeeklyLessons);
            var days = section.AllowedByDay.Count(day => day.Lessons > 0 && _input.WorkingDays.Contains(day.Day));
            var cap = Math.Max(2, days == 0 ? weekly : (weekly + days - 1) / days);
            foreach (var day in group.SelectMany(line => line.Vars).GroupBy(item => item.Key.Day).Where(day => day.Count() > cap))
                Enforce(Model.Add(LinearExpr.Sum(day.Select(item => item.Value)) <= cap), Families.SubjectDailyCap, group.Key.Item2);
        }

        // H10 («دروس مزدوجة»): x = pair starting here + pair ending here + single; ⌊n ÷ 2⌋ pairs and n mod 2 singles.
        if (_doubles)
        {
            foreach (var line in Lines.Where(line => line.Double && line.Line.WeeklyLessons >= 2))
            {
                var pairs = PairVars(line, "p");
                var singles = new List<BoolVar>();
                foreach (var ((day, lesson), x) in line.Vars)
                {
                    var single = Model.NewBoolVar($"s_{line.Row.SectionId}_{line.Row.LineId}_{day}_{lesson}");
                    singles.Add(single);
                    var covering = LinearExpr.NewBuilder().Add(single);
                    if (pairs.TryGetValue((day, lesson), out var starts))
                        covering.Add(starts);
                    if (pairs.TryGetValue((day, lesson - 1), out var ends))
                        covering.Add(ends);
                    Enforce(Model.Add(x == covering), Families.DoublePeriods, line.Subject.Id);
                }
                Enforce(Model.Add(LinearExpr.Sum(pairs.Values) == line.Line.WeeklyLessons / 2), Families.DoublePeriods, line.Subject.Id);
                Enforce(Model.Add(LinearExpr.Sum(singles) == line.Line.WeeklyLessons % 2), Families.DoublePeriods, line.Subject.Id);
            }
        }
    }

    /// <summary>A pair variable for every adjacent (n, n+1) of one line on one day, keyed by its first lesson.</summary>
    private Dictionary<(int Day, int Lesson), BoolVar> PairVars(LineVars line, string prefix)
    {
        var pairs = new Dictionary<(int Day, int Lesson), BoolVar>();
        foreach (var ((day, lesson), x) in line.Vars)
        {
            if (!line.Vars.TryGetValue((day, lesson + 1), out var next) || !Adjacent(line.Section.ShiftId, lesson))
                continue;
            var pair = Model.NewBoolVar($"{prefix}_{line.Row.SectionId}_{line.Row.LineId}_{day}_{lesson}");
            Model.AddImplication(pair, x);
            Model.AddImplication(pair, next);
            pairs[(day, lesson)] = pair;
        }
        return pairs;
    }

    /// <summary>
    /// Groups of variables that run at one moment: for every lesson start time, all slots whose clock interval contains
    /// it (intervals on a line: these cover every overlap). One shift alone gives one group per lesson number.
    /// </summary>
    private IEnumerable<BoolVar[]> Cliques(IEnumerable<(long ShiftId, int Lesson, BoolVar Var)> slots)
    {
        var bySlot = slots.GroupBy(item => (item.ShiftId, item.Lesson)).ToDictionary(group => group.Key, group => group.Select(item => item.Var).ToArray());
        if (bySlot.Keys.Select(key => key.ShiftId).Distinct().Count() <= 1)
            return bySlot.Values;
        var intervals = bySlot.Keys.ToDictionary(key => key, key => Interval(key.ShiftId, key.Lesson));
        return intervals.Values.Select(interval => interval.Start).Distinct()
            .Select(point => intervals.Where(item => item.Value.Start <= point && point < item.Value.End).Select(item => item.Key).OrderBy(key => key).ToArray())
            .DistinctBy(keys => string.Join(';', keys))
            .Select(keys => keys.SelectMany(key => bySlot[key]).ToArray());
    }

    private int RuleWeight(string key)
    {
        var rule = _input.Profile.Rules.FirstOrDefault(item => item.Key == key);
        var defaults = SchedulingRuleKeys.Defaults.First(item => item.Key == key);
        return (rule?.Enabled ?? defaults.Enabled) ? rule?.Weight ?? defaults.Weight : 0;
    }

    /// <summary>A subject is a double subject in a stage when any of its lines there is taught in double periods.</summary>
    private bool SubjectDouble(long stageId, long subjectId) =>
        _input.Lines.Any(line => line.StageId == stageId && line.SubjectId == subjectId
            && (line.NeedsDoublePeriod || _input.Subjects.Any(subject => subject.Id == subjectId && subject.RequiresDoublePeriod)));

    /// <summary>S1–S5 with the profile weights (docs/SOLVER.md §3); the same definitions as <see cref="TimetableScorer"/>.</summary>
    private void AddObjective()
    {
        var objective = LinearExpr.NewBuilder();
        long constant = 0;

        // S1 spread: lessons of a (section, subject) on one day above 1 (2 for a double subject).
        if (RuleWeight(SchedulingRuleKeys.SpreadSubjectsAcrossDays) is var spreadWeight and > 0)
        {
            foreach (var group in Lines.Where(line => line.Subject.SpreadAcrossDays).GroupBy(line => (line.Section.Id, line.Subject.Id)))
            {
                var allowance = SubjectDouble(group.First().Section.StageId, group.Key.Item2) ? 2 : 1;
                foreach (var day in group.SelectMany(line => line.Vars).GroupBy(item => item.Key.Day).Where(day => day.Count() > allowance))
                {
                    var excess = Model.NewIntVar(0, day.Count() - allowance, $"spread_{group.Key.Item1}_{group.Key.Item2}_{day.Key}");
                    Model.Add(excess >= LinearExpr.Sum(day.Select(item => item.Value)) - allowance);
                    objective.AddTerm(excess, spreadWeight);
                }
            }
        }

        // S2 teacher gaps: free lesson numbers between the first and the last lesson of a (teacher, day, shift).
        if (RuleWeight(SchedulingRuleKeys.AvoidTeacherGaps) is var gapWeight and > 0)
        {
            foreach (var group in Lines.SelectMany(line => line.Vars.Select(item => (line.Teacher.Id, line.Section.ShiftId, item.Key.Day, item.Key.Lesson, Var: item.Value)))
                         .GroupBy(item => (item.Id, item.Day, item.ShiftId)))
            {
                var occupied = group.GroupBy(item => item.Lesson).ToDictionary(slot => slot.Key, slot => slot.Select(item => item.Var).ToArray());
                var first = occupied.Keys.Min();
                var last = occupied.Keys.Max();
                if (last - first < 2)
                    continue;
                LinearExpr At(int lesson) => occupied.TryGetValue(lesson, out var vars) ? LinearExpr.Sum(vars) : LinearExpr.Constant(0);
                var before = new Dictionary<int, BoolVar>();
                var after = new Dictionary<int, BoolVar>();
                for (var lesson = first; lesson <= last; lesson++)
                {
                    var b = Model.NewBoolVar($"gb_{group.Key}_{lesson}");
                    Model.Add(b >= At(lesson));
                    if (lesson > first)
                        Model.Add(b >= before[lesson - 1]);
                    before[lesson] = b;
                }
                for (var lesson = last; lesson >= first; lesson--)
                {
                    var a = Model.NewBoolVar($"ga_{group.Key}_{lesson}");
                    Model.Add(a >= At(lesson));
                    if (lesson < last)
                        Model.Add(a >= after[lesson + 1]);
                    after[lesson] = a;
                }
                for (var lesson = first + 1; lesson < last; lesson++)
                {
                    var gap = Model.NewBoolVar($"gap_{group.Key}_{lesson}");
                    Model.Add(gap >= before[lesson - 1] + after[lesson + 1] - 1 - At(lesson));
                    objective.AddTerm(gap, gapWeight);
                }
            }
        }

        // S3 heavy subjects early: lesson number − 1 for every lesson of a heavy subject.
        if (RuleWeight(SchedulingRuleKeys.HeavySubjectsEarly) is var heavyWeight and > 0)
        {
            foreach (var line in Lines.Where(line => line.Subject.Heavy))
            {
                foreach (var ((_, lesson), x) in line.Vars.Where(item => item.Key.Lesson > 1))
                    objective.AddTerm(x, (long)heavyWeight * (lesson - 1));
            }
        }

        // S4 the same subject in consecutive lessons of a section (double subjects excluded).
        if (RuleWeight(SchedulingRuleKeys.AvoidSameSubjectRepeated) is var repeatWeight and > 0)
        {
            foreach (var group in Lines.GroupBy(line => (line.Section.Id, line.Subject.Id)).Where(group => !SubjectDouble(group.First().Section.StageId, group.Key.Item2)))
            {
                var bySlot = group.SelectMany(line => line.Vars).GroupBy(item => item.Key).ToDictionary(slot => slot.Key, slot => slot.Select(item => item.Value).ToArray());
                foreach (var ((day, lesson), vars) in bySlot)
                {
                    if (!bySlot.TryGetValue((day, lesson + 1), out var next))
                        continue;
                    var repeat = Model.NewBoolVar($"rep_{group.Key}_{day}_{lesson}");
                    Model.Add(repeat >= LinearExpr.Sum(vars) + LinearExpr.Sum(next) - 1);
                    objective.AddTerm(repeat, repeatWeight);
                }
            }
        }

        // S5 (standard mode): ⌊n ÷ 2⌋ minus the adjacent pairs a double line gets.
        if (!_doubles && RuleWeight(SchedulingRuleKeys.KeepDoubleLessonsTogether) is var doubleWeight and > 0)
        {
            foreach (var line in Lines.Where(line => line.Double && line.Line.WeeklyLessons >= 2))
            {
                var target = line.Line.WeeklyLessons / 2;
                var pairs = PairVars(line, "sp");
                foreach (var ((day, lesson), pair) in pairs)
                {
                    if (pairs.TryGetValue((day, lesson + 1), out var next))
                        Model.AddBoolOr([pair.Not(), next.Not()]);
                }
                if (pairs.Count > 0)
                    Model.Add(LinearExpr.Sum(pairs.Values) <= target);
                constant += (long)doubleWeight * target;
                foreach (var pair in pairs.Values)
                    objective.AddTerm(pair, -doubleWeight);
            }
        }

        Model.Minimize(objective.Add(constant));
    }
}
