namespace SmartSchoolTimetable.Application.Scheduling;

/// <param name="Usable">How many (shift, lesson) slots the teacher could teach in on this day: his sections' lessons that day without his blocked periods; 0 on an off day.</param>
/// <param name="Full">The day has the most usable slots of the week (the teacher is not hampered that day): balanced strictly against the other full days.</param>
/// <param name="Capacity">The most lessons he can teach that day under his daily maximum and the consecutive-lessons rule (an upper bound: every shift counted on its own).</param>
public sealed record TeacherDayInfo(int Day, int Usable, bool Full, int Capacity);

/// <summary>
/// What one teacher's week looks like before any timetable exists (Phase 5 generation rules): the lessons he holds, his usable slots and capacity per
/// day, the days that count as «full» for balancing, and the share each partly-available day may take. The solver, the scorer, the verifier and the
/// readiness checks all read this one definition.
/// </summary>
public sealed class TeacherDays
{
    private TeacherDays(TeacherInput teacher, int load, int? consecutive, IReadOnlyList<TeacherDayInfo> days)
    {
        Teacher = teacher;
        Load = load;
        Consecutive = consecutive;
        Days = days;
    }

    public TeacherInput Teacher { get; }

    /// <summary>Weekly lessons assigned to him.</summary>
    public int Load { get; }

    /// <summary>His «أقصى حصص متتالية» (own or the school's); null: no such rule.</summary>
    public int? Consecutive { get; }

    public IReadOnlyList<TeacherDayInfo> Days { get; }

    /// <summary>Days (in week order) balanced strictly against each other: every day with the most usable slots.</summary>
    public IReadOnlyList<int> FullDays => Days.Where(day => day.Full).Select(day => day.Day).ToArray();

    /// <summary>Total lessons he can teach in the week under his limits (the sum of the daily capacities, also bounded by his weekly maximum).</summary>
    public int Capacity => Math.Min(Teacher.MaxPerWeek ?? int.MaxValue, Days.Sum(day => day.Capacity));

    /// <summary>
    /// The most lessons a partly-available day may take for balance: his load shared out in proportion to the usable slots, rounded up.
    /// Null for a full day or a day without usable slots.
    /// </summary>
    public int? PartialAllowance(int day)
    {
        var info = Days.FirstOrDefault(item => item.Day == day);
        var total = Days.Sum(item => item.Usable);
        if (info is null || info.Full || info.Usable == 0 || total == 0)
            return null;
        return (int)(((long)Load * info.Usable + total - 1) / total);
    }

    /// <summary>The most lessons that fit in a run of <paramref name="length"/> consecutive free lesson numbers when no more than <paramref name="consecutive"/> may follow each other.</summary>
    public static int RunCapacity(int length, int? consecutive) => consecutive is { } k ? length - length / (k + 1) : length;

    /// <summary>All teachers that hold at least one lesson, keyed by teacher id.</summary>
    public static IReadOnlyDictionary<long, TeacherDays> Of(SchedulingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var lines = input.Lines.ToDictionary(line => line.Id);
        var sections = input.Sections.ToDictionary(section => section.Id);
        var result = new Dictionary<long, TeacherDays>();
        foreach (var group in input.Assignments.GroupBy(row => row.TeacherId))
        {
            var teacher = input.Teachers.FirstOrDefault(item => item.Id == group.Key);
            if (teacher is null)
                continue;
            var rows = group.Where(row => lines.ContainsKey(row.LineId) && sections.ContainsKey(row.SectionId)).ToArray();
            var load = rows.Sum(row => lines[row.LineId].WeeklyLessons);
            if (load == 0)
                continue;
            result[teacher.Id] = Build(input, teacher, load, rows.Select(row => sections[row.SectionId]).Distinct().ToArray());
        }
        return result;
    }

    private static TeacherDays Build(SchedulingInput input, TeacherInput teacher, int load, IReadOnlyList<SectionInput> sections)
    {
        var consecutive = input.MaxConsecutiveOf(teacher);
        var days = new List<TeacherDayInfo>();
        foreach (var day in input.WorkingDays)
        {
            var slots = new HashSet<(long Shift, int Lesson)>();
            if (!teacher.Released && !teacher.OffDays.Contains(day))
            {
                foreach (var section in sections)
                {
                    var allowed = section.AllowedByDay.FirstOrDefault(item => item.Day == day)?.Lessons ?? 0;
                    for (var lesson = 1; lesson <= allowed; lesson++)
                    {
                        if (!teacher.Blocked.Contains(new SlotRef(day, lesson)))
                            slots.Add((section.ShiftId, lesson));
                    }
                }
            }
            var capacity = slots.GroupBy(slot => slot.Shift).Sum(shift => Runs(shift.Select(slot => slot.Lesson)).Sum(run => RunCapacity(run, consecutive)));
            days.Add(new TeacherDayInfo(day, slots.Count, false, Math.Min(capacity, teacher.MaxPerDay ?? int.MaxValue)));
        }
        var most = days.Max(day => day.Usable);
        return new TeacherDays(teacher, load, consecutive, days.Select(day => day with { Full = most > 0 && day.Usable == most }).ToArray());
    }

    /// <summary>Lengths of the maximal runs of consecutive numbers.</summary>
    private static List<int> Runs(IEnumerable<int> lessons)
    {
        var runs = new List<int>();
        var run = 0;
        var previous = int.MinValue;
        foreach (var lesson in lessons.Distinct().Order())
        {
            if (run > 0 && lesson != previous + 1)
            {
                runs.Add(run);
                run = 0;
            }
            run++;
            previous = lesson;
        }
        if (run > 0)
            runs.Add(run);
        return runs;
    }
}
