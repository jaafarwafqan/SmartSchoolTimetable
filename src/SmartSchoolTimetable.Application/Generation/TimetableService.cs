using System.Text.Json;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Generation;
using PeriodKind = SmartSchoolTimetable.Domain.SchoolSetup.PeriodKind;

namespace SmartSchoolTimetable.Application.Generation;

/// <param name="Stale">The school data changed after this version was made (its input hash differs from today's).</param>
public sealed record TimetableVersionSummary(long Id, int Version, int Number, string Source, long? GenerationRunId, long? ParentVersionId, string Mode,
    long? Score, int Lessons, DateTimeOffset CreatedAt, bool IsApproved, DateTimeOffset? ApprovedAt, string? Note, bool Stale);

public sealed record GridLessonTime(int Number, int StartMinute, int EndMinute);
public sealed record GridShift(long Id, string Name, IReadOnlyList<GridLessonTime> Lessons);
public sealed record GridSection(long Id, string StageName, string Label, long ShiftId, IReadOnlyList<DayLessons> AllowedByDay);
public sealed record GridSubject(long Id, string Name, int ColorIndex);
public sealed record GridTeacher(long Id, string Name, string ShortName);
public sealed record GridLesson(long SectionId, long LineId, long SubjectId, long TeacherId, int Day, int Lesson);

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
    int Violations);

public sealed record ApproveTimetableCommand(int Version);

/// <summary>The whole edited timetable (the editor moves or swaps lessons locally and sends every lesson).</summary>
public sealed record EditedTimetableCommand(IReadOnlyList<GridLesson>? Lessons, string? Note);

public sealed record ViolationDto(string Code, string Rule, long? SectionId, long? TeacherId, long? SubjectId, long? ResourceId, int? Day, int? Lesson, int? Count, int? Limit);

public sealed record TimetableCheckDto(IReadOnlyList<ViolationDto> Violations, TimetableScore Score);

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
        return OperationResult.Success(ToDto(version, input, current is null ? version.InputHash : SchedulingInputHash.Compute(current)));
    }

    public async Task<OperationResult<TimetableVersionSummary>> ApproveAsync(long versionId, ApproveTimetableCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FirstOrDefaultAsync(store.Query<TimetableVersion>().Where(item => item.Id == versionId), token) is not { } version)
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.NotFound);
        if (!version.IsVersion(command.Version))
            return OperationResult.Failure<TimetableVersionSummary>(ErrorCodes.Conflict);
        if (!version.IsApproved)
        {
            await store.ExecuteInTransactionAsync(async () =>
            {
                // The filtered unique index allows one approved version per year: clear the old one first.
                foreach (var previous in await store.ListAsync(store.Query<TimetableVersion>()
                             .Where(item => item.AcademicYearId == version.AcademicYearId && item.IsApproved && item.Id != version.Id), token))
                    previous.Unapprove();
                await store.SaveChangesAsync(token);
                version.Approve(clock.GetUtcNow());
                AuditTrail.Record(store, clock, "TimetableApproved", $"timetable:{version.Id}", $"Timetable version {version.Number} approved.");
                await store.SaveChangesAsync(token);
            }, token);
        }
        return OperationResult.Success(Summary(version, version.InputHash));
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
        AuditTrail.Record(store, clock, "TimetableEdited", $"timetable:{parent.Id}", $"Timetable version {number} saved from version {parent.Number} with {moved} moved lessons.");
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
            version.ApprovedAt, version.Note, !string.Equals(version.InputHash, currentHash, StringComparison.Ordinal));
    }

    public static TimetableDto ToDto(TimetableVersion version, SchedulingInput input, string currentHash)
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
            TimetableVerifier.Verify(input, placed, doubles).Count);
    }
}
