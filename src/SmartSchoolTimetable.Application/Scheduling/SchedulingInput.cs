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
    [property: NotHashed] int ColorIndex = 1);

/// <summary>A curriculum line of a stage: every section of the stage takes it.</summary>
public sealed record LineInput(long Id, long StageId, long SubjectId, [property: NotHashed] string? Label, int WeeklyLessons, bool NeedsDoublePeriod);

public sealed record AssignmentInput([property: NotHashed] long Id, long SectionId, long LineId, long TeacherId);

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
    [property: NotHashed] string? ShortName = null);

public sealed record ResourceInput(long Id, [property: NotHashed] string Name, string Kind, int Capacity, bool IsArchived);

public sealed record RuleInput(string Key, bool Enabled, int Weight);

public sealed record ProfileInput(int ProfileVersion, IReadOnlyList<RuleInput> Rules);

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
    IReadOnlyList<StageInput>? Stages = null)
{
    public const int CurrentFormatVersion = 1;
}
