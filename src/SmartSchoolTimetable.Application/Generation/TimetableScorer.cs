using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Generation;

/// <summary>Total penalty and its breakdown per soft rule (lower is better).</summary>
public sealed record TimetableScore(long Total, IReadOnlyList<RuleScore> Rules);

/// <summary>
/// Scores a timetable on the soft rules S1–S5 with the profile weights (docs/SOLVER.md §3). The solver minimises the
/// same sum, so for a solver timetable <see cref="TimetableScore.Total"/> equals its objective (tested). The
/// definitions, per rule:
/// S1 spread: per (section, subject flagged spread, day), lessons above 1 (above 2 for a double subject).
/// S2 teacher gaps: per (teacher, day, shift), free lesson numbers between the first and the last lesson.
/// S3 heavy early: per lesson of a heavy subject, its lesson number − 1.
/// S4 repeats: per section and day, consecutive lesson numbers with the same subject (double subjects excluded).
/// S5 doubles (standard mode only): per double line, ⌊lessons ÷ 2⌋ minus the adjacent pairs it got.
/// </summary>
public static class TimetableScorer
{
    public static TimetableScore Score(SchedulingInput input, IReadOnlyList<PlacedLesson> lessons, bool doublePeriodsRequired)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(lessons);
        var lines = input.Lines.ToDictionary(line => line.Id);
        var subjects = input.Subjects.ToDictionary(subject => subject.Id);
        var sections = input.Sections.ToDictionary(section => section.Id);
        var items = lessons.Where(lesson => lines.ContainsKey(lesson.LineId) && sections.ContainsKey(lesson.SectionId)
                && subjects.ContainsKey(lines[lesson.LineId].SubjectId))
            .Select(lesson => (Lesson: lesson, Line: lines[lesson.LineId], Subject: subjects[lines[lesson.LineId].SubjectId], Section: sections[lesson.SectionId]))
            .ToArray();
        bool LineDouble(LineInput line) => line.NeedsDoublePeriod || subjects.TryGetValue(line.SubjectId, out var subject) && subject.RequiresDoublePeriod;
        bool SubjectDouble(long stageId, long subjectId) => input.Lines.Any(line => line.StageId == stageId && line.SubjectId == subjectId && LineDouble(line));

        var spread = items.Where(item => item.Subject.SpreadAcrossDays)
            .GroupBy(item => (item.Section.Id, item.Subject.Id, item.Lesson.Day))
            .Sum(group => (long)Math.Max(0, group.Count() - (SubjectDouble(group.First().Section.StageId, group.Key.Item2) ? 2 : 1)));

        var gaps = items.GroupBy(item => (item.Lesson.TeacherId, item.Lesson.Day, item.Section.ShiftId))
            .Sum(group =>
            {
                var used = group.Select(item => item.Lesson.Lesson).Distinct().ToArray();
                return (long)(used.Max() - used.Min() + 1 - used.Length);
            });

        var heavy = items.Where(item => item.Subject.Heavy).Sum(item => (long)(item.Lesson.Lesson - 1));

        var repeats = items.GroupBy(item => (item.Section.Id, item.Lesson.Day))
            .Sum(group =>
            {
                var bySlot = group.GroupBy(item => item.Lesson.Lesson).ToDictionary(slot => slot.Key, slot => slot.Select(item => item.Subject.Id).ToHashSet());
                long count = 0;
                foreach (var (lesson, subjectIds) in bySlot)
                {
                    if (!bySlot.TryGetValue(lesson + 1, out var next))
                        continue;
                    count += subjectIds.Count(id => next.Contains(id) && !SubjectDouble(group.First().Section.StageId, id));
                }
                return count;
            });

        long doubles = 0;
        if (!doublePeriodsRequired)
        {
            var shifts = input.Shifts.ToDictionary(shift => shift.Id);
            foreach (var group in items.Where(item => LineDouble(item.Line)).GroupBy(item => (item.Section.Id, item.Line.Id)))
            {
                var target = group.First().Line.WeeklyLessons / 2;
                var shift = shifts.GetValueOrDefault(group.First().Section.ShiftId);
                var pairs = group.GroupBy(item => item.Lesson.Day).Sum(day => Pairs(shift, day.Select(item => item.Lesson.Lesson)));
                doubles += target - Math.Min(target, pairs);
            }
        }

        var penalties = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [SchedulingRuleKeys.SpreadSubjectsAcrossDays] = spread,
            [SchedulingRuleKeys.AvoidTeacherGaps] = gaps,
            [SchedulingRuleKeys.HeavySubjectsEarly] = heavy,
            [SchedulingRuleKeys.AvoidSameSubjectRepeated] = repeats,
            [SchedulingRuleKeys.KeepDoubleLessonsTogether] = doubles,
        };
        var rules = SchedulingRuleKeys.Defaults.Select(rule =>
        {
            var configured = input.Profile.Rules.FirstOrDefault(item => item.Key == rule.Key);
            var enabled = configured?.Enabled ?? rule.Enabled;
            var weight = configured?.Weight ?? rule.Weight;
            var penalty = penalties[rule.Key];
            return new RuleScore(rule.Key, enabled, weight, penalty, enabled ? weight * penalty : 0);
        }).ToArray();
        return new TimetableScore(rules.Sum(rule => rule.Weighted), rules);
    }

    /// <summary>Most disjoint pairs (n, n+1) on one day with no break row between them (in any daily session, R3).</summary>
    private static int Pairs(ShiftInput? shift, IEnumerable<int> lessons)
    {
        var sorted = lessons.Distinct().Order().ToArray();
        var pairs = 0;
        for (var index = 0; index + 1 < sorted.Length; index++)
        {
            var lesson = sorted[index];
            var adjacent = shift?.Adjacent(lesson) ?? true;
            if (sorted[index + 1] != lesson + 1 || !adjacent)
                continue;
            pairs++;
            index++;
        }
        return pairs;
    }
}
