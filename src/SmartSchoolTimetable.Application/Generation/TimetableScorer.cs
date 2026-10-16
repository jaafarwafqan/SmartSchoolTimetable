using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Generation;

/// <summary>
/// The last-period fairness of a timetable in the owner's terms: the teacher with the most lessons in the last period of the day and the one with the
/// fewest (among teachers with a normal load), with their raw counts (a lesson on an evening day counts once here).
/// </summary>
public sealed record LastPeriodInsight(string MostTeacher, int MostCount, string LeastTeacher, int LeastCount);

/// <summary>Total penalty and its breakdown per soft rule (lower is better); <paramref name="LastPeriods"/> is set when there are last periods to share.</summary>
public sealed record TimetableScore(long Total, IReadOnlyList<RuleScore> Rules, LastPeriodInsight? LastPeriods = null, IReadOnlyList<ConsecutiveRun>? ConsecutiveRuns = null);

/// <summary>A teacher who got more lessons in a row than his limit on one day: his name, the day, the longest run that day and the limit.</summary>
public sealed record ConsecutiveRun(string Teacher, int Day, int Count, int Limit);

/// <summary>
/// Scores a timetable on the soft rules S1–S5 with the profile weights (docs/SOLVER.md §3). The solver minimises the
/// same sum, so for a solver timetable <see cref="TimetableScore.Total"/> equals its objective (tested). The
/// definitions, per rule:
/// S1 spread: per (section, subject flagged spread, day), lessons above 1 (above 2 for a double subject).
/// S2 teacher gaps: per (teacher, day, shift), free lesson numbers between the first and the last lesson.
/// S3 heavy early: per lesson of a heavy subject, its lesson number − 1.
/// S4 repeats: per section and day, consecutive lesson numbers with the same subject (double subjects excluded).
/// S5 doubles (standard mode only): per double line, ⌊lessons ÷ 2⌋ minus the adjacent pairs it got.
/// S6 teacher spread: per (teacher, day, shift), lesson numbers n and n + 1 both taught by him (a pair of double lessons excepted).
/// S7 balanced days: per teacher, the busiest minus the quietest of his full days minus 1 (not below 0), plus the lessons of a partly-available day above its share.
/// S9 consecutive lessons (a priority): per (teacher, shift, day), windows of limit + 1 consecutive lesson numbers all his.
/// S8 fair last periods: per teacher, his weighted last-period lessons (evening days count double) above his share by weekly load.
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

        // S6: adjacent lessons of one teacher.
        var teacherSpread = items.GroupBy(item => (item.Lesson.TeacherId, item.Lesson.Day, item.Section.ShiftId))
            .Sum(group =>
            {
                var byLesson = group.GroupBy(item => item.Lesson.Lesson).ToDictionary(slot => slot.Key, slot => slot.ToArray());
                long count = 0;
                foreach (var (lesson, here) in byLesson)
                {
                    if (!byLesson.TryGetValue(lesson + 1, out var next))
                        continue;
                    count += here.Any(first => next.Any(second => !(LineDouble(first.Line) && LineDouble(second.Line)))) ? 1 : 0;
                }
                return count;
            });

        // S7: balanced teacher days.
        var teacherDays = TeacherDays.Of(input);
        long balance = 0;
        foreach (var teacherGroup in items.GroupBy(item => item.Lesson.TeacherId))
        {
            if (!teacherDays.TryGetValue(teacherGroup.Key, out var info))
                continue;
            var perDay = teacherGroup.GroupBy(item => item.Lesson.Day).ToDictionary(day => day.Key, day => day.Count());
            if (info.FullDays.Count >= 2)
            {
                var counts = info.FullDays.Select(day => perDay.GetValueOrDefault(day)).ToArray();
                balance += Math.Max(0, counts.Max() - counts.Min() - 1);
            }
            foreach (var day in info.Days)
            {
                if (info.PartialAllowance(day.Day) is { } allowance)
                    balance += Math.Max(0, perDay.GetValueOrDefault(day.Day) - allowance);
            }
        }

        // S8: fair last periods.
        var lastLessons = LastPeriodFairness.LastLessons(input);
        var allowances = LastPeriodFairness.Allowances(input);
        var lastWeighted = items.Where(item => lastLessons.GetValueOrDefault((item.Section.ShiftId, item.Lesson.Day)) == item.Lesson.Lesson)
            .GroupBy(item => item.Lesson.TeacherId)
            .ToDictionary(group => group.Key, group => (Weighted: group.Sum(item => (long)LastPeriodFairness.WeightOf(input, item.Lesson.Day)), Raw: group.Count()));
        var lastFair = lastWeighted.Sum(pair => Math.Max(0, pair.Value.Weighted - allowances.GetValueOrDefault(pair.Key)));

        // S9: lessons in a row above the teacher's limit (a priority for him; a hard teacher has none in a valid timetable).
        var teachersById = input.Teachers.ToDictionary(teacher => teacher.Id);
        long overRuns = 0;
        var runs = new List<ConsecutiveRun>();
        foreach (var group in items.GroupBy(item => (item.Lesson.TeacherId, item.Section.ShiftId, item.Lesson.Day)))
        {
            if (!teachersById.TryGetValue(group.Key.TeacherId, out var teacher) || input.MaxConsecutiveOf(teacher) is not { } limit)
                continue;
            var numbers = group.Select(item => item.Lesson.Lesson).ToHashSet();
            overRuns += numbers.Count(number => Enumerable.Range(number, limit + 1).All(numbers.Contains));
            var longest = numbers.Max(number => numbers.Contains(number - 1) ? 0 : Enumerable.Range(number, numbers.Count).TakeWhile(numbers.Contains).Count());
            if (longest > limit)
                runs.Add(new ConsecutiveRun(teacher.Name, group.Key.Day, longest, limit));
        }
        var dayOrder = input.WorkingDays.Select((day, index) => (day, index)).ToDictionary(pair => pair.day, pair => pair.index);
        var runList = runs.GroupBy(run => (run.Teacher, run.Day)).Select(day => day.MaxBy(run => run.Count)!)
            .OrderBy(run => run.Teacher, StringComparer.Ordinal).ThenBy(run => dayOrder.GetValueOrDefault(run.Day)).ToArray();

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
            [SchedulingRuleKeys.SpreadTeacherLessons] = teacherSpread,
            [SchedulingRuleKeys.BalanceTeacherDays] = balance,
            [SchedulingRuleKeys.FairLastPeriods] = lastFair,
            [SchedulingRuleKeys.MaxConsecutiveLessons] = overRuns,
        };
        var rules = SchedulingRuleKeys.Defaults.Select(rule =>
        {
            var configured = input.Profile.Rules.FirstOrDefault(item => item.Key == rule.Key);
            var enabled = configured?.Enabled ?? rule.Enabled;
            var weight = configured?.Weight ?? rule.Weight;
            var penalty = penalties[rule.Key];
            return new RuleScore(rule.Key, enabled, weight, penalty, enabled ? weight * penalty : 0);
        }).ToArray();
        return new TimetableScore(rules.Sum(rule => rule.Weighted), rules, Insight(input, teacherDays, lastWeighted.ToDictionary(pair => pair.Key, pair => pair.Value.Raw), allowances),
            runList.Length == 0 ? null : runList);
    }

    /// <summary>
    /// The most and the fewest last-period lessons among teachers whose weekly load is at least half the average (a teacher with two lessons a week
    /// would always be "the fewest"); null when no last period is taught at all.
    /// </summary>
    private static LastPeriodInsight? Insight(SchedulingInput input, IReadOnlyDictionary<long, TeacherDays> days, Dictionary<long, int> raw, IReadOnlyDictionary<long, int> allowances)
    {
        if (raw.Count == 0 || allowances.Count == 0)
            return null;
        var average = days.Values.Average(item => item.Load);
        var normal = days.Values.Where(item => item.Load * 2 >= average).ToArray();
        if (normal.Length < 2)
            return null;
        string Name(TeacherDays item) => item.Teacher.Name;
        var most = normal.OrderByDescending(item => raw.GetValueOrDefault(item.Teacher.Id)).ThenBy(item => item.Teacher.Id).First();
        var least = normal.OrderBy(item => raw.GetValueOrDefault(item.Teacher.Id)).ThenByDescending(item => item.Load).ThenBy(item => item.Teacher.Id).First();
        return new LastPeriodInsight(Name(most), raw.GetValueOrDefault(most.Teacher.Id), Name(least), raw.GetValueOrDefault(least.Teacher.Id));
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
