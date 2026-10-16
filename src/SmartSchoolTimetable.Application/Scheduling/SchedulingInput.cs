using System.Text.Json.Serialization;

namespace SmartSchoolTimetable.Application.Scheduling;

/// <summary>
/// Marks a value that does not change what can be scheduled: names and labels shown to the owner, and row ids of
/// assignments. <see cref="SchedulingInputHash"/> leaves it out, so renaming a teacher does not invalidate a timetable.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotHashedAttribute : Attribute;

/// <summary>Lessons that exist on one working day.</summary>
public sealed record DayLessons(int Day, int Lessons);

public sealed record StageInput(long Id, [property: NotHashed] string Name);

public sealed record PeriodInput(int Position, string Kind, int StartMinute, int EndMinute, bool StartBell, bool EndBell);

/// <summary>A day and lesson number (blocked periods; lesson numbers are the same in every shift, DECISIONS_PENDING #57).</summary>
public sealed record SlotRef(int Day, int Lesson);

/// <param name="StartMinute">First period start (minutes after midnight), null without periods; for the overlap warning.</param>
/// <param name="SessionBreaksAfter">
/// R3 (دوام مزدوج): lesson numbers n after which another daily session of this shift has a break, so (n, n+1) is not a
/// double lesson. Hashed: it changes which timetables are valid. Null for a single session and then left out of the
/// JSON, so the hash of every single-session school is the same as before R3. The other sessions' clock times and the
/// day→session mapping only change the times shown, so they are not part of the input (docs/DECISIONS_PENDING #81).
/// </param>
public sealed record ShiftInput(long Id, [property: NotHashed] string Name, IReadOnlyList<DayLessons> LessonsByDay, int? StartMinute, int? EndMinute,
    IReadOnlyList<PeriodInput>? Periods = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? SessionBreaksAfter = null)
{
    /// <summary>True when lessons n and n+1 can form a double: no break row between them in the shift or any session.</summary>
    public bool Adjacent(int lesson)
    {
        if (SessionBreaksAfter?.Contains(lesson) == true)
            return false;
        if (Periods is not { Count: > 0 } periods)
            return true;
        var rows = periods.Where(period => period.Kind == nameof(Domain.SchoolSetup.PeriodKind.Lesson)).OrderBy(period => period.Position).Select(period => period.Position).ToArray();
        return lesson < 1 || lesson + 1 > rows.Length || rows[lesson] == rows[lesson - 1] + 1;
    }
}

/// <param name="AllowedByDay">The first N lessons of each working day the section may use (its stage's count in its shift).</param>
public sealed record SectionInput(long Id, long StageId, long ShiftId, [property: NotHashed] string StageName, [property: NotHashed] string Label, IReadOnlyList<DayLessons> AllowedByDay);

public sealed record SubjectInput(
    long Id,
    [property: NotHashed] string Name,
    int Priority,
    bool DistributionEnabled,
    bool SpreadAcrossDays,
    bool Heavy,
    bool RequiresDoublePeriod,
    long? RequiredResourceId,
    IReadOnlyList<SlotRef> Blocked,
    [property: NotHashed] int ColorIndex = 1,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? DailyCap = null);

/// <summary>A curriculum line of a stage: every section of the stage takes it.</summary>
public sealed record LineInput(long Id, long StageId, long SubjectId, [property: NotHashed] string? Label, int WeeklyLessons, bool NeedsDoublePeriod);

public sealed record AssignmentInput([property: NotHashed] long Id, long SectionId, long LineId, long TeacherId);

/// <param name="MaxConsecutive">The teacher's own limit of lessons in a row (2 or 3); null follows the school's (<see cref="ProfileInput.DefaultMaxConsecutive"/>).</param>
/// <param name="Released">Fully released for the whole year (no lessons possible).</param>
/// <param name="PartiallyReleased">Released for part of the year only (a warning).</param>
/// <param name="ShortName">Shown in timetable cells (display only, not hashed).</param>
public sealed record TeacherInput(
    long Id,
    [property: NotHashed] string Name,
    bool IsArchived,
    bool Released,
    bool PartiallyReleased,
    IReadOnlyList<int> OffDays,
    IReadOnlyList<SlotRef> Blocked,
    int? MaxPerDay,
    int? MaxPerWeek,
    IReadOnlyList<long> SpecializationIds,
    [property: NotHashed] string? ShortName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxConsecutive = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ConsecutiveHard = false);

public sealed record ResourceInput(long Id, [property: NotHashed] string Name, string Kind, int Capacity, bool IsArchived);

public sealed record RuleInput(string Key, bool Enabled, int Weight);

/// <param name="DefaultMaxConsecutive">The school's «أقصى حصص متتالية للمعلم» (2 or 3); null (hand-built inputs) means no such rule.</param>
/// <param name="BalanceHard">Balanced teacher days are a hard rule (H12) instead of a priority.</param>
/// <param name="ConsecutiveHard">The consecutive-lessons limit is a hard rule (H11) for every teacher instead of the priority S9.</param>
public sealed record ProfileInput(int ProfileVersion, IReadOnlyList<RuleInput> Rules,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? DefaultMaxConsecutive = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool BalanceHard = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ConsecutiveHard = false);

/// <summary>A subject's daily limit for one grade or section (exactly one of the two ids is set); it wins over the subject's own limit, the section's over the grade's.</summary>
public sealed record CapOverrideInput(long SubjectId, long? StageId, long? SectionId, int Cap);

/// <summary>
/// Everything the solver needs for one academic year (Phase 3 §2.5, ADR 0034): an immutable, serializable snapshot
/// in a canonical order (every list sorted by id, days in week order). Phase 4 consumes it as it is; the pre-solve
/// validator reads only this, never the database.
/// </summary>
/// <param name="FormatVersion">Bumped when the contract changes shape, so an old hash never matches a new input.</param>
public sealed record SchedulingInput(
    int FormatVersion,
    long AcademicYearId,
    IReadOnlyList<int> WorkingDays,
    IReadOnlyList<ShiftInput> Shifts,
    IReadOnlyList<SectionInput> Sections,
    IReadOnlyList<SubjectInput> Subjects,
    IReadOnlyList<LineInput> Lines,
    IReadOnlyList<AssignmentInput> Assignments,
    IReadOnlyList<TeacherInput> Teachers,
    IReadOnlyList<ResourceInput> Resources,
    ProfileInput Profile,
    IReadOnlyList<StageInput>? Stages = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CapOverrideInput>? CapOverrides = null,
    [property: NotHashed] [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? EveningDays = null)
{
    public const int CurrentFormatVersion = 1;

    /// <summary>The teacher's «أقصى حصص متتالية»: his own, else the school's; null when no such rule applies.</summary>
    public int? MaxConsecutiveOf(TeacherInput teacher) => teacher.MaxConsecutive ?? Profile.DefaultMaxConsecutive;

    /// <summary>The limit when it is a HARD rule for this teacher (the school's «إلزامي» or his own); null when it is only a priority or absent.</summary>
    public int? HardConsecutiveOf(TeacherInput teacher) =>
        Profile.ConsecutiveHard || teacher.ConsecutiveHard ? MaxConsecutiveOf(teacher) : null;

    /// <summary>True when some teacher has the consecutive-lessons limit as a hard rule.</summary>
    public bool AnyHardConsecutive => Teachers.Any(teacher => HardConsecutiveOf(teacher) is not null);

    /// <summary>The same input with the consecutive-lessons limit a priority for everyone (the automatic retry after a timeout).</summary>
    public SchedulingInput WithSoftConsecutive() => this with
    {
        Profile = Profile with { ConsecutiveHard = false },
        Teachers = Teachers.Select(teacher => teacher with { ConsecutiveHard = false }).ToArray(),
    };

    /// <summary>
    /// True when the school attends an evening session on this working day in some semester (R3 double shift): its last periods weigh more in the fairness
    /// priority only. Not hashed (a soft preference never makes a timetable out of date, DECISIONS_PENDING #81).
    /// </summary>
    public bool IsEvening(int day) => EveningDays?.Contains(day) == true;
}
