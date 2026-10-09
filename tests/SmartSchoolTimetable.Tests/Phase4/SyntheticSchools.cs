using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// Synthetic schools built inside the test project only (the product has no demo data). Two kinds:
/// <see cref="FromRandomTimetable"/> builds a valid timetable FIRST and derives an input it satisfies (property tests);
/// <see cref="Realistic"/> builds a school of a given size for the measurements (docs/PERFORMANCE.md).
/// </summary>
internal static class SyntheticSchools
{
    /// <summary>A random small school and a witness timetable that satisfies every hard constraint of it.</summary>
    public static (SchedulingInput Input, IReadOnlyList<PlacedLesson> Witness) FromRandomTimetable(int seed)
    {
        var random = new Random(seed);
        var school = new TestSchool();
        var days = TestSchool.Week;
        var shiftCount = random.Next(1, 3);
        var shifts = new List<(long Id, int Lessons, int Start)>();
        for (var index = 0; index < shiftCount; index++)
        {
            var lessons = random.Next(4, 7);
            // The second shift may overlap the first in time: teachers working in both must not clash.
            var start = index == 0 ? 480 : 480 + random.Next(2, 6) * 45;
            shifts.Add((school.Shift(lessons, start, breakAfter: random.Next(2) == 0 ? 3 : null), lessons, start));
        }

        var witness = new List<PlacedLesson>();
        var teacherBusy = new Dictionary<long, List<(int Day, int Start, int End, long Shift, int Lesson)>>();
        var subjectUse = new Dictionary<long, HashSet<(int Day, int Lesson)>>();
        var stageCount = random.Next(1, 4);
        var subjectPool = Enumerable.Range(0, 6).Select(_ => school.Subject(heavy: random.Next(3) == 0, spread: random.Next(2) == 0)).ToArray();
        var teacherPlan = new List<(long Section, long Line, long Shift, List<(int Day, int Lesson)> Slots)>();

        for (var stageIndex = 0; stageIndex < stageCount; stageIndex++)
        {
            var stage = school.Stage();
            var shift = shifts[random.Next(shifts.Count)];
            var allowed = days.Select(_ => random.Next(Math.Max(2, shift.Lessons - 2), shift.Lessons + 1)).ToArray();
            var capacity = allowed.Sum();
            var total = random.Next(Math.Max(4, capacity / 2), capacity + 1);
            // Curriculum: 2–5 subjects whose weekly lessons add up to total.
            var subjectCount = Math.Min(subjectPool.Length, random.Next(2, 6));
            var subjects = subjectPool.OrderBy(_ => random.Next()).Take(subjectCount).ToArray();
            var weekly = new int[subjectCount];
            for (var lesson = 0; lesson < total; lesson++)
                weekly[random.Next(subjectCount)]++;
            var lines = subjects.Select((subject, index) => (Subject: subject, Weekly: weekly[index])).Where(item => item.Weekly > 0)
                .Select(item => (item.Subject, item.Weekly, Id: school.Line(stage, item.Subject, item.Weekly))).ToArray();
            var sectionCount = random.Next(1, 4);
            for (var sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
            {
                var section = school.Section(shift.Id, stage, lessonsByDay: allowed);
                var slots = Pack(random, allowed, lines.Select(line => (line.Id, line.Weekly)).ToArray(), days.Length);
                if (slots is null)
                    return FromRandomTimetable(seed + 7919);
                foreach (var line in lines)
                {
                    var mine = slots.Where(slot => slot.Line == line.Id).Select(slot => (Day: days[slot.DayIndex], slot.Lesson)).ToList();
                    teacherPlan.Add((section, line.Id, shift.Id, mine));
                    foreach (var slot in mine)
                    {
                        if (!subjectUse.TryGetValue(line.Subject, out var used))
                            subjectUse[line.Subject] = used = [];
                        used.Add(slot);
                    }
                }
            }
        }

        // Teachers: reuse one with no clash in real time, otherwise hire a new one.
        var input = school.Build();
        var times = input.Shifts.ToDictionary(shift => shift.Id, shift => shift.Periods!.Where(period => period.Kind == "Lesson").OrderBy(period => period.Position).ToArray());
        var teacherOf = new Dictionary<(long Section, long Line), long>();
        var teacherIds = new List<long>();
        foreach (var plan in teacherPlan.OrderBy(_ => random.Next()))
        {
            var intervals = plan.Slots.Select(slot => (slot.Day, times[plan.Shift][slot.Lesson - 1].StartMinute, times[plan.Shift][slot.Lesson - 1].EndMinute, plan.Shift, slot.Lesson)).ToArray();
            long? chosen = null;
            foreach (var candidate in teacherIds.OrderBy(_ => random.Next()))
            {
                if (teacherBusy[candidate].Any(busy => intervals.Any(item => item.Day == busy.Day
                        && (item.Shift == busy.Shift ? item.Lesson == busy.Lesson : item.StartMinute < busy.End && busy.Start < item.EndMinute))))
                    continue;
                chosen = candidate;
                break;
            }
            if (chosen is null)
            {
                chosen = 10_000 + teacherIds.Count;
                teacherIds.Add(chosen.Value);
                teacherBusy[chosen.Value] = [];
            }
            teacherBusy[chosen.Value].AddRange(intervals.Select(item => (item.Day, item.StartMinute, item.EndMinute, item.Shift, item.Lesson)));
            teacherOf[(plan.Section, plan.Line)] = chosen.Value;
        }

        // Rebuild with the teachers, their limits and restrictions derived from the witness.
        var final = new TestSchoolRebuild(input, random);
        foreach (var teacher in teacherIds)
        {
            var busy = teacherBusy[teacher];
            var usedDays = busy.Select(item => item.Day).ToHashSet();
            var offDays = days.Where(day => !usedDays.Contains(day) && random.Next(2) == 0).ToArray();
            var freeSlots = days.Where(day => !offDays.Contains(day)).SelectMany(day => Enumerable.Range(1, 6).Select(lesson => new SlotRef(day, lesson)))
                .Where(slot => !busy.Any(item => item.Day == slot.Day && item.Lesson == slot.Lesson)).OrderBy(_ => random.Next()).Take(random.Next(0, 3)).ToArray();
            var perDay = busy.GroupBy(item => item.Day).Select(group => group.Count()).DefaultIfEmpty(0).Max();
            final.AddTeacher(teacher, busy.Count + random.Next(0, 3), random.Next(2) == 0 ? perDay : null, offDays, freeSlots);
        }
        foreach (var ((section, line), teacher) in teacherOf)
            final.Assign(section, line, teacher);
        foreach (var (subject, used) in subjectUse)
        {
            var free = days.SelectMany(day => Enumerable.Range(1, 6).Select(lesson => (day, lesson))).Where(slot => !used.Contains(slot)).OrderBy(_ => random.Next()).Take(random.Next(0, 3));
            final.BlockSubject(subject, free.Select(slot => new SlotRef(slot.day, slot.lesson)).ToArray());
        }
        foreach (var plan in teacherPlan)
            witness.AddRange(plan.Slots.Select(slot => new PlacedLesson(plan.Section, plan.Line, teacherOf[(plan.Section, plan.Line)], slot.Day, slot.Lesson)));
        return (final.Build(), witness);
    }

    /// <summary>
    /// Fills each day's first k lessons (k chosen so the days add up to the curriculum) with the lines' lessons, at most
    /// max(2, ceil(weekly ÷ days)) of a line per day. Null when the random packing fails (the caller retries).
    /// </summary>
    private static List<(long Line, int DayIndex, int Lesson)>? Pack(Random random, int[] allowed, (long Id, int Weekly)[] lines, int dayCount)
    {
        var total = lines.Sum(line => line.Weekly);
        var lengths = new int[allowed.Length];
        for (var placed = 0; placed < total; placed++)
        {
            var open = Enumerable.Range(0, allowed.Length).Where(day => lengths[day] < allowed[day]).ToArray();
            if (open.Length == 0)
                return null;
            lengths[open[random.Next(open.Length)]]++;
        }
        var remaining = lines.ToDictionary(line => line.Id, line => line.Weekly);
        var caps = lines.ToDictionary(line => line.Id, line => Math.Max(2, (line.Weekly + dayCount - 1) / dayCount));
        var result = new List<(long, int, int)>();
        for (var day = 0; day < allowed.Length; day++)
        {
            var today = new Dictionary<long, int>();
            for (var lesson = 1; lesson <= lengths[day]; lesson++)
            {
                var options = remaining.Where(item => item.Value > 0 && today.GetValueOrDefault(item.Key) < caps[item.Key]).Select(item => item.Key).ToArray();
                if (options.Length == 0)
                    return null;
                // Lines with more lessons left go first, so the last days are not left with one line only.
                var line = options.OrderByDescending(id => remaining[id] + random.NextDouble()).First();
                remaining[line]--;
                today[line] = today.GetValueOrDefault(line) + 1;
                result.Add((line, day, lesson));
            }
        }
        return remaining.Values.All(count => count == 0) ? result : null;
    }

    /// <summary>Copies an input and adds teachers, assignments and subject blocks (records are immutable).</summary>
    private sealed class TestSchoolRebuild(SchedulingInput input, Random random)
    {
        private readonly List<TeacherInput> _teachers = [];
        private readonly List<AssignmentInput> _assignments = [];
        private readonly Dictionary<long, SlotRef[]> _blocks = [];
        private long _next = 50_000;

        public void AddTeacher(long id, int maxPerWeek, int? maxPerDay, int[] offDays, SlotRef[] blocked) =>
            _teachers.Add(new TeacherInput(id, $"معلم {id}", false, false, false, offDays, blocked, maxPerDay, random.Next(2) == 0 ? maxPerWeek : null, []));

        public void Assign(long section, long line, long teacher) => _assignments.Add(new AssignmentInput(_next++, section, line, teacher));

        public void BlockSubject(long subject, SlotRef[] blocked) => _blocks[subject] = blocked;

        public SchedulingInput Build() => SchedulingInputHash.Canonical(input with
        {
            Teachers = _teachers.ToArray(),
            Assignments = _assignments.ToArray(),
            Subjects = input.Subjects.Select(subject => _blocks.TryGetValue(subject.Id, out var blocked) ? subject with { Blocked = blocked } : subject).ToArray(),
        });
    }

    /// <summary>
    /// A school of <paramref name="sections"/> sections (3 stages per 12 sections, one shift of 6 lessons × 5 days,
    /// 28 lessons a week per section) staffed by <paramref name="teachers"/> subject teachers. It has heavy and spread
    /// subjects, a laboratory subject, a double-period subject, off days, blocked periods and daily limits.
    /// </summary>
    public static SchedulingInput Realistic(int sections, int teachers, int seed)
    {
        var random = new Random(seed);
        var school = new TestSchool();
        var shift = school.Shift(6, breakAfter: 3);
        if (teachers < 9)
            throw new ArgumentOutOfRangeException(nameof(teachers), "One teacher per subject at least (nine subjects).");
        var lab = school.Resource(Math.Max(1, sections / 12));
        // Weekly lessons per subject (28 in all) and its flags.
        var plan = new (int Weekly, bool Heavy, bool Spread, bool Lab, bool Double)[]
        {
            (6, true, true, false, false), (5, true, true, false, false), (4, false, true, false, false), (3, false, true, false, false),
            (3, false, false, false, false), (2, false, false, true, false), (2, false, false, false, true), (2, false, false, false, false), (1, false, false, false, false),
        };
        var subjects = plan.Select(item => school.Subject(heavy: item.Heavy, spread: item.Spread, resource: item.Lab ? lab : null, doublePeriod: item.Double)).ToArray();
        var stageCount = Math.Max(1, sections / 4);
        var stages = Enumerable.Range(0, stageCount).Select(_ => school.Stage()).ToArray();
        var lines = stages.ToDictionary(stage => stage, stage => subjects.Select((subject, index) => school.Line(stage, subject, plan[index].Weekly)).ToArray());
        var sectionIds = Enumerable.Range(0, sections).Select(index => (Id: school.Section(shift, stages[index % stageCount]), Stage: stages[index % stageCount])).ToArray();

        // Teachers per subject in proportion to its lessons (largest remainder, at least one each).
        var totals = plan.Select(item => item.Weekly * sections).ToArray();
        var all = totals.Sum();
        var shares = totals.Select(total => Math.Max(1, (int)Math.Floor((double)total * teachers / all))).ToArray();
        while (shares.Sum() < teachers)
            shares[Enumerable.Range(0, shares.Length).OrderByDescending(index => (double)totals[index] / shares[index]).First()]++;
        while (shares.Sum() > teachers)
            shares[Enumerable.Range(0, shares.Length).Where(index => shares[index] > 1).OrderBy(index => (double)totals[index] / shares[index]).First()]--;

        for (var subject = 0; subject < subjects.Length; subject++)
        {
            var load = (double)totals[subject] / shares[subject];
            var staff = Enumerable.Range(0, shares[subject]).Select(_ =>
            {
                // Light loads get an off day and blocked periods; heavier ones only a daily limit.
                var offDays = load <= 18 && random.Next(3) == 0 ? new[] { TestSchool.Week[random.Next(5)] } : [];
                var blocked = Enumerable.Range(0, random.Next(0, 3)).Select(_ => new SlotRef(TestSchool.Week[random.Next(5)], random.Next(1, 7))).Distinct().ToArray();
                return school.Teacher(maxPerDay: load > 20 ? 6 : 5, offDays: offDays, blocked: blocked);
            }).ToArray();
            for (var index = 0; index < sectionIds.Length; index++)
            {
                var (section, stage) = sectionIds[index];
                school.Assign(section, lines[stage][subject], staff[index * staff.Length / sectionIds.Length]);
            }
        }
        return school.Build();
    }
}
