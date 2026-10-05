using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Domain.Workload;

namespace SmartSchoolTimetable.Application.Workload;

/// <summary>
/// Everything the workload screens need for one academic year, loaded with a fixed number of queries (no N+1):
/// active stages, sections, lines, the subjects and teachers they name, and the year's assignments. Loads and
/// availability are computed in memory from it.
/// </summary>
internal sealed class WorkloadData
{
    public const string Within = "within";
    public const string Near = "near";
    public const string Over = "over";

    private WorkloadData()
    {
    }

    public required AcademicYear Year { get; init; }
    public required IReadOnlyList<int> Days { get; init; }
    public required IReadOnlyDictionary<long, Shift> Shifts { get; init; }
    public required IReadOnlyList<Stage> Stages { get; init; }
    public required IReadOnlyList<Section> Sections { get; init; }
    public required IReadOnlyList<CurriculumEntry> Entries { get; init; }
    public required IReadOnlyDictionary<long, Subject> Subjects { get; init; }
    public required IReadOnlyDictionary<long, Teacher> Teachers { get; init; }

    /// <summary>The year's assignments (tracked when loaded for a change, so they can be edited and saved).</summary>
    public required List<WorkloadAssignment> Assignments { get; init; }

    public static async Task<WorkloadData?> LoadAsync(IDataStore store, long yearId, bool forUpdate, CancellationToken token)
    {
        if (await store.FirstOrDefaultAsync(store.Read<AcademicYear>().Where(year => year.Id == yearId), token) is not { } year)
            return null;
        var week = await store.FirstOrDefaultAsync(store.Read<WorkingWeek>(), token) ?? WorkingWeek.CreateDefault();
        var shifts = await store.ListAsync(store.Read<Shift>().Where(shift => shift.AcademicYearId == yearId), token);
        var stages = await store.ListAsync(store.Read<Stage>().Where(stage => stage.AcademicYearId == yearId && !stage.IsArchived)
            .OrderBy(stage => stage.DisplayOrder).ThenBy(stage => stage.NormalizedName), token);
        var stageIds = stages.Select(stage => stage.Id).ToArray();
        var sections = await store.ListAsync(store.Read<Section>().Where(section => stageIds.Contains(section.StageId) && !section.IsArchived)
            .OrderBy(section => section.NormalizedLabel), token);
        var entries = await store.ListAsync(store.Read<CurriculumEntry>().Where(entry => stageIds.Contains(entry.StageId) && !entry.IsArchived)
            .OrderBy(entry => entry.Id), token);
        var subjects = await store.ListAsync(store.Read<Subject>(), token);
        var teachers = await store.ListAsync(store.Read<Teacher>(), token);
        var sectionIds = sections.Select(section => section.Id).ToArray();
        var assignmentQuery = forUpdate ? store.Query<WorkloadAssignment>() : store.Read<WorkloadAssignment>();
        var assignments = await store.ListAsync(assignmentQuery.Where(row => sectionIds.Contains(row.SectionId) && !row.IsArchived), token);
        var entryIds = entries.Select(entry => entry.Id).ToHashSet();
        return new WorkloadData
        {
            Year = year,
            Days = week.Days,
            Shifts = shifts.ToDictionary(shift => shift.Id),
            Stages = stages,
            Sections = sections,
            Entries = entries.OrderBy(entry => subjects.FirstOrDefault(subject => subject.Id == entry.SubjectId)?.NormalizedName, StringComparer.Ordinal)
                .ThenBy(entry => entry.NormalizedLabel, StringComparer.Ordinal).ToArray(),
            Subjects = subjects.ToDictionary(subject => subject.Id),
            Teachers = teachers.ToDictionary(teacher => teacher.Id),
            // Assignments of archived lines were archived with them; any left over are ignored here.
            Assignments = assignments.Where(row => entryIds.Contains(row.CurriculumEntryId)).ToList(),
        };
    }

    public IEnumerable<Section> SectionsOf(long stageId) => Sections.Where(section => section.StageId == stageId);

    public IEnumerable<CurriculumEntry> EntriesOf(long stageId) => Entries.Where(entry => entry.StageId == stageId);

    public CurriculumEntry? Entry(long id) => Entries.FirstOrDefault(entry => entry.Id == id);

    public Section? Section(long id) => Sections.FirstOrDefault(section => section.Id == id);

    public Stage StageOf(Section section) => Stages.First(stage => stage.Id == section.StageId);

    public WorkloadAssignment? Assignment(long sectionId, long entryId) =>
        Assignments.FirstOrDefault(row => row.SectionId == sectionId && row.CurriculumEntryId == entryId && !row.IsArchived);

    public int LessonsOf(WorkloadAssignment assignment) => Entry(assignment.CurriculumEntryId)?.WeeklyLessons ?? 0;

    public string SubjectName(CurriculumEntry entry) => Subjects.TryGetValue(entry.SubjectId, out var subject) ? subject.Name : string.Empty;

    public string ShiftName(Section section) => Shifts.TryGetValue(section.ShiftId, out var shift) ? shift.Name : string.Empty;

    public bool OutsideSpecialization(long teacherId, CurriculumEntry entry) =>
        Teachers.TryGetValue(teacherId, out var teacher) && teacher.Specializations.All(item => item.SubjectId != entry.SubjectId);

    /// <summary>Fully released for the whole year (no dates, or dates covering it). A partial release only warns (3D).</summary>
    public bool ReleasedForYear(Teacher teacher) =>
        teacher.FullyReleased
        && (teacher.ReleaseFrom is null || teacher.ReleaseFrom <= Year.StartDate)
        && (teacher.ReleaseTo is null || teacher.ReleaseTo >= Year.EndDate);

    /// <summary>The allowed slots of a section: its stage's lessons on each working day in its shift.</summary>
    public IEnumerable<ShiftSlot> SlotsOf(Section section) =>
        Shifts.TryGetValue(section.ShiftId, out var shift)
            ? TeacherAvailability.SectionSlots(shift.Id, Days, day => StageOf(section).LessonsOn(day, shift))
            : [];

    /// <summary>
    /// Availability from the teacher's sections; a teacher without assignments yet is measured against every
    /// shift of the year (the most they could ever teach).
    /// </summary>
    public TeacherAvailabilityResult AvailabilityOf(Teacher teacher, IEnumerable<WorkloadAssignment> assignments)
    {
        var sectionIds = assignments.Select(row => row.SectionId).Distinct().ToArray();
        var slots = sectionIds.Length > 0
            ? sectionIds.Select(Section).OfType<Section>().SelectMany(SlotsOf)
            : Shifts.Values.SelectMany(shift => TeacherAvailability.SectionSlots(shift.Id, Days, shift.LessonsOn));
        return TeacherAvailability.Compute(slots, teacher.OffDays, teacher.BlockedPeriods, teacher.MaxLessonsPerDay, teacher.MaxLessonsPerWeek, ReleasedForYear(teacher));
    }

    public int AssignedLessons(long teacherId) => Assignments.Where(row => row.TeacherId == teacherId && !row.IsArchived).Sum(LessonsOf);

    public int LimitOf(Teacher teacher) => AvailabilityOf(teacher, Assignments.Where(row => row.TeacherId == teacher.Id && !row.IsArchived)).Available;

    /// <summary>«ضمن الحد» / «قريب» (90% of the limit or more) / «تجاوز» (DECISIONS_PENDING #58).</summary>
    public static string Status(int assigned, int limit) =>
        assigned > limit ? Over : limit > 0 && assigned * 10 >= limit * 9 ? Near : Within;
}
