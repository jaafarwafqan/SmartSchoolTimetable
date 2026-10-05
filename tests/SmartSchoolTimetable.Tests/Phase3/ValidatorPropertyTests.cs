using FsCheck;
using FsCheck.Xunit;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;

namespace SmartSchoolTimetable.Tests.Phase3;

/// <summary>
/// Property-based soundness of the pre-solve validator (Phase 3 §6, ADR 0035, FsCheck ADR 0033). A random VALID
/// timetable is built first (every lesson placed in a slot, no teacher or section double-booked, double lines in
/// consecutive pairs, resources within capacity); the input is then derived from it with constraints the timetable
/// satisfies (off days, blocked periods, limits, subject blocks, capacities). Such a school can be timetabled, so the
/// validator must report no error. Then one teacher's weekly limit is lowered below their load: exactly that teacher
/// is reported, with the exact shortage.
/// </summary>
public sealed class ValidatorPropertyTests
{
    public const int Runs = 300;

    private sealed record Placed(long Section, long Line, long Teacher, long Shift, int Day, int Lesson);

    internal sealed record Generated(SchedulingInput Input, IReadOnlyDictionary<long, int> Loads);

    /// <summary>Deterministic for a seed: retries with the next random draws until a placement succeeds.</summary>
    internal static Generated Generate(int seed)
    {
        var random = new Random(seed);
        for (var attempt = 0; ; attempt++)
        {
            if (TryGenerate(random) is { } generated)
                return generated;
            if (attempt > 50)
                throw new InvalidOperationException($"No valid timetable for seed {seed}.");
        }
    }

    private static Generated? TryGenerate(Random random)
    {
        var days = InputFactory.Week;
        long next = 1;
        // Shifts: one or two, never overlapping in time.
        var shiftCount = random.Next(1, 3);
        var shifts = Enumerable.Range(0, shiftCount).Select(index =>
        {
            var lessons = random.Next(4, 8);
            var start = index == 0 ? 480 : 780;
            return new ShiftInput(next++, $"دوام {index}", days.Select(day => new DayLessons(day, lessons)).ToArray(), start, start + lessons * 40);
        }).ToArray();
        // Stages with their own day counts (≤ the shift's), and sections in one of the shifts.
        var stages = Enumerable.Range(0, random.Next(1, 4)).Select(_ => next++).ToArray();
        var sections = new List<SectionInput>();
        foreach (var stage in stages)
        {
            var maxLessons = shifts.Min(shift => shift.LessonsByDay[0].Lessons);
            var counts = days.Select(_ => random.Next(Math.Max(1, maxLessons - 2), maxLessons + 1)).ToArray();
            for (var count = random.Next(1, 5); count > 0; count--)
            {
                var shift = shifts[random.Next(shifts.Length)];
                sections.Add(new SectionInput(next++, stage, shift.Id, $"مرحلة {stage}", $"شعبة {count}", days.Select((day, index) => new DayLessons(day, counts[index])).ToArray()));
            }
        }
        // Subjects and lines per stage, the total never above the smallest section's week.
        var subjectIds = Enumerable.Range(0, random.Next(3, 9)).Select(_ => next++).ToArray();
        var lines = new List<LineInput>();
        foreach (var stage in stages)
        {
            var capacity = sections.Where(section => section.StageId == stage).Min(section => section.AllowedByDay.Sum(day => day.Lessons));
            var remaining = capacity - random.Next(0, 4);
            foreach (var subject in subjectIds.OrderBy(_ => random.Next()).Take(random.Next(1, Math.Min(7, subjectIds.Length) + 1)))
            {
                var lessons = Math.Min(random.Next(1, 6), remaining);
                if (lessons <= 0)
                    break;
                remaining -= lessons;
                lines.Add(new LineInput(next++, stage, subject, null, lessons, lessons >= 2 && random.NextDouble() < 0.3));
            }
        }
        if (lines.Count == 0)
            return null;

        // Place every lesson; teachers are reused when free, a new one is added otherwise.
        var teachers = Enumerable.Range(0, random.Next(2, 7)).Select(_ => next++).ToList();
        var placed = new List<Placed>();
        var assignments = new List<AssignmentInput>();
        var teacherBusy = new HashSet<(long Teacher, long Shift, int Day, int Lesson)>();
        foreach (var section in sections)
        {
            var free = section.AllowedByDay.SelectMany(day => Enumerable.Range(1, day.Lessons).Select(lesson => (day.Day, lesson))).ToHashSet();
            foreach (var line in lines.Where(line => line.StageId == section.StageId).OrderByDescending(line => line.NeedsDoublePeriod))
            {
                var candidates = teachers.OrderBy(_ => random.Next()).Append(next).ToArray();
                List<(int Day, int Lesson)>? slots = null;
                long chosen = 0;
                foreach (var teacher in candidates)
                {
                    slots = Choose(random, free, line, slot => !teacherBusy.Contains((teacher, section.ShiftId, slot.Day, slot.Lesson)));
                    if (slots is null)
                        continue;
                    chosen = teacher;
                    break;
                }
                if (slots is null)
                    return null; // even a new teacher cannot take consecutive pairs here: draw again
                if (chosen == next)
                    teachers.Add(next++);
                foreach (var slot in slots)
                {
                    free.Remove(slot);
                    teacherBusy.Add((chosen, section.ShiftId, slot.Day, slot.Lesson));
                    placed.Add(new Placed(section.Id, line.Id, chosen, section.ShiftId, slot.Day, slot.Lesson));
                }
                assignments.Add(new AssignmentInput(next++, section.Id, line.Id, chosen));
            }
        }

        // Derive constraints the timetable satisfies.
        var maxLesson = shifts.Max(shift => shift.LessonsByDay[0].Lessons);
        var allSlots = days.SelectMany(day => Enumerable.Range(1, maxLesson).Select(lesson => new SlotRef(day, lesson))).ToArray();
        var lineSubject = lines.ToDictionary(line => line.Id, line => line.SubjectId);
        var resources = new List<ResourceInput>();
        var subjects = subjectIds.Select(id =>
        {
            var used = placed.Where(item => lineSubject[item.Line] == id).Select(item => new SlotRef(item.Day, item.Lesson)).ToHashSet();
            var blocked = allSlots.Where(slot => !used.Contains(slot) && random.NextDouble() < 0.3).ToArray();
            long? resource = null;
            if (random.NextDouble() < 0.3)
            {
                var peak = placed.Where(item => lineSubject[item.Line] == id).GroupBy(item => (item.Shift, item.Day, item.Lesson)).Select(group => group.Count()).DefaultIfEmpty(0).Max();
                resource = next++;
                resources.Add(new ResourceInput(resource.Value, $"مورد {id}", "Lab", Math.Max(1, peak) + random.Next(0, 2), false));
            }
            return new SubjectInput(id, $"مادة {id}", 3, random.NextDouble() > 0.1, false, false, false, resource, blocked);
        }).ToArray();
        var loads = new Dictionary<long, int>();
        var teacherInputs = teachers.Select(id =>
        {
            var own = placed.Where(item => item.Teacher == id).ToArray();
            loads[id] = own.Length;
            var busyDays = own.Select(item => item.Day).ToHashSet();
            var busySlots = own.Select(item => new SlotRef(item.Day, item.Lesson)).ToHashSet();
            var offDays = days.Where(day => !busyDays.Contains(day) && random.NextDouble() < 0.4).ToArray();
            var blocked = allSlots.Where(slot => !busySlots.Contains(slot) && random.NextDouble() < 0.2).ToArray();
            var perDay = own.GroupBy(item => item.Day).Select(group => group.Count()).DefaultIfEmpty(0).Max();
            int? maxPerDay = random.NextDouble() < 0.5 ? Math.Max(1, perDay) + random.Next(0, 2) : null;
            int? maxPerWeek = random.NextDouble() < 0.5 ? Math.Max(1, own.Length) + random.Next(0, 3) : null;
            return new TeacherInput(id, $"معلم {id}", false, false, false, offDays, blocked, maxPerDay, maxPerWeek, []);
        }).ToArray();
        var profile = new ProfileInput(1, SchedulingRuleKeys.Defaults.Select(rule => new RuleInput(rule.Key, rule.Enabled, rule.Weight)).ToArray());
        var input = new SchedulingInput(SchedulingInput.CurrentFormatVersion, 1, days, shifts, sections, subjects, lines, assignments, teacherInputs, resources, profile);
        return new Generated(input, loads);
    }

    /// <summary>Free slots for one line: pairs on the same day for double lines (the odd lesson single), else any.</summary>
    private static List<(int Day, int Lesson)>? Choose(Random random, HashSet<(int Day, int Lesson)> free, LineInput line, Func<(int Day, int Lesson), bool> teacherFree)
    {
        var open = free.Where(teacherFree).OrderBy(_ => random.Next()).ToList();
        var chosen = new List<(int Day, int Lesson)>();
        if (line.NeedsDoublePeriod)
        {
            for (var pair = 0; pair < line.WeeklyLessons / 2; pair++)
            {
                var start = open.FirstOrDefault(slot => open.Contains((slot.Day, slot.Lesson + 1)));
                if (start == default)
                    return null;
                chosen.Add(start);
                chosen.Add((start.Day, start.Lesson + 1));
                open.Remove(start);
                open.Remove((start.Day, start.Lesson + 1));
            }
        }
        var singles = line.WeeklyLessons - chosen.Count;
        if (open.Count < singles)
            return null;
        chosen.AddRange(open.Take(singles));
        return chosen;
    }

    [Property(MaxTest = Runs)]
    public bool ARandomValidTimetableGivesNoError(PositiveInt seed)
    {
        var input = Generate(seed.Get).Input;
        var report = PreSolveValidator.Validate(input);
        // Generated double lines are placed in real consecutive pairs, so even the «دروس مزدوجة» mode finds no error.
        var doubles = PreSolveValidator.Validate(input, new ValidatorOptions(DoublePeriodsRequired: true));
        return report.Errors == 0 && report.Ready && doubles.Errors == 0;
    }

    [Property(MaxTest = Runs)]
    public bool LoweringOneTeachersLimitBelowTheirLoadReportsExactlyThatTeacher(PositiveInt seed, PositiveInt cut)
    {
        var generated = Generate(seed.Get);
        var busy = generated.Loads.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key).ToArray();
        var (teacherId, load) = busy[seed.Get % busy.Length];
        var shortage = 1 + cut.Get % load; // 1..load
        var input = generated.Input with
        {
            Teachers = generated.Input.Teachers.Select(teacher => teacher.Id == teacherId ? teacher with { MaxPerWeek = load - shortage } : teacher).ToArray(),
        };
        var overloads = PreSolveValidator.Validate(input).Findings.Where(finding => finding.Code == FindingCodes.TeacherOverload).ToArray();
        return overloads.Length == 1
            && overloads[0].Entity.Id == teacherId
            && overloads[0].Required == load
            && overloads[0].Available == load - shortage
            && overloads[0].Shortage == shortage;
    }

    [Fact]
    public void TheGeneratorProducesRealSchools()
    {
        // Guard against a generator that only makes trivial inputs.
        var inputs = Enumerable.Range(1, 50).Select(seed => Generate(seed).Input).ToArray();
        Assert.Contains(inputs, input => input.Shifts.Count == 2);
        Assert.Contains(inputs, input => input.Sections.Count >= 6);
        Assert.Contains(inputs, input => input.Lines.Any(line => line.NeedsDoublePeriod));
        Assert.Contains(inputs, input => input.Resources.Count > 0);
        Assert.Contains(inputs, input => input.Sections.Any(section => section.AllowedByDay.Select(day => day.Lessons).Distinct().Count() > 1)); // per-stage day counts
        Assert.Contains(inputs, input => input.Teachers.Any(teacher => teacher.OffDays.Count > 0 && teacher.Blocked.Count > 0 && teacher.MaxPerDay is not null));
        Assert.All(inputs, input => Assert.Equal(input.Sections.Sum(section => input.Lines.Count(line => line.StageId == section.StageId)), input.Assignments.Count));
    }
}
