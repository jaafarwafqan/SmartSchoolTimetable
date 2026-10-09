using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>How many daily sessions a school alternates between (R3). Stored as an integer; never reorder.</summary>
public enum SessionSystem
{
    /// <summary>One session every day: the behaviour before R3 (default for every existing school).</summary>
    OneSession = 1,

    /// <summary>دوام مزدوج: the same sections attend the morning session on some days and the evening one on others.</summary>
    TwoSessions = 2,

    /// <summary>دوام ثلاثي (morning, noon, evening): allowed by the model, not offered in the UI yet («قريباً»).</summary>
    ThreeSessions = 3,
}

/// <summary>A daily session. The structural shift's own periods are the timing of <see cref="Morning"/>.</summary>
public enum SessionKind
{
    Morning = 1,
    Noon = 2,
    Evening = 3,
}

/// <summary>A lesson or break row of a session's timing (same rules as a shift's periods).</summary>
public sealed record SessionPeriod(SessionKind Session, int Position, PeriodKind Kind, TimeOnly StartTime, TimeOnly EndTime);

/// <summary>On working <paramref name="Day"/> of semester <paramref name="Term"/> (1 or 2) the school attends <paramref name="Session"/>.</summary>
public sealed record SessionDay(int Term, int Day, SessionKind Session);

/// <summary>
/// The daily-session system of one academic year (R3). The school keeps ONE set of sections, curriculum, staff and
/// ONE timetable grid (day, lesson); only the clock times change with the session a day falls in, and that mapping
/// may differ per semester (for example Sunday and Monday morning in semester 1, evening in semester 2).
/// Rules:
/// - the plan belongs to the year's single structural shift; that shift's periods are the morning timing;
/// - every other session's timing has exactly the same number of lessons (only start, length and breaks differ);
/// - for each semester every working day is mapped to exactly one session of the system.
/// A year without a plan is <see cref="SessionSystem.OneSession"/>.
/// </summary>
public sealed class SessionPlan : VersionedEntity
{
    public const int MaxTerms = 2;

    private readonly List<SessionPeriod> _periods = [];
    private readonly List<SessionDay> _days = [];

    private SessionPlan()
    {
    }

    public long AcademicYearId { get; private set; }
    public long ShiftId { get; private set; }
    public SessionSystem System { get; private set; } = SessionSystem.OneSession;
    public IReadOnlyList<SessionPeriod> Periods => _periods.OrderBy(period => period.Session).ThenBy(period => period.Position).ToArray();
    public IReadOnlyList<SessionDay> Days => _days.OrderBy(day => day.Term).ThenBy(day => day.Day).ToArray();

    /// <summary>The sessions a system uses, morning first.</summary>
    public static IReadOnlyList<SessionKind> SessionsOf(SessionSystem system) => system switch
    {
        SessionSystem.TwoSessions => [SessionKind.Morning, SessionKind.Evening],
        SessionSystem.ThreeSessions => [SessionKind.Morning, SessionKind.Noon, SessionKind.Evening],
        _ => [SessionKind.Morning],
    };

    public static SessionPlan Create(long academicYearId, long shiftId)
    {
        new DomainErrors()
            .When(academicYearId <= 0, nameof(AcademicYearId), DomainErrorCode.Required)
            .When(shiftId <= 0, nameof(ShiftId), DomainErrorCode.Required)
            .ThrowIfAny();
        return new SessionPlan { AcademicYearId = academicYearId, ShiftId = shiftId };
    }

    /// <summary>
    /// Replaces the system, the timing of the sessions other than morning, and the day mapping. Validation: the
    /// timings are valid period lists with exactly <paramref name="lessonCount"/> lessons; each semester
    /// (1 and 2) maps every working day once, to a session of the system.
    /// Field names: "System", "Periods", "Days".
    /// </summary>
    public void Replace(SessionSystem system, IReadOnlyDictionary<SessionKind, IReadOnlyList<PeriodDraft>> timings, IReadOnlyCollection<SessionDay> days,
        int lessonCount, IReadOnlyCollection<int> workingDays)
    {
        ArgumentNullException.ThrowIfNull(timings);
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(workingDays);
        var errors = new DomainErrors();
        errors.When(!Enum.IsDefined(system), nameof(System), DomainErrorCode.InvalidOption);
        var sessions = Enum.IsDefined(system) ? SessionsOf(system) : [SessionKind.Morning];
        var extra = sessions.Where(kind => kind != SessionKind.Morning).ToArray();
        errors.When(timings.Keys.Any(kind => !extra.Contains(kind)) || extra.Any(kind => !timings.ContainsKey(kind)), "Periods", DomainErrorCode.InvalidOption);
        foreach (var (_, drafts) in timings)
        {
            foreach (var error in Shift.ValidatePeriods(drafts).Errors)
                errors.Add(error.Field, error.Code);
            errors.When(drafts.Count(draft => draft.Kind == PeriodKind.Lesson) != lessonCount, "Periods", DomainErrorCode.SessionLessonCountMismatch);
        }
        const int terms = MaxTerms;
        if (system != SessionSystem.OneSession)
        {
            errors.When(days.Any(day => day.Term < 1 || day.Term > terms || !workingDays.Contains(day.Day) || !sessions.Contains(day.Session)), "Days", DomainErrorCode.InvalidOption);
            errors.When(days.GroupBy(day => (day.Term, day.Day)).Any(group => group.Count() > 1), "Days", DomainErrorCode.Duplicate);
            errors.When(Enumerable.Range(1, terms).Any(term => workingDays.Any(day => days.All(item => item.Term != term || item.Day != day))), "Days", DomainErrorCode.Required);
        }
        errors.ThrowIfAny();

        System = system;
        _periods.Clear();
        _days.Clear();
        if (system != SessionSystem.OneSession)
        {
            foreach (var (kind, drafts) in timings)
                _periods.AddRange(drafts.Select((draft, index) => new SessionPeriod(kind, index + 1, draft.Kind, draft.StartTime, draft.EndTime)));
            _days.AddRange(days);
        }
        Touch();
    }

    /// <summary>The session of a working day in a semester (morning when unmapped, or for a single-session year).</summary>
    public SessionKind SessionOn(int term, int day) =>
        System == SessionSystem.OneSession ? SessionKind.Morning : _days.FirstOrDefault(item => item.Term == term && item.Day == day)?.Session ?? SessionKind.Morning;

    /// <summary>
    /// Lesson numbers n after which some session other than morning has a break row (before lesson n+1). A double
    /// lesson (n, n+1) must be adjacent in EVERY session, so the scheduler treats these gaps like a morning break.
    /// </summary>
    public IReadOnlyList<int> BreaksAfterLessons()
    {
        if (System == SessionSystem.OneSession)
            return [];
        var result = new SortedSet<int>();
        foreach (var session in _periods.GroupBy(period => period.Session))
        {
            var lessons = 0;
            var rows = session.OrderBy(period => period.Position).ToArray();
            for (var index = 0; index < rows.Length; index++)
            {
                if (rows[index].Kind == PeriodKind.Lesson)
                    lessons++;
                else if (lessons > 0 && rows.Skip(index + 1).Any(row => row.Kind == PeriodKind.Lesson))
                    result.Add(lessons);
            }
        }
        return result.ToArray();
    }

    /// <summary>The other semester's mapping for a double system: every day attends the other session (one click «اعكس»).</summary>
    public static IReadOnlyList<SessionDay> Reversed(IEnumerable<SessionDay> days, int toTerm)
    {
        ArgumentNullException.ThrowIfNull(days);
        return days.Select(day => day with
        {
            Term = toTerm,
            Session = day.Session == SessionKind.Morning ? SessionKind.Evening : day.Session == SessionKind.Evening ? SessionKind.Morning : day.Session,
        }).ToArray();
    }
}
