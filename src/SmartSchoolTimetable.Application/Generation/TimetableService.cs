using System.Text.Json;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Generation;
using PeriodKind = SmartSchoolTimetable.Domain.SchoolSetup.PeriodKind;
using SessionKind = SmartSchoolTimetable.Domain.SchoolSetup.SessionKind;
using SessionPlan = SmartSchoolTimetable.Domain.SchoolSetup.SessionPlan;
using AcademicYear = SmartSchoolTimetable.Domain.SchoolSetup.AcademicYear;
using SessionSystem = SmartSchoolTimetable.Domain.SchoolSetup.SessionSystem;

namespace SmartSchoolTimetable.Application.Generation;

/// <param name="Stale">The school data changed after this version was made (its input hash differs from today's).</param>
/// <param name="Status">draft, approved or archived (M1 lifecycle); only a draft can still be approved.</param>
public sealed record TimetableVersionSummary(long Id, int Version, int Number, string Source, long? GenerationRunId, long? ParentVersionId, string Mode,
    long? Score, int Lessons, DateTimeOffset CreatedAt, bool IsApproved, DateTimeOffset? ApprovedAt, string? Note, bool Stale,
    string Status = "draft", DateTimeOffset? ArchivedAt = null);

public sealed record GridLessonTime(int Number, int StartMinute, int EndMinute);
public sealed record GridShift(long Id, string Name, IReadOnlyList<GridLessonTime> Lessons);
public sealed record GridSection(long Id, string StageName, string Label, long ShiftId, IReadOnlyList<DayLessons> AllowedByDay);
public sealed record GridSubject(long Id, string Name, int ColorIndex);
public sealed record GridTeacher(long Id, string Name, string ShortName);
public sealed record GridLesson(long SectionId, long LineId, long SubjectId, long TeacherId, int Day, int Lesson);
public sealed record GridSessionTiming(string Session, IReadOnlyList<GridLessonTime> Lessons);
public sealed record GridSessionDay(int Term, int Day, string Session);

/// <summary>
/// R3 daily sessions of the timetable's shift: the clock of each session and which session each working day falls in,
/// per semester (1, 2). The grid itself is the same in both semesters; only the times shown change. Morning is the
/// shift's own timing from the snapshot; the other sessions and the mapping are the current settings (not hashed:
/// they change no lesson, DECISIONS_PENDING #81).
/// </summary>
/// <param name="CurrentTerm">The semester whose dates contain today (the viewer opens on it), or null when the year's dates do not tell.</param>
public sealed record GridSessions(string System, long ShiftId, IReadOnlyList<GridSessionTiming> Timings, IReadOnlyList<GridSessionDay> Days, int? CurrentTerm = null)
{
    /// <summary>The session of a working day in a semester (morning when unmapped).</summary>
    public string SessionOn(int term, int day) =>
        Days.FirstOrDefault(item => item.Term == term && item.Day == day)?.Session ?? ApiText.ToValue(SessionKind.Morning);

    /// <summary>The lesson clock of a session, or null when it has no timing.</summary>
    public IReadOnlyList<GridLessonTime>? TimesOf(string session) => Timings.FirstOrDefault(item => item.Session == session)?.Lessons;
}

/// <summary>Everything the read-only grids need, from the version's own input snapshot (names as they were).</summary>
/// <param name="Violations">Hard-constraint violations found by the independent verifier now (0 for a valid version).</param>
public sealed record TimetableDto(
    TimetableVersionSummary Summary,
    IReadOnlyList<int> Days,
    IReadOnlyList<GridShift> Shifts,
    IReadOnlyList<GridSection> Sections,
    IReadOnlyList<GridSubject> Subjects,
    IReadOnlyList<GridTeacher> Teachers,
    IReadOnlyList<GridLesson> Lessons,
    TimetableScore? Score,
    int Violations,
    GridSessions? Sessions = null);

public sealed record ApproveTimetableCommand(int Version);

public sealed record ArchiveTimetableCommand(int Version);

/// <summary>Restores an older version as a NEW draft. <paramref name="Version"/> is the older version's concurrency token.</summary>
public sealed record RollbackTimetableCommand(int Version, string? Note);

/// <summary>The whole edited timetable (the editor moves or swaps lessons locally and sends every lesson).</summary>
public sealed record EditedTimetableCommand(IReadOnlyList<GridLesson>? Lessons, string? Note);

public sealed record ViolationDto(string Code, string Rule, long? SectionId, long? TeacherId, long? SubjectId, long? ResourceId, int? Day, int? Lesson, int? Count, int? Limit);

public sealed record TimetableCheckDto(IReadOnlyList<ViolationDto> Violations, TimetableScore Score);

public sealed record NamedItem(long Id, string Name);

public sealed record NamedSection(long Id, string StageName, string Label);

/// <summary>Names for the ids in a current-data check: today's names, and the snapshot's for things that no longer exist.</summary>
public sealed record CurrentCheckNames(IReadOnlyList<NamedItem> Teachers, IReadOnlyList<NamedSection> Sections, IReadOnlyList<NamedItem> Subjects,
    IReadOnlyList<NamedItem> Stages, IReadOnlyList<NamedItem> Resources, IReadOnlyList<NamedItem> Shifts);

/// <summary>
/// MF11: a SAVED version checked against TODAY's school data (the version itself never changes). <paramref name="Findings"/> are the hard
/// problems (empty: it still fits); <paramref name="Changes"/> is what changed in the scheduling input since the version was made.
/// </summary>
/// <param name="Stale">The school data changed after this version was made (the input hash differs).</param>
/// <param name="CanReplaceTeachers">Teacher changes are the only problem and putting the new teachers in the same slots breaks nothing.</param>
/// <param name="CanRepair">There is something to repair (any finding); «إصلاح بأقل تغيير» keeps every lesson that still fits.</param>
public sealed record CurrentCheckDto(long VersionId, bool Stale, IReadOnlyList<CurrentFinding> Findings, IReadOnlyList<InputChange> Changes,
    bool CanReplaceTeachers, bool CanRepair, CurrentCheckNames Names);

public sealed record ReplaceTeachersCommand(int Version);

/// <summary>«الجداول» (Phase 4 M3): saved versions, the grids, and «اعتماد هذا الإصدار» (one approved version per year).</summary>
public sealed class TimetableService(IDataStore store, TimeProvider clock)
{
    public async Task<OperationResult<IReadOnlyList<TimetableVersionSummary>>> ListAsync(long yearId, CancellationToken token)
    {
        if (await SchedulingInputBuilder.BuildAsync(store, yearId, token) is not { } input)
            return OperationResult.Failure<IReadOnlyList<TimetableVersionSummary>>(ErrorCodes.NotFound);
        var hash = SchedulingInputHash.Compute(input);
        var versions = await store.ListAsync(store.Read<TimetableVersion>().Where(version => version.AcademicYearId == yearId).OrderByDescending(version => version.Number), token);
        return OperationResult.Success<IReadOnlyList<TimetableVersionSummary>>(versions.Select(version => Summary(version, hash)).ToArray());
    }

    public async Task<OperationResult<TimetableDto>> GetAsync(long versionId, CancellationToken token)
    {
        if (await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == versionId), token) is not { } version)
            return OperationResult.Failure<TimetableDto>(ErrorCodes.NotFound);
        var current = await SchedulingInputBuilder.BuildAsync(store, version.AcademicYearId, token);
        var input = SnapshotOf(version);
        var sessions = await SessionsAsync(store, version.AcademicYearId, input, token);
        if (sessions is not null && await store.FirstOrDefaultAsync(store.Read<AcademicYear>().Where(year => year.Id == version.AcademicYearId), token) is { } year)
        {
            // #86: open on the semester whose dates contain today (the school computer's local date).
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            sessions = sessions with { CurrentTerm = SessionPlan.SemesterOn(year.Terms.Select(term => (term.StartDate, term.EndDate)), today) };
        }
        return OperationResult.Success(ToDto(version, input, current is null ? version.InputHash : SchedulingInputHash.Compute(current), sessions));
    }

    public async Task<OperationResult<TimetableVersionSummary>> ApproveAsync(long versionId, ApproveTimetableCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FirstOrDefaultAsync(store.Query<TimetableVersion>().Where(item => item.Id == versionId), token) is not { } version)
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.NotFound);
        if (!version.IsVersion(command.Version))
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.Conflict);
        if (!TimetableTransitions.IsValid(version.Status, TimetableStatus.Approved))
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.TimetableInvalidTransition);
        // MF11: a version that conflicts with today's school data is not approved; one whose data merely changed (no conflict) is.
        if (await SchedulingInputBuilder.BuildAsync(store, version.AcademicYearId, token) is { } today
            && CurrentDataAnalyzer.Analyze(today, PlacedOf(version), version.Mode == GenerationModes.DoublePeriods).HasConflicts)
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.TimetableConflictsWithCurrentData);
        await store.ExecuteInTransactionAsync(async () =>
        {
            // The filtered unique index allows one approved version per year: the old approved version is archived first.
            foreach (var previous in await store.ListAsync(store.Query<TimetableVersion>()
                         .Where(item => item.AcademicYearId == version.AcademicYearId && item.IsApproved && item.Id != version.Id), token))
            {
                previous.Archive(clock.GetUtcNow());
                AuditTrail.Record(store, clock, AuditEvents.TimetableArchived, $"timetable:{previous.Id}", $"Timetable version {previous.Number} archived by a new approval.",
                    new { number = previous.Number, byApproval = true });
            }
            await store.SaveChangesAsync(token);
            version.Approve(clock.GetUtcNow());
            AuditTrail.Record(store, clock, AuditEvents.TimetableApproved, $"timetable:{version.Id}", $"Timetable version {version.Number} approved.", new { number = version.Number });
            await store.SaveChangesAsync(token);
        }, token);
        return OperationResult.Success(Summary(version, version.InputHash));
    }

    /// <summary>MF11: «الفحص على البيانات الحالية» and what changed since the version was made. Read-only: saved and approved versions never change by themselves.</summary>
    public async Task<OperationResult<CurrentCheckDto>> CurrentCheckAsync(long versionId, CancellationToken token)
    {
        if (await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == versionId), token) is not { } version)
            return OperationResult.Failure<CurrentCheckDto>(ErrorCodes.NotFound);
        if (await SchedulingInputBuilder.BuildAsync(store, version.AcademicYearId, token) is not { } today)
            return OperationResult.Failure<CurrentCheckDto>(ErrorCodes.NotFound);
        var snapshot = SnapshotOf(version);
        var analysis = CurrentDataAnalyzer.Analyze(today, PlacedOf(version), version.Mode == GenerationModes.DoublePeriods);
        var stale = !string.Equals(version.InputHash, SchedulingInputHash.Compute(today), StringComparison.Ordinal);
        var changes = InputDiff.Compare(snapshot, today).ToList();
        if (stale && changes.Count == 0)
            changes.Add(new InputChange(InputChangeCodes.OtherChange));
        return OperationResult.Success(new CurrentCheckDto(version.Id, stale, analysis.Findings, changes, analysis.Reassigned > 0 && analysis.OnlyReassignments, analysis.HasConflicts, NamesOf(snapshot, today)));
    }

    /// <summary>
    /// MF11 «استبدال المعلم في الجدول»: puts today's teacher into the lessons whose assignment changed, in the same slots, as a NEW
    /// draft made from today's data. Refused (the owner is sent to the repair) when that would break any rule.
    /// </summary>
    public async Task<OperationResult<TimetableVersionSummary>> ReplaceTeachersAsync(long versionId, ReplaceTeachersCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == versionId), token) is not { } version)
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.NotFound);
        if (!version.IsVersion(command.Version))
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.Conflict);
        if (await SchedulingInputBuilder.BuildAsync(store, version.AcademicYearId, token) is not { } today)
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.NotFound);
        var doubles = version.Mode == GenerationModes.DoublePeriods;
        var analysis = CurrentDataAnalyzer.Analyze(today, PlacedOf(version), doubles);
        if (analysis.Reassigned == 0)
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.TimetableNothingToReplace);
        if (!analysis.OnlyReassignments)
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.TimetableReplaceConflict);
        var score = TimetableScorer.Score(today, analysis.Candidate, doubles);
        var number = await GenerationService.NextNumberAsync(store, version.AcademicYearId, token);
        var replaced = TimetableVersion.Create(version.AcademicYearId, number, TimetableSource.TeacherReplaced, version.GenerationRunId, version.Id, version.Mode,
            SchedulingInputHash.Compute(today), GenerationJson.Serialize(today), score.Total, GenerationJson.Serialize(score), null,
            analysis.Candidate.Select(lesson => new TimetableLesson(lesson.SectionId, lesson.LineId, lesson.TeacherId, lesson.Day, lesson.Lesson)), clock.GetUtcNow());
        store.Add(replaced);
        AuditTrail.Record(store, clock, AuditEvents.TimetableTeacherReplaced, $"timetable:{version.Id}", $"Timetable version {number} saved from version {version.Number} with {analysis.Reassigned} lessons given to their new teacher.",
            new { number, from = version.Number, replaced = analysis.Reassigned });
        await store.SaveChangesAsync(token);
        return OperationResult.Success(Summary(replaced, replaced.InputHash));
    }

    public static IReadOnlyList<PlacedLesson> PlacedOf(TimetableVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return version.Lessons.Select(lesson => new PlacedLesson(lesson.SectionId, lesson.CurriculumEntryId, lesson.TeacherId, lesson.Day, lesson.LessonNumber)).ToArray();
    }

    private static CurrentCheckNames NamesOf(SchedulingInput snapshot, SchedulingInput today)
    {
        static IReadOnlyList<NamedItem> Merge(IEnumerable<NamedItem> current, IEnumerable<NamedItem> old) =>
            current.Concat(old).GroupBy(item => item.Id).Select(group => group.First()).OrderBy(item => item.Id).ToArray();
        var stagesNow = today.Stages ?? today.Sections.GroupBy(section => section.StageId).Select(group => new StageInput(group.Key, group.First().StageName)).ToArray();
        var stagesOld = snapshot.Stages ?? snapshot.Sections.GroupBy(section => section.StageId).Select(group => new StageInput(group.Key, group.First().StageName)).ToArray();
        return new CurrentCheckNames(
            Merge(today.Teachers.Select(item => new NamedItem(item.Id, item.Name)), snapshot.Teachers.Select(item => new NamedItem(item.Id, item.Name))),
            today.Sections.Concat(snapshot.Sections).GroupBy(section => section.Id).Select(group => new NamedSection(group.Key, group.First().StageName, group.First().Label)).OrderBy(item => item.Id).ToArray(),
            Merge(today.Subjects.Select(item => new NamedItem(item.Id, item.Name)), snapshot.Subjects.Select(item => new NamedItem(item.Id, item.Name))),
            Merge(stagesNow.Select(item => new NamedItem(item.Id, item.Name)), stagesOld.Select(item => new NamedItem(item.Id, item.Name))),
            Merge(today.Resources.Select(item => new NamedItem(item.Id, item.Name)), snapshot.Resources.Select(item => new NamedItem(item.Id, item.Name))),
            Merge(today.Shifts.Select(item => new NamedItem(item.Id, item.Name)), snapshot.Shifts.Select(item => new NamedItem(item.Id, item.Name))));
    }

    /// <summary>Draft or Approved → Archived (final). The version itself is never deleted or changed otherwise.</summary>
    public async Task<OperationResult<TimetableVersionSummary>> ArchiveAsync(long versionId, ArchiveTimetableCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FirstOrDefaultAsync(store.Query<TimetableVersion>().Where(item => item.Id == versionId), token) is not { } version)
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.NotFound);
        if (!version.IsVersion(command.Version))
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.Conflict);
        if (!version.Archive(clock.GetUtcNow()))
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.TimetableInvalidTransition);
        AuditTrail.Record(store, clock, AuditEvents.TimetableArchived, $"timetable:{version.Id}", $"Timetable version {version.Number} archived.", new { number = version.Number, byApproval = false });
        await store.SaveChangesAsync(token);
        return OperationResult.Success(Summary(version, version.InputHash));
    }

    /// <summary>
    /// Restores an older version as a NEW draft version (source «rolledBack») linked to it. The older version, whatever its
    /// status, is not changed, and the current approved version stays approved until the owner approves the new one.
    /// </summary>
    public async Task<OperationResult<TimetableVersionSummary>> RollbackAsync(long versionId, RollbackTimetableCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == versionId), token) is not { } older)
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.NotFound);
        if (!older.IsVersion(command.Version))
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.Conflict);
        if (command.Note is { Length: > TimetableVersion.NoteMaxLength })
            return OperationResult.Invalid<TimetableVersionSummary>(nameof(command.Note), ErrorCodes.ValueTooLong);
        var number = await GenerationService.NextNumberAsync(store, older.AcademicYearId, token);
        var restored = TimetableVersion.Create(older.AcademicYearId, number, TimetableSource.RolledBack, older.GenerationRunId, older.Id, older.Mode, older.InputHash,
            older.InputJson, older.Score, older.ScoreJson, command.Note, older.Lessons, clock.GetUtcNow());
        store.Add(restored);
        AuditTrail.Record(store, clock, AuditEvents.TimetableRolledBack, $"timetable:{older.Id}", $"Timetable version {older.Number} restored as version {number}.",
            new { number, from = older.Number });
        await store.SaveChangesAsync(token);
        return OperationResult.Success(Summary(restored, restored.InputHash));
    }

    /// <summary>Added, removed, moved and teacher-changed lessons going from <paramref name="fromId"/> to <paramref name="toId"/> (same year only).</summary>
    public async Task<OperationResult<TimetableComparisonDto>> CompareAsync(long fromId, long toId, CancellationToken token)
    {
        var from = await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == fromId), token);
        var to = await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == toId), token);
        if (from is null || to is null)
            return OperationResult.Failure<TimetableComparisonDto>(ErrorCodes.NotFound);
        if (from.AcademicYearId != to.AcademicYearId)
            return OperationResult.Failure<TimetableComparisonDto>(ErrorCodes.TimetableCompareYearMismatch);
        var subjectOfLine = new Dictionary<long, long>();
        foreach (var line in SnapshotOf(from).Lines.Concat(SnapshotOf(to).Lines))
            subjectOfLine[line.Id] = line.SubjectId;
        var (totals, changes) = TimetableComparer.Compare(from.Lessons, to.Lessons, subjectOfLine);
        return OperationResult.Success(new TimetableComparisonDto(from.Id, to.Id, from.Number, to.Number, totals,
            TimetableComparer.BySection(changes), TimetableComparer.ByTeacher(changes), changes));
    }

    /// <summary>«تعديل يدوي»: checks an edited timetable against every hard rule of the version's own input.</summary>
    public async Task<OperationResult<TimetableCheckDto>> CheckAsync(long versionId, EditedTimetableCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == versionId), token) is not { } version)
            return OperationResult.Failure<TimetableCheckDto>(ErrorCodes.NotFound);
        if (command.Lessons is null)
            return OperationResult.Invalid<TimetableCheckDto>(nameof(command.Lessons), ErrorCodes.Required);
        return OperationResult.Success(Check(version, command.Lessons));
    }

    /// <summary>
    /// Saves an edited timetable as a NEW version linked to its parent (the parent never changes), with an audit
    /// entry. Refused with <see cref="ErrorCodes.TimetableHasViolations"/> when any hard rule is broken.
    /// </summary>
    public async Task<OperationResult<TimetableVersionSummary>> SaveEditAsync(long versionId, EditedTimetableCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == versionId), token) is not { } parent)
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.NotFound);
        if (command.Lessons is null || command.Lessons.Count == 0)
            return OperationResult.Invalid<TimetableVersionSummary>(nameof(command.Lessons), ErrorCodes.Required);
        if (command.Note is { Length: > TimetableVersion.NoteMaxLength })
            return OperationResult.Invalid<TimetableVersionSummary>(nameof(command.Note), ErrorCodes.ValueTooLong);
        var check = Check(parent, command.Lessons);
        if (check.Violations.Count > 0)
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.TimetableHasViolations);
        var number = await GenerationService.NextNumberAsync(store, parent.AcademicYearId, token);
        var edited = TimetableVersion.Create(parent.AcademicYearId, number, TimetableSource.Edited, parent.GenerationRunId, parent.Id, parent.Mode, parent.InputHash,
            parent.InputJson, check.Score.Total, GenerationJson.Serialize(check.Score), command.Note,
            command.Lessons.Select(lesson => new TimetableLesson(lesson.SectionId, lesson.LineId, lesson.TeacherId, lesson.Day, lesson.Lesson)), clock.GetUtcNow());
        store.Add(edited);
        var moved = command.Lessons.Count(lesson => !parent.Lessons.Contains(new TimetableLesson(lesson.SectionId, lesson.LineId, lesson.TeacherId, lesson.Day, lesson.Lesson)));
        AuditTrail.Record(store, clock, AuditEvents.TimetableEdited, $"timetable:{parent.Id}", $"Timetable version {number} saved from version {parent.Number} with {moved} moved lessons.",
            new { number, from = parent.Number, moved });
        await store.SaveChangesAsync(token);
        return OperationResult.Success(Summary(edited, edited.InputHash));
    }

    private static TimetableCheckDto Check(TimetableVersion version, IReadOnlyList<GridLesson> lessons)
    {
        var input = SnapshotOf(version);
        var placed = lessons.Select(lesson => new PlacedLesson(lesson.SectionId, lesson.LineId, lesson.TeacherId, lesson.Day, lesson.Lesson)).ToArray();
        var doubles = version.Mode == GenerationModes.DoublePeriods;
        var violations = TimetableVerifier.Verify(input, placed, doubles)
            .Select(item => new ViolationDto(item.Code, item.Rule, item.SectionId, item.TeacherId, item.SubjectId, item.ResourceId, item.Day, item.Lesson, item.Count, item.Limit))
            .ToArray();
        return new TimetableCheckDto(violations, TimetableScorer.Score(input, placed, doubles));
    }

    public static SchedulingInput SnapshotOf(TimetableVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return GenerationJson.Deserialize<SchedulingInput>(version.InputJson) ?? throw new JsonException("Empty timetable snapshot.");
    }

    public static TimetableVersionSummary Summary(TimetableVersion version, string currentHash)
    {
        ArgumentNullException.ThrowIfNull(version);
        return new TimetableVersionSummary(version.Id, version.Version, version.Number, JsonNamingPolicy.CamelCase.ConvertName(version.Source.ToString()),
            version.GenerationRunId, version.ParentVersionId, version.Mode, version.Score, version.Lessons.Count, version.CreatedAt, version.IsApproved,
            version.ApprovedAt, version.Note, !string.Equals(version.InputHash, currentHash, StringComparison.Ordinal),
            JsonNamingPolicy.CamelCase.ConvertName(version.Status.ToString()), version.ArchivedAt);
    }

    /// <summary>The year's daily sessions for a timetable of that year (null for a single session).</summary>
    public static async Task<GridSessions?> SessionsAsync(IDataStore store, long yearId, SchedulingInput input, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(input);
        var plan = await store.FirstOrDefaultAsync(store.Read<SessionPlan>().Where(item => item.AcademicYearId == yearId), token);
        if (plan is null || plan.System == SessionSystem.OneSession || input.Shifts.FirstOrDefault(shift => shift.Id == plan.ShiftId) is not { } shift)
            return null;
        static GridLessonTime[] Lessons(IEnumerable<(string Kind, int Position, int Start, int End)> rows) =>
            rows.Where(row => row.Kind == nameof(PeriodKind.Lesson)).OrderBy(row => row.Position)
                .Select((row, index) => new GridLessonTime(index + 1, row.Start, row.End)).ToArray();
        static int Minutes(TimeOnly time) => time.Hour * 60 + time.Minute;
        var timings = new List<GridSessionTiming>
        {
            new(ApiText.ToValue(SessionKind.Morning), Lessons((shift.Periods ?? []).Select(period => (period.Kind, period.Position, period.StartMinute, period.EndMinute)))),
        };
        timings.AddRange(plan.Periods.GroupBy(period => period.Session).OrderBy(group => group.Key).Select(group => new GridSessionTiming(ApiText.ToValue(group.Key),
            Lessons(group.Select(period => (period.Kind.ToString(), period.Position, Minutes(period.StartTime), Minutes(period.EndTime)))))));
        return new GridSessions(ApiText.ToValue(plan.System), shift.Id, timings,
            plan.Days.Where(day => input.WorkingDays.Contains(day.Day)).Select(day => new GridSessionDay(day.Term, day.Day, ApiText.ToValue(day.Session))).ToArray());
    }

    public static TimetableDto ToDto(TimetableVersion version, SchedulingInput input, string currentHash, GridSessions? sessions = null)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(input);
        var lines = input.Lines.ToDictionary(line => line.Id);
        var lessons = version.Lessons
            .Select(lesson => new GridLesson(lesson.SectionId, lesson.CurriculumEntryId, lines.TryGetValue(lesson.CurriculumEntryId, out var line) ? line.SubjectId : 0,
                lesson.TeacherId, lesson.Day, lesson.LessonNumber))
            .OrderBy(lesson => lesson.SectionId).ThenBy(lesson => lesson.Day).ThenBy(lesson => lesson.Lesson).ToArray();
        var placed = version.Lessons.Select(lesson => new PlacedLesson(lesson.SectionId, lesson.CurriculumEntryId, lesson.TeacherId, lesson.Day, lesson.LessonNumber)).ToArray();
        var doubles = version.Mode == GenerationModes.DoublePeriods;
        var usedTeachers = lessons.Select(lesson => lesson.TeacherId).ToHashSet();
        var usedSubjects = lessons.Select(lesson => lesson.SubjectId).ToHashSet();
        return new TimetableDto(
            Summary(version, currentHash),
            input.WorkingDays,
            input.Shifts.Select(shift => new GridShift(shift.Id, shift.Name, (shift.Periods ?? [])
                .Where(period => period.Kind == nameof(PeriodKind.Lesson)).OrderBy(period => period.Position)
                .Select((period, index) => new GridLessonTime(index + 1, period.StartMinute, period.EndMinute)).ToArray())).ToArray(),
            input.Sections.Select(section => new GridSection(section.Id, section.StageName, section.Label, section.ShiftId, section.AllowedByDay)).ToArray(),
            input.Subjects.Where(subject => usedSubjects.Contains(subject.Id)).Select(subject => new GridSubject(subject.Id, subject.Name, subject.ColorIndex)).ToArray(),
            input.Teachers.Where(teacher => usedTeachers.Contains(teacher.Id))
                .Select(teacher => new GridTeacher(teacher.Id, teacher.Name, string.IsNullOrWhiteSpace(teacher.ShortName) ? teacher.Name : teacher.ShortName)).ToArray(),
            lessons,
            TimetableScorer.Score(input, placed, doubles),
            TimetableVerifier.Verify(input, placed, doubles).Count,
            sessions);
    }
}
