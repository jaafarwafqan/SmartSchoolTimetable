using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Scheduling;

/// <summary>Kinds of entities a finding points at (and the frontend links to).</summary>
public static class FindingEntities
{
    public const string School = "school";
    public const string Section = "section";
    public const string Stage = "stage";
    public const string Teacher = "teacher";
    public const string Subject = "subject";
    public const string Resource = "resource";
    public const string Shift = "shift";
}

public sealed record FindingEntity(string Kind, long Id, string Name);

/// <param name="Severity">"error" blocks generation; "warning" does not.</param>
/// <param name="Related">Other entities involved (the sections of a subject shortage, the shifts of an overlap…).</param>
/// <param name="Details">Extra names for the message (subjects of unassigned lines, a shift name).</param>
/// <param name="Fixes">Suggested fix codes, shown in Arabic with a link.</param>
public sealed record ValidationFinding(
    string Code,
    string Severity,
    FindingEntity Entity,
    IReadOnlyList<FindingEntity> Related,
    int? Required,
    int? Available,
    int? Shortage,
    IReadOnlyList<string> Details,
    IReadOnlyList<string> Fixes);

/// <param name="DoublePeriodsRequired">The «دروس مزدوجة» mode: double lessons are hard constraints, so a line that
/// cannot get its pairs is an error. Off (default): they are a soft preference and it is a warning.</param>
public sealed record ValidatorOptions(bool DoublePeriodsRequired = false)
{
    public static readonly ValidatorOptions Default = new();
}

public sealed record ValidationReport(bool Ready, int Errors, int Warnings, IReadOnlyList<ValidationFinding> Findings);

/// <summary>
/// The pre-solve validator (Phase 3 §3, ADR 0035). Pure: it reads only a <see cref="SchedulingInput"/>.
/// SOUNDNESS: an error is reported only when the data PROVES that no timetable exists (a demand above an upper
/// bound of what is available). Anything uncertain is a warning. Every bound below is an over-estimate of what
/// any timetable could use, so a school that can be timetabled never gets an error (property-tested).
/// </summary>
public static class PreSolveValidator
{
    public const string Error = "error";
    public const string Warning = "warning";

    private sealed record Slot(long Shift, int Day, int Lesson);

    public static ValidationReport Validate(SchedulingInput input, ValidatorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        options ??= ValidatorOptions.Default;
        var context = new Context(input);
        var findings = new List<ValidationFinding>();
        CheckNothingToSchedule(context, findings);
        CheckShifts(context, findings);
        CheckUnassigned(context, findings);
        CheckSectionCapacity(context, findings);
        CheckTeachers(context, findings);
        CheckSubjectSlots(context, findings);
        CheckAssignments(context, findings);
        CheckResources(context, findings);
        CheckDoublePeriods(context, options, findings);
        CheckConsistency(context, findings);
        CheckSectionFill(context, findings);
        CheckTeacherDays(context, findings);
        var errors = findings.Count(finding => finding.Severity == Error);
        return new ValidationReport(errors == 0, errors, findings.Count - errors, findings);
    }

    private sealed class Context(SchedulingInput input)
    {
        public SchedulingInput Input { get; } = input;
        public DailyCaps Caps { get; } = new(input);
        public Dictionary<long, SubjectInput> Subjects { get; } = input.Subjects.ToDictionary(subject => subject.Id);
        public Dictionary<long, TeacherInput> Teachers { get; } = input.Teachers.ToDictionary(teacher => teacher.Id);
        public Dictionary<long, ShiftInput> Shifts { get; } = input.Shifts.ToDictionary(shift => shift.Id);
        public Dictionary<long, ResourceInput> Resources { get; } = input.Resources.ToDictionary(resource => resource.Id);
        public ILookup<long, LineInput> LinesByStage { get; } = input.Lines.ToLookup(line => line.StageId);
        public Dictionary<long, LineInput> Lines { get; } = input.Lines.ToDictionary(line => line.Id);
        public Dictionary<long, SectionInput> Sections { get; } = input.Sections.ToDictionary(section => section.Id);
        public Dictionary<(long SectionId, long LineId), AssignmentInput> Assignments { get; } =
            input.Assignments.GroupBy(row => (row.SectionId, row.LineId)).ToDictionary(group => group.Key, group => group.First());

        /// <summary>A section's allowed slots: the first N lessons of each working day in its shift.</summary>
        public IEnumerable<Slot> SlotsOf(SectionInput section) =>
            section.AllowedByDay.Where(day => Input.WorkingDays.Contains(day.Day))
                .SelectMany(day => Enumerable.Range(1, Math.Max(0, day.Lessons)).Select(lesson => new Slot(section.ShiftId, day.Day, lesson)));

        public bool ConsecutiveLessons(Slot first, Slot second)
        {
            if (first.Shift != second.Shift || second.Lesson != first.Lesson + 1)
                return false;
            // No break row between them, in the shift or in any of its daily sessions (R3).
            return !Shifts.TryGetValue(first.Shift, out var shift) || shift.Adjacent(first.Lesson);
        }

        public static bool Usable(TeacherInput teacher, Slot slot) =>
            !teacher.OffDays.Contains(slot.Day) && !teacher.Blocked.Contains(new SlotRef(slot.Day, slot.Lesson));

        public static bool Allowed(SubjectInput subject, Slot slot) => !subject.Blocked.Contains(new SlotRef(slot.Day, slot.Lesson));

        public string SubjectName(long id) => Subjects.TryGetValue(id, out var subject) ? subject.Name : string.Empty;
    }

    private static ValidationFinding Finding(string code, string severity, FindingEntity entity, int? required = null, int? available = null,
        IReadOnlyList<FindingEntity>? related = null, IReadOnlyList<string>? details = null, params string[] fixes) =>
        new(code, severity, entity, related ?? [], required, available,
            required is { } r && available is { } a && r > a ? r - a : null, details ?? [], fixes);

    private static FindingEntity SectionEntity(SectionInput section) => new(FindingEntities.Section, section.Id, $"{section.StageName} / {section.Label}");

    private static FindingEntity TeacherEntity(TeacherInput teacher) => new(FindingEntities.Teacher, teacher.Id, teacher.Name);

    private static FindingEntity SubjectEntity(SubjectInput subject) => new(FindingEntities.Subject, subject.Id, subject.Name);

    private static void CheckNothingToSchedule(Context context, List<ValidationFinding> findings)
    {
        if (context.Input.Sections.Count == 0 || context.Input.Lines.Count == 0)
            findings.Add(Finding(FindingCodes.NothingToSchedule, Error, new FindingEntity(FindingEntities.School, 0, string.Empty), fixes: "addSectionsAndCurriculum"));
    }

    /// <summary>Sections in a shift with no lessons at all cannot be placed (check 8: a section without a usable shift).</summary>
    private static void CheckShifts(Context context, List<ValidationFinding> findings)
    {
        foreach (var group in context.Input.Sections.GroupBy(section => section.ShiftId))
        {
            var shift = context.Shifts.GetValueOrDefault(group.Key);
            if (shift is not null && shift.LessonsByDay.Any(day => day.Lessons > 0))
                continue;
            // Only a proof when some section there has lessons to place; otherwise it is a warning.
            var severity = group.Any(section => context.LinesByStage[section.StageId].Any()) ? Error : Warning;
            findings.Add(Finding(FindingCodes.ShiftWithoutPeriods, severity, new FindingEntity(FindingEntities.Shift, group.Key, shift?.Name ?? string.Empty),
                required: group.Count(), related: group.Select(SectionEntity).ToArray(), fixes: "addPeriods"));
        }
    }

    /// <summary>Check 1: every line of every section needs a teacher (one finding per section).</summary>
    private static void CheckUnassigned(Context context, List<ValidationFinding> findings)
    {
        foreach (var section in context.Input.Sections)
        {
            var missing = context.LinesByStage[section.StageId].Where(line => !context.Assignments.ContainsKey((section.Id, line.Id))).ToArray();
            if (missing.Length == 0)
                continue;
            findings.Add(Finding(FindingCodes.UnassignedLines, Error, SectionEntity(section), required: missing.Length,
                details: missing.Select(line => line.Label is null ? context.SubjectName(line.SubjectId) : $"{context.SubjectName(line.SubjectId)} - {line.Label}").ToArray(),
                fixes: "assignTeachers"));
        }
    }

    /// <summary>Check 2: a section's weekly lessons against its allowed slots (over = error, under = warning).</summary>
    private static void CheckSectionCapacity(Context context, List<ValidationFinding> findings)
    {
        foreach (var section in context.Input.Sections)
        {
            var required = context.LinesByStage[section.StageId].Sum(line => line.WeeklyLessons);
            var available = context.SlotsOf(section).Count();
            if (required == 0 || available == 0)
                continue;
            if (required > available)
                findings.Add(Finding(FindingCodes.SectionOverCapacity, Error, SectionEntity(section), required, available, fixes: ["reduceCurriculum", "increaseLessons"]));
            else if (required < available)
                findings.Add(Finding(FindingCodes.SectionUnderCapacity, Warning, SectionEntity(section), required, available, fixes: "completeCurriculum"));
        }
    }

    /// <summary>
    /// Check 3: a teacher's assigned lessons against what they can teach: the union of their sections' allowed slots,
    /// without off days and blocked periods, bounded by days × max per day and by max per week (an upper bound).
    /// </summary>
    private static void CheckTeachers(Context context, List<ValidationFinding> findings)
    {
        foreach (var group in context.Input.Assignments.GroupBy(row => row.TeacherId))
        {
            if (!context.Teachers.TryGetValue(group.Key, out var teacher))
                continue;
            var rows = group.Where(row => context.Sections.ContainsKey(row.SectionId) && context.Lines.ContainsKey(row.LineId)).ToArray();
            var required = rows.Sum(row => context.Lines[row.LineId].WeeklyLessons);
            if (required == 0)
                continue;
            if (teacher.IsArchived)
            {
                findings.Add(Finding(FindingCodes.TeacherArchived, Error, TeacherEntity(teacher), required, fixes: "moveWorkload"));
                continue;
            }
            if (teacher.Released)
            {
                findings.Add(Finding(FindingCodes.TeacherReleased, Error, TeacherEntity(teacher), required, 0, fixes: "moveWorkload"));
                continue;
            }
            if (teacher.PartiallyReleased)
                findings.Add(Finding(FindingCodes.TeacherPartialRelease, Warning, TeacherEntity(teacher), required, fixes: "checkRelease"));
            var slots = rows.Select(row => row.SectionId).Distinct().SelectMany(id => context.SlotsOf(context.Sections[id]))
                .Select(slot => new ShiftSlot(slot.Shift, slot.Day, slot.Lesson));
            var available = TeacherAvailability.Compute(slots, teacher.OffDays, teacher.Blocked.Select(slot => new BlockedPeriod(slot.Day, slot.Lesson)).ToArray(),
                teacher.MaxPerDay, teacher.MaxPerWeek, released: false).Available;
            if (required > available)
                findings.Add(Finding(FindingCodes.TeacherOverload, Error, TeacherEntity(teacher), required, available, fixes: ["moveWorkload", "raiseTeacherLimit"]));
        }
    }

    /// <summary>
    /// Check 4: all lessons of a subject in a section must fit in the section's slots the subject is not blocked in.
    /// Identical sections (same stage and shift) are reported once with all of them related.
    /// </summary>
    private static void CheckSubjectSlots(Context context, List<ValidationFinding> findings)
    {
        foreach (var group in context.Input.Sections.GroupBy(section => (section.StageId, section.ShiftId)))
        {
            var first = group.First();
            var slots = context.SlotsOf(first).ToArray();
            foreach (var subjectLines in context.LinesByStage[group.Key.StageId].GroupBy(line => line.SubjectId))
            {
                if (!context.Subjects.TryGetValue(subjectLines.Key, out var subject))
                    continue;
                var required = subjectLines.Sum(line => line.WeeklyLessons);
                var available = slots.Count(slot => Context.Allowed(subject, slot));
                if (required > available)
                    findings.Add(Finding(FindingCodes.SubjectSlotsShort, Error, SubjectEntity(subject), required, available,
                        related: group.Select(SectionEntity).ToArray(), fixes: ["removeSubjectBlocks", "reduceLessons"]));
            }
        }
    }

    /// <summary>
    /// Check 5: what a teacher teaches in one section must fit in section ∩ teacher (∩ subject) slots, with at most
    /// max per day on each day. Per subject first; the section total only when every subject fits on its own.
    /// </summary>
    private static void CheckAssignments(Context context, List<ValidationFinding> findings)
    {
        foreach (var group in context.Input.Assignments.GroupBy(row => (row.SectionId, row.TeacherId)))
        {
            if (!context.Sections.TryGetValue(group.Key.SectionId, out var section) || !context.Teachers.TryGetValue(group.Key.TeacherId, out var teacher)
                || teacher.IsArchived || teacher.Released)
                continue;
            var usable = context.SlotsOf(section).Where(slot => Context.Usable(teacher, slot)).ToArray();
            var lines = group.Where(row => context.Lines.ContainsKey(row.LineId)).Select(row => context.Lines[row.LineId]).ToArray();
            var reported = false;
            foreach (var bySubject in lines.GroupBy(line => line.SubjectId))
            {
                if (!context.Subjects.TryGetValue(bySubject.Key, out var subject))
                    continue;
                var required = bySubject.Sum(line => line.WeeklyLessons);
                var available = ByDay(usable.Where(slot => Context.Allowed(subject, slot)), teacher.MaxPerDay);
                if (required <= available)
                    continue;
                reported = true;
                findings.Add(Finding(FindingCodes.AssignmentInfeasible, Error, TeacherEntity(teacher), required, available,
                    related: [SectionEntity(section), SubjectEntity(subject)], fixes: ["changeTeacher", "removeTeacherBlocks"]));
            }
            var total = lines.Sum(line => line.WeeklyLessons);
            var totalAvailable = ByDay(usable, teacher.MaxPerDay);
            if (!reported && total > totalAvailable)
                findings.Add(Finding(FindingCodes.AssignmentInfeasible, Error, TeacherEntity(teacher), total, totalAvailable,
                    related: [SectionEntity(section)], fixes: ["changeTeacher", "removeTeacherBlocks"]));
        }
    }

    private static int ByDay(IEnumerable<Slot> slots, int? maxPerDay) =>
        slots.GroupBy(slot => slot.Day).Sum(day => Math.Min(day.Count(), maxPerDay ?? int.MaxValue));

    /// <summary>
    /// Check 6: per resource and shift, the lessons that need it against capacity × slots, where a slot counts at
    /// most as many uses as sections that could use it there (an upper bound).
    /// </summary>
    private static void CheckResources(Context context, List<ValidationFinding> findings)
    {
        foreach (var resource in context.Input.Resources)
        {
            var subjects = context.Input.Subjects.Where(subject => subject.RequiredResourceId == resource.Id).ToArray();
            if (subjects.Length == 0)
                continue;
            if (resource.IsArchived && subjects.Any(subject => context.Input.Lines.Any(line => line.SubjectId == subject.Id)))
                findings.Add(Finding(FindingCodes.ResourceArchived, Warning, new FindingEntity(FindingEntities.Resource, resource.Id, resource.Name),
                    related: subjects.Select(SubjectEntity).ToArray(), fixes: "restoreResource"));
            foreach (var shift in context.Input.Sections.GroupBy(section => section.ShiftId))
            {
                var required = 0;
                var users = new Dictionary<Slot, int>();
                foreach (var section in shift)
                {
                    var needing = context.LinesByStage[section.StageId].Where(line => subjects.Any(subject => subject.Id == line.SubjectId)).ToArray();
                    if (needing.Length == 0)
                        continue;
                    required += needing.Sum(line => line.WeeklyLessons);
                    foreach (var slot in context.SlotsOf(section).Where(slot => needing.Any(line => Context.Allowed(context.Subjects[line.SubjectId], slot))))
                        users[slot] = users.GetValueOrDefault(slot) + 1;
                }
                var available = users.Values.Sum(count => Math.Min(count, resource.Capacity));
                if (required > available)
                    findings.Add(Finding(FindingCodes.ResourceOverCapacity, Error, new FindingEntity(FindingEntities.Resource, resource.Id, resource.Name), required, available,
                        related: subjects.Select(SubjectEntity).ToArray(), details: [context.Shifts.GetValueOrDefault(shift.Key)?.Name ?? string.Empty],
                        fixes: ["raiseResourceCapacity", "removeSubjectBlocks"]));
            }
        }
    }

    /// <summary>
    /// Check 7: a line taught in double periods needs lessons ÷ 2 pairs of consecutive usable slots on the same day
    /// (section, subject and its teacher). Impossible = an error only when double lessons are REQUIRED (the
    /// «دروس مزدوجة» generation mode); in the standard mode they are a soft preference, so it is a warning (an error
    /// there could block a school that can be timetabled: DECISIONS_PENDING #65). Exactly enough = warning («ضيق»).
    /// </summary>
    private static void CheckDoublePeriods(Context context, ValidatorOptions options, List<ValidationFinding> findings)
    {
        foreach (var section in context.Input.Sections)
        {
            foreach (var line in context.LinesByStage[section.StageId])
            {
                if (!context.Subjects.TryGetValue(line.SubjectId, out var subject) || !(line.NeedsDoublePeriod || subject.RequiresDoublePeriod))
                    continue;
                var pairs = line.WeeklyLessons / 2;
                if (pairs == 0)
                    continue;
                var teacher = context.Assignments.TryGetValue((section.Id, line.Id), out var row) ? context.Teachers.GetValueOrDefault(row.TeacherId) : null;
                var usable = context.SlotsOf(section).Where(slot => Context.Allowed(subject, slot) && (teacher is null || Context.Usable(teacher, slot))).ToHashSet();
                var available = usable.GroupBy(slot => slot.Day).Sum(day => PairsIn(day, context));
                var related = new[] { SectionEntity(section) };
                if (pairs > available)
                    findings.Add(Finding(FindingCodes.DoublePeriodImpossible, options.DoublePeriodsRequired ? Error : Warning, SubjectEntity(subject), pairs, available, related,
                        fixes: ["removeSubjectBlocks", "turnOffDoublePeriod"]));
                else if (pairs == available)
                    findings.Add(Finding(FindingCodes.DoublePeriodTight, Warning, SubjectEntity(subject), pairs, available, related, fixes: "removeSubjectBlocks"));
            }
        }
    }

    /// <summary>Disjoint pairs of consecutive lesson numbers (n, n+1) on one day: ⌊run ÷ 2⌋ per run.</summary>
    private static int PairsIn(IEnumerable<Slot> lessons, Context context)
    {
        var sorted = lessons.Distinct().OrderBy(slot => slot.Lesson).ToArray();
        var pairs = 0;
        var run = 0;
        for (var index = 0; index < sorted.Length; index++)
        {
            run = index > 0 && context.ConsecutiveLessons(sorted[index - 1], sorted[index]) ? run + 1 : 1;
            if (run == 2)
            {
                pairs++;
                run = 0;
            }
        }
        return pairs;
    }

    /// <summary>One subject of one section: its lessons a week, its daily limit (H9) and the teachers who teach it there.</summary>
    private sealed record SubjectPlan(SubjectInput Subject, int Weekly, int Cap, IReadOnlyList<TeacherInput> Teachers);

    private static string Text(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The subjects of a section with their teachers, or null when the plan is not complete (a line without a teacher, an unknown or
    /// archived or fully released teacher: other findings already report those, so the checks below stay silent about them).
    /// </summary>
    private static List<SubjectPlan>? PlansOf(Context context, SectionInput section)
    {
        var days = section.AllowedByDay.Count(day => day.Lessons > 0 && context.Input.WorkingDays.Contains(day.Day));
        var caps = context.Caps;
        var plans = new List<SubjectPlan>();
        foreach (var bySubject in context.LinesByStage[section.StageId].GroupBy(line => line.SubjectId))
        {
            if (!context.Subjects.TryGetValue(bySubject.Key, out var subject))
                return null;
            var teachers = new List<TeacherInput>();
            foreach (var line in bySubject)
            {
                if (!context.Assignments.TryGetValue((section.Id, line.Id), out var row) || !context.Teachers.TryGetValue(row.TeacherId, out var teacher)
                    || teacher.IsArchived || teacher.Released)
                    return null;
                if (!teachers.Contains(teacher))
                    teachers.Add(teacher);
            }
            var weekly = bySubject.Sum(line => line.WeeklyLessons);
            if (weekly > 0)
                plans.Add(new SubjectPlan(subject, weekly, caps.For(subject, section, weekly, days), teachers));
        }
        return plans;
    }

    /// <summary>The lesson numbers (1..lessons of that day) where this subject could be taught on one day: not blocked for the subject and some teacher of it is free.</summary>
    private static List<int> FreeLessons(SubjectPlan plan, int day, int lessons)
    {
        var free = Enumerable.Range(1, Math.Max(0, lessons))
            .Where(lesson => !plan.Subject.Blocked.Contains(new SlotRef(day, lesson))
                && plan.Teachers.Any(teacher => !teacher.OffDays.Contains(day) && !teacher.Blocked.Contains(new SlotRef(day, lesson)))).ToList();
        // One teacher with a daily maximum cannot teach more of the subject than that in a day.
        if (plan.Teachers is [{ MaxPerDay: { } maximum }] && free.Count > maximum)
            free = free.Take(maximum).ToList();
        return free;
    }

    /// <summary>
    /// The four «ملء الشعبة» checks (the owner's real-school test: every section is full every day, so a day off or a daily limit makes the
    /// timetable impossible and the solver could only say so late). Each error is a proof: the numbers are upper bounds of what any timetable
    /// can place, and a section may keep (slots - lessons) slots empty, so a day or a slot is reported only when even that slack cannot absorb it.
    /// </summary>
    private static void CheckSectionFill(Context context, List<ValidationFinding> findings)
    {
        foreach (var section in context.Input.Sections)
        {
            if (PlansOf(context, section) is not { Count: > 0 } plans)
                continue;
            var days = section.AllowedByDay.Where(day => day.Lessons > 0 && context.Input.WorkingDays.Contains(day.Day)).ToArray();
            var totalSlots = days.Sum(day => day.Lessons);
            var totalRequired = plans.Sum(plan => plan.Weekly);
            var slack = totalSlots - totalRequired;
            if (slack < 0)
                continue; // SECTION_OVER_CAPACITY already says so.

            // 1. Days: a subject needs ceil(weekly / limit) different days, and its teacher must be free on that many.
            foreach (var plan in plans)
            {
                var needed = (plan.Weekly + plan.Cap - 1) / plan.Cap;
                var freeDays = days.Where(day => FreeLessons(plan, day.Day, day.Lessons).Count > 0).Select(day => day.Day).ToArray();
                if (needed <= freeDays.Length)
                    continue;
                var lost = days.Select(day => day.Day).Except(freeDays).ToArray();
                findings.Add(Finding(FindingCodes.SubjectDaysShort, Error, SectionEntity(section), needed, freeDays.Length,
                    related: [SubjectEntity(plan.Subject), .. plan.Teachers.Select(TeacherEntity)],
                    details: [Text(plan.Weekly), Text(plan.Cap), .. lost.Select(Text)],
                    fixes: ["raiseDailyCap", "removeTeacherBlocks", "changeTeacher"]));
            }

            // 2. A day: what the subjects can place on it (limit, weekly lessons and free teacher periods) must reach the day's lessons less the slack.
            var reportedDay = new HashSet<int>();
            foreach (var day in days)
            {
                var fill = plans.Sum(plan => Math.Min(Math.Min(plan.Cap, plan.Weekly), FreeLessons(plan, day.Day, day.Lessons).Count));
                var needed = day.Lessons - slack;
                if (fill >= needed)
                    continue;
                reportedDay.Add(day.Day);
                var off = plans.SelectMany(plan => plan.Teachers).Distinct().Where(teacher => teacher.OffDays.Contains(day.Day)).ToArray();
                findings.Add(Finding(FindingCodes.SectionDayUnderfilled, Error, SectionEntity(section), needed, fill,
                    related: off.Select(TeacherEntity).ToArray(),
                    details: [Text(day.Day), Text(day.Lessons)],
                    fixes: ["removeTeacherBlocks", "raiseDailyCap", "changeTeacher"]));
            }

            // 3. A slot: some teacher of the section must be free at it, unless the section can keep that slot empty.
            var uncovered = days.SelectMany(day => Enumerable.Range(1, day.Lessons).Select(lesson => (day.Day, Lesson: lesson)))
                .Where(slot => !plans.Any(plan => !plan.Subject.Blocked.Contains(new SlotRef(slot.Day, slot.Lesson))
                    && plan.Teachers.Any(teacher => !teacher.OffDays.Contains(slot.Day) && !teacher.Blocked.Contains(new SlotRef(slot.Day, slot.Lesson))))).ToArray();
            if (uncovered.Length > slack)
                findings.Add(Finding(FindingCodes.SectionSlotUncovered, Error, SectionEntity(section), uncovered.Length, slack,
                    details: [Text(uncovered[0].Day), Text(uncovered[0].Lesson)],
                    fixes: ["removeTeacherBlocks", "changeTeacher"]));

            // 4. Two teachers of the section who are off the same day and hold more than the weekly lessons less what the others could place that day (warning).
            foreach (var day in days.Where(day => !reportedDay.Contains(day.Day)))
            {
                var off = plans.SelectMany(plan => plan.Teachers).Distinct().Where(teacher => teacher.OffDays.Contains(day.Day)).OrderBy(teacher => teacher.Id).ToArray();
                for (var first = 0; first < off.Length; first++)
                {
                    for (var second = first + 1; second < off.Length; second++)
                    {
                        var pair = new[] { off[first], off[second] };
                        var held = plans.Where(plan => plan.Teachers.All(teacher => pair.Contains(teacher))).Sum(plan => plan.Weekly);
                        var possibleFill = plans.Where(plan => !plan.Teachers.All(teacher => pair.Contains(teacher)))
                            .Sum(plan => Math.Min(plan.Cap, FreeLessons(plan, day.Day, day.Lessons).Count));
                        var limit = totalRequired - Math.Min(day.Lessons, possibleFill);
                        if (held > limit)
                            findings.Add(Finding(FindingCodes.TeachersSameDayOff, Warning, SectionEntity(section), held, limit,
                                related: pair.Select(TeacherEntity).ToArray(),
                                details: [Text(day.Day), Text(totalRequired)],
                                fixes: ["removeTeacherBlocks", "changeTeacher"]));
                    }
                }
            }
        }
    }

    /// <summary>
    /// The teacher-day rules. A teacher can teach at most <c>run - floor(run ÷ (k + 1))</c> lessons in a run of free lesson numbers when no more than k may follow
    /// each other (k = 2: 4 of 6, k = 3: 5 of 6), so a load above the sum over his days, under his daily and weekly maximum, is impossible (an error; a proof, since the
    /// numbers over-estimate what any timetable can use). With balanced days made a hard rule, the busiest full day must still fit the load (a proof as well).
    /// </summary>
    private static void CheckTeacherDays(Context context, List<ValidationFinding> findings)
    {
        foreach (var info in TeacherDays.Of(context.Input).Values)
        {
            var teacher = info.Teacher;
            if (teacher.IsArchived || teacher.Released)
                continue; // other findings already report those
            // Hard for him: an error (a proof). A priority: a warning (some lessons will exceed the limit), only while the priority is on.
            var softOn = context.Input.Profile.Rules.FirstOrDefault(rule => rule.Key == SchedulingRuleKeys.MaxConsecutiveLessons) is not { } rule || (rule.Enabled && rule.Weight > 0);
            if (info.Limit is { } consecutive && (info.Hard || softOn))
            {
                // Only when the limit is what makes it impossible: without it the load would fit.
                var without = info.CapacityUnder(null);
                var available = info.CapacityUnder(consecutive);
                if (info.Load > available && info.Load <= without)
                {
                    findings.Add(Finding(FindingCodes.TeacherConsecutiveShort, info.Hard ? Error : Warning, TeacherEntity(teacher), info.Load, available,
                        details: [Text(consecutive), Text(info.Days.Count(day => day.Usable > 0))],
                        fixes: consecutive < Teacher3 ? ["raiseTeacherConsecutive", "moveWorkload"] : ["moveWorkload", "removeTeacherBlocks"]));
                }
            }
            if (context.Input.Profile.BalanceHard && info.FullDays.Count >= 2 && info.Load <= info.Capacity)
            {
                // Full days within one lesson of each other: none can hold more than the quietest full day's capacity + 1; a partly-available day holds at most its share.
                var quietest = info.Days.Where(day => day.Full).Min(day => day.Capacity);
                var bound = info.Days.Sum(day => day.Full ? Math.Min(day.Capacity, quietest + 1) : Math.Min(day.Capacity, info.PartialAllowance(day.Day) ?? 0));
                if (info.Load > bound)
                    findings.Add(Finding(FindingCodes.TeacherBalanceHardShort, Error, TeacherEntity(teacher), info.Load, bound, fixes: ["relaxBalance", "moveWorkload"]));
            }
        }
    }

    private const int Teacher3 = 3;

    /// <summary>Check 8: data that is inconsistent but does not prove infeasibility (warnings).</summary>
    private static void CheckConsistency(Context context, List<ValidationFinding> findings)
    {
        var input = context.Input;
        // Orphan blocked periods: outside the grid (working days × the most lessons any shift has that day).
        var grid = input.WorkingDays.ToDictionary(day => day, day => input.Shifts.Select(shift => shift.LessonsByDay.FirstOrDefault(item => item.Day == day)?.Lessons ?? 0).DefaultIfEmpty(0).Max());
        bool Orphan(SlotRef slot) => !grid.TryGetValue(slot.Day, out var lessons) || slot.Lesson < 1 || slot.Lesson > lessons;
        if (grid.Values.Any(lessons => lessons > 0))
        {
            foreach (var teacher in input.Teachers.Where(teacher => !teacher.IsArchived && teacher.Blocked.Any(Orphan)))
                findings.Add(Finding(FindingCodes.OrphanBlockedPeriods, Warning, TeacherEntity(teacher), teacher.Blocked.Count(Orphan), fixes: "cleanOrphans"));
            foreach (var subject in input.Subjects.Where(subject => subject.Blocked.Any(Orphan)))
                findings.Add(Finding(FindingCodes.OrphanBlockedPeriods, Warning, SubjectEntity(subject), subject.Blocked.Count(Orphan), fixes: "cleanOrphans"));
        }
        var usedSubjects = input.Lines.Select(line => line.SubjectId).ToHashSet();
        foreach (var subject in input.Subjects.Where(subject => !subject.DistributionEnabled && usedSubjects.Contains(subject.Id)))
            findings.Add(Finding(FindingCodes.DistributionDisabled, Warning, SubjectEntity(subject), fixes: "enableDistribution"));
        var stages = input.Stages ?? input.Sections.GroupBy(section => section.StageId)
            .Select(group => new StageInput(group.Key, group.First().StageName)).ToArray();
        foreach (var stage in stages.Where(stage => !context.LinesByStage[stage.Id].Any()))
            findings.Add(Finding(FindingCodes.StageWithoutCurriculum, Warning, new FindingEntity(FindingEntities.Stage, stage.Id, stage.Name), fixes: "fillCurriculum"));
        // A teacher in two shifts whose bell times overlap (DECISIONS_PENDING: shifts do not overlap by default).
        foreach (var group in input.Assignments.GroupBy(row => row.TeacherId))
        {
            if (!context.Teachers.TryGetValue(group.Key, out var teacher))
                continue;
            var shifts = group.Select(row => context.Sections.GetValueOrDefault(row.SectionId)?.ShiftId).OfType<long>().Distinct()
                .Select(id => context.Shifts.GetValueOrDefault(id)).OfType<ShiftInput>().ToArray();
            var overlapping = shifts.SelectMany((a, i) => shifts.Skip(i + 1).Where(b => Overlap(a, b)).Select(b => (a, b))).ToArray();
            if (overlapping.Length > 0)
                findings.Add(Finding(FindingCodes.TeacherShiftOverlap, Warning, TeacherEntity(teacher),
                    related: overlapping.SelectMany(pair => new[] { pair.a, pair.b }).Distinct().Select(shift => new FindingEntity(FindingEntities.Shift, shift.Id, shift.Name)).ToArray(),
                    fixes: "moveWorkload"));
        }
    }

    private static bool Overlap(ShiftInput a, ShiftInput b) =>
        a.StartMinute is { } aStart && a.EndMinute is { } aEnd && b.StartMinute is { } bStart && b.EndMinute is { } bEnd
        && aStart < bEnd && bStart < aEnd;
}
