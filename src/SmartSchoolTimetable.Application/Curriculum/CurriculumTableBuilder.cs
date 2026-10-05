using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Curriculum;

/// <summary>
/// Builds the curriculum table of a year: rows = active subjects (a main row plus one row per extra label),
/// columns = active stages, cells = weekly lessons; totals per stage against the capacity of its sections per shift.
/// </summary>
internal static class CurriculumTableBuilder
{
    public static async Task<CurriculumTableDto> BuildAsync(IDataStore store, long yearId, CancellationToken token)
    {
        var stages = await store.ListAsync(
            store.Query<Stage>().Where(stage => stage.AcademicYearId == yearId && !stage.IsArchived).OrderBy(stage => stage.DisplayOrder).ThenBy(stage => stage.NormalizedName),
            token);
        var stageIds = stages.Select(stage => stage.Id).ToArray();
        var subjects = await store.ListAsync(store.Query<Subject>().Where(subject => !subject.IsArchived).OrderBy(subject => subject.NormalizedName), token);
        var entries = await store.ListAsync(
            store.Query<CurriculumEntry>().Where(entry => stageIds.Contains(entry.StageId) && !entry.IsArchived).OrderBy(entry => entry.Id), token);
        var sections = await store.ListAsync(store.Query<Section>().Where(section => stageIds.Contains(section.StageId) && !section.IsArchived), token);
        var shifts = (await store.ListAsync(store.Query<Shift>().Where(shift => shift.AcademicYearId == yearId), token)).ToDictionary(shift => shift.Id);
        var week = await store.FirstOrDefaultAsync(store.Query<WorkingWeek>(), token);

        var stageDtos = stages.Select(stage =>
        {
            var planned = entries.Where(entry => entry.StageId == stage.Id).Sum(entry => entry.WeeklyLessons);
            var capacities = sections.Where(section => section.StageId == stage.Id)
                .GroupBy(section => section.ShiftId)
                .Select(group => shifts.GetValueOrDefault(group.Key) is { } shift
                    ? new ShiftCapacity(shift.Id, shift.Name, group.Count(), Section.WeeklyCapacity(week, shift, stage))
                    : null)
                .OfType<ShiftCapacity>()
                .OrderBy(capacity => capacity.ShiftName, StringComparer.Ordinal);
            var totals = CurriculumTotals.For(planned, capacities)
                .Select(total => new ShiftTotalDto(total.ShiftId, total.ShiftName, total.Sections, total.WeeklyCapacity, ApiText.ToValue(total.Status), total.Difference))
                .ToArray();
            return new CurriculumStageDto(stage.Id, stage.Name, planned, totals);
        }).ToArray();

        var rows = new List<CurriculumRowDto>();
        foreach (var subject in subjects)
        {
            var subjectEntries = entries.Where(entry => entry.SubjectId == subject.Id).ToArray();
            var labels = subjectEntries.Where(entry => entry.NormalizedLabel.Length > 0)
                .GroupBy(entry => entry.NormalizedLabel).Select(group => group.First().Label).ToList();
            foreach (var label in labels.Prepend(null))
            {
                var normalized = ArabicText.Normalize(label);
                var cells = stages.Select(stage =>
                {
                    var matches = subjectEntries.Where(entry => entry.StageId == stage.Id && entry.NormalizedLabel == normalized).ToArray();
                    var first = matches.FirstOrDefault();
                    return new CurriculumCellDto(stage.Id, first?.Id, first?.WeeklyLessons, first?.Version, Math.Max(0, matches.Length - 1));
                }).ToArray();
                rows.Add(new CurriculumRowDto(subject.Id, subject.Name, subject.ColorIndex, label, cells));
            }
        }
        return new CurriculumTableDto(stageDtos, rows);
    }
}
