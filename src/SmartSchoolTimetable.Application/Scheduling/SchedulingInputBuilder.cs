using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.Resources;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Domain.Workload;

namespace SmartSchoolTimetable.Application.Scheduling;

/// <summary>
/// Reads one academic year into a <see cref="SchedulingInput"/> (Phase 3 §2.5) with a fixed number of no-tracking
/// queries. Active stages, sections and lines; the subjects, teachers and resources they use plus every active one;
/// active assignments of those sections and lines; the single profile. Lists come out in canonical order.
/// </summary>
public static class SchedulingInputBuilder
{
    public static async Task<SchedulingInput?> BuildAsync(IDataStore store, long yearId, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (await store.FirstOrDefaultAsync(store.Read<AcademicYear>().Where(year => year.Id == yearId), token) is not { } year)
            return null;
        var week = await store.FirstOrDefaultAsync(store.Read<WorkingWeek>(), token) ?? WorkingWeek.CreateDefault();
        var days = week.Days;
        var shifts = await store.ListAsync(store.Read<Shift>().Where(shift => shift.AcademicYearId == yearId), token);
        var stages = await store.ListAsync(store.Read<Stage>().Where(stage => stage.AcademicYearId == yearId && !stage.IsArchived), token);
        var stageIds = stages.Select(stage => stage.Id).ToArray();
        var sections = await store.ListAsync(store.Read<Section>().Where(section => stageIds.Contains(section.StageId) && !section.IsArchived), token);
        var lines = await store.ListAsync(store.Read<CurriculumEntry>().Where(entry => stageIds.Contains(entry.StageId) && !entry.IsArchived), token);
        var sectionIds = sections.Select(section => section.Id).ToArray();
        var lineIds = lines.Select(line => line.Id).ToArray();
        var assignments = await store.ListAsync(store.Read<WorkloadAssignment>()
            .Where(row => !row.IsArchived && sectionIds.Contains(row.SectionId) && lineIds.Contains(row.CurriculumEntryId)), token);
        var usedSubjects = lines.Select(line => line.SubjectId).Distinct().ToArray();
        var subjects = await store.ListAsync(store.Read<Subject>().Where(subject => !subject.IsArchived || usedSubjects.Contains(subject.Id)), token);
        var usedTeachers = assignments.Select(row => row.TeacherId).Distinct().ToArray();
        var teachers = await store.ListAsync(store.Read<Teacher>().Where(teacher => !teacher.IsArchived || usedTeachers.Contains(teacher.Id)), token);
        var usedResources = subjects.Select(subject => subject.RequiredResourceId).OfType<long>().Distinct().ToArray();
        var resources = await store.ListAsync(store.Read<Resource>().Where(resource => !resource.IsArchived || usedResources.Contains(resource.Id)), token);
        var profile = await store.FirstOrDefaultAsync(store.Read<SchedulingProfile>(), token) ?? SchedulingProfile.CreateDefault();

        var shiftById = shifts.ToDictionary(shift => shift.Id);
        var stageById = stages.ToDictionary(stage => stage.Id);
        var input = new SchedulingInput(
            SchedulingInput.CurrentFormatVersion,
            yearId,
            days,
            shifts.Select(shift => ShiftOf(shift, days)).ToArray(),
            sections.Select(section =>
            {
                var stage = stageById[section.StageId];
                var allowed = shiftById.TryGetValue(section.ShiftId, out var shift)
                    ? days.Select(day => new DayLessons(day, stage.LessonsOn(day, shift))).ToArray()
                    : days.Select(day => new DayLessons(day, 0)).ToArray();
                return new SectionInput(section.Id, section.StageId, section.ShiftId, stage.Name, section.Label, allowed);
            }).ToArray(),
            subjects.Select(subject => new SubjectInput(subject.Id, subject.Name, subject.Priority, subject.DistributionEnabled, subject.SpreadAcrossDays,
                subject.Heavy, subject.RequiresDoublePeriod, subject.RequiredResourceId,
                subject.BlockedPeriods.Select(period => new SlotRef(period.Day, period.LessonNumber)).ToArray(), subject.ColorIndex)).ToArray(),
            lines.Select(line => new LineInput(line.Id, line.StageId, line.SubjectId, line.Label, line.WeeklyLessons, line.NeedsDoublePeriod)).ToArray(),
            assignments.Select(row => new AssignmentInput(row.Id, row.SectionId, row.CurriculumEntryId, row.TeacherId)).ToArray(),
            teachers.Select(teacher => new TeacherInput(teacher.Id, teacher.FullName, teacher.IsArchived,
                Released(teacher, year), PartiallyReleased(teacher, year), teacher.OffDays.ToArray(),
                teacher.BlockedPeriods.Select(period => new SlotRef(period.Day, period.LessonNumber)).ToArray(),
                teacher.MaxLessonsPerDay, teacher.MaxLessonsPerWeek, teacher.Specializations.Select(item => item.SubjectId).ToArray(),
                string.IsNullOrWhiteSpace(teacher.ShortName) ? null : teacher.ShortName)).ToArray(),
            resources.Select(resource => new ResourceInput(resource.Id, resource.Name, resource.Kind.ToString(), resource.Capacity, resource.IsArchived)).ToArray(),
            new ProfileInput(profile.ProfileVersion, profile.Rules.Select(rule => new RuleInput(rule.Key, rule.Enabled, rule.Weight)).ToArray()),
            stages.Select(stage => new StageInput(stage.Id, stage.Name)).ToArray());
        return SchedulingInputHash.Canonical(input);
    }

    /// <summary>Fully released with no dates, or with dates covering the whole year (DECISIONS_PENDING #61).</summary>
    public static bool Released(Teacher teacher, AcademicYear year)
    {
        ArgumentNullException.ThrowIfNull(teacher);
        ArgumentNullException.ThrowIfNull(year);
        return teacher.FullyReleased
            && (teacher.ReleaseFrom is null || teacher.ReleaseFrom <= year.StartDate)
            && (teacher.ReleaseTo is null || teacher.ReleaseTo >= year.EndDate);
    }

    private static bool PartiallyReleased(Teacher teacher, AcademicYear year) =>
        teacher.FullyReleased && !Released(teacher, year)
        && (teacher.ReleaseFrom is null || teacher.ReleaseFrom <= year.EndDate)
        && (teacher.ReleaseTo is null || teacher.ReleaseTo >= year.StartDate);

    private static ShiftInput ShiftOf(Shift shift, IReadOnlyList<int> days)
    {
        var periods = shift.Periods;
        var hasLessons = periods.Any(period => period.Kind == PeriodKind.Lesson);
        static int Minutes(TimeOnly time) => time.Hour * 60 + time.Minute;
        return new ShiftInput(shift.Id, shift.Name, days.Select(day => new DayLessons(day, shift.LessonsOn(day))).ToArray(),
            hasLessons ? Minutes(periods[0].StartTime) : null,
            hasLessons ? Minutes(periods[^1].EndTime) : null,
            periods.Select(period => new PeriodInput(period.Position, period.Kind.ToString(), Minutes(period.StartTime), Minutes(period.EndTime), period.StartBell, period.EndBell)).ToArray());
    }
}
