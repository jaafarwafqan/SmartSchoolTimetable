using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Generation;

/// <summary>
/// The last-period fairness of a timetable in the owner's terms: the teacher with the most lessons in the last period of the day and the one with the
/// fewest (among teachers with a normal load), with their raw counts (a lesson on an evening day counts once here).
/// </summary>
/// <remarks>
/// <c>FullWeek</c> is true when every section uses every period of its week (for example 30 lessons in 30 periods): then each section has a last lesson
/// every day, so <c>LastLessons</c> last lessons a week have to go to someone.
/// </remarks>
public sealed record LastPeriodInsight(string MostTeacher, int MostCount, string LeastTeacher, int LeastCount, bool FullWeek = false, int LastLessons = 0);

/// <summary>Total penalty and its breakdown per soft rule (lower is better); <paramref name="LastPeriods"/> is set when there are last periods to share.</summary>
public sealed record TimetableScore(long Total, IReadOnlyList<RuleScore> Rules, LastPeriodInsight? LastPeriods = null, IReadOnlyList<ConsecutiveRun>? ConsecutiveRuns = null,
    RepeatInsight? Repeats = null);

/// <summary>
/// Same-day repeats of a subject in a section (a lesson above the first of the day; above the first two for a double subject), split into those no
/// timetable can avoid and those a better timetable could remove. <paramref name="Forced"/> names who and why for the unavoidable ones.
/// </summary>
public sealed record RepeatInsight(int Total, int Unavoidable, int Improvable, IReadOnlyList<ForcedRepeat> Forced);

/// <param name="Reason">A <see cref="SpreadReasons"/> code: the teacher is missing on some days, or the subject has more weekly lessons than days.</param>
/// <param name="Weekly">The subject's weekly lessons in one section.</param>
/// <param name="Days">Days on which the teacher can teach it there.</param>
/// <param name="Count">Unavoidable repeats in all these sections together.</param>
/// <param name="Sections">How many sections they are in.</param>
public sealed record ForcedRepeat(string Teacher, string Subject, string Reason, int Weekly, int Days, int Count, int Sections);

/// <summary>A teacher who got more lessons in a row than his limit on one day: his name, the day, the longest run that day and the limit.</summary>
public sealed record ConsecutiveRun(string Teacher, int Day, int Count, int Limit);

/// <summary>
/// Scores a timetable on the soft rules S1–S5 with the profile weights (docs/SOLVER.md §3). The solver minimises the
/// same sum, so for a solver timetable <see cref="TimetableScore.Total"/> equals its objective (tested). The
/// definitions, per rule:
/// S1 spread: per (section, subject that spreads, day), lessons above 1 (above 2 for a double subject) plus lessons above its fair share (<see cref="SpreadShare"/>).
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

        var shares = SubjectSpread.Of(input);
        var spreadDays = items.GroupBy(item => (item.Section.Id, item.Subject.Id, item.Lesson.Day))
            .Where(group => shares.ContainsKey((group.Key.Item1, group.Key.Item2)))
            .Select(group => (Share: shares[(group.Key.Item1, group.Key.Item2)], Count: group.Count()))
            .ToArray();
        var spread = spreadDays.Sum(day => (long)day.Share.Penalty(day.Count));
        var repeatsInsight = Repeats(input, spreadDays);

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
            runList.Length == 0 ? null : runList, repeatsInsight);
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
        var lines = input.Lines.ToDictionary(line => line.Id);
        var weekly = input.Assignments.Where(row => lines.ContainsKey(row.LineId)).GroupBy(row => row.SectionId)
            .ToDictionary(group => group.Key, group => group.Sum(row => lines[row.LineId].WeeklyLessons));
        var fullWeek = input.Sections.Count > 0 && input.Sections.All(section =>
            weekly.GetValueOrDefault(section.Id) == section.AllowedByDay.Where(day => input.WorkingDays.Contains(day.Day)).Sum(day => day.Lessons));
        var lastLessons = input.Sections.Sum(section => section.AllowedByDay.Count(day => input.WorkingDays.Contains(day.Day) && day.Lessons > 0));
        return new LastPeriodInsight(Name(most), raw.GetValueOrDefault(most.Teacher.Id), Name(least), raw.GetValueOrDefault(least.Teacher.Id), fullWeek, lastLessons);
    }

    /// <summary>
    /// Counts the same-day repeats and splits them: per (section, subject), the repeats up to <see cref="SpreadShare.Unavoidable"/> cannot be avoided,
    /// the rest could. The unavoidable ones are listed per (teacher, subject, reason). Null when the timetable has no repeat.
    /// </summary>
    private static RepeatInsight? Repeats(SchedulingInput input, IReadOnlyList<(SpreadShare Share, int Count)> days)
    {
        var perPair = days.GroupBy(day => day.Share).Select(group => (Share: group.Key, Repeats: group.Sum(day => Math.Max(0, day.Count - day.Share.Base))))
            .Where(pair => pair.Repeats > 0).ToArray();
        var total = perPair.Sum(pair => pair.Repeats);
        if (total == 0)
            return null;
        var teachers = input.Teachers.ToDictionary(teacher => teacher.Id, teacher => teacher.Name);
        var subjects = input.Subjects.ToDictionary(subject => subject.Id, subject => subject.Name);
        var forced = perPair.Select(pair => (pair.Share, Count: Math.Min(pair.Repeats, pair.Share.Unavoidable))).Where(pair => pair.Count > 0).ToArray();
        var list = forced
            .GroupBy(pair => (Teacher: string.Join("، ", pair.Share.TeacherIds.Select(id => teachers.GetValueOrDefault(id, string.Empty))),
                Subject: subjects.GetValueOrDefault(pair.Share.SubjectId, string.Empty), pair.Share.Reason, pair.Share.Weekly, pair.Share.Days))
            .Select(group => new ForcedRepeat(group.Key.Teacher, group.Key.Subject, group.Key.Reason, group.Key.Weekly, group.Key.Days, group.Sum(pair => pair.Count), group.Count()))
            .OrderByDescending(item => item.Count).ThenBy(item => item.Subject, StringComparer.Ordinal).ThenBy(item => item.Teacher, StringComparer.Ordinal)
            .ToArray();
        var unavoidable = forced.Sum(pair => pair.Count);
        return new RepeatInsight(total, unavoidable, total - unavoidable, list);
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
