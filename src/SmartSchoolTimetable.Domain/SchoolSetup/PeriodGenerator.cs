using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>Input of the "Generate periods" helper (spec 2.5).</summary>
/// <param name="BreakAfterLesson">Optional: a break of <paramref name="BreakMinutes"/> follows this lesson.</param>
public sealed record PeriodPlan(TimeOnly FirstStartTime, int LessonMinutes, int LessonCount, int BreakMinutes, int? BreakAfterLesson);

/// <summary>
/// Builds an editable list of consecutive lessons with an optional break. Nothing is saved: the owner reviews
/// and edits the rows, then saves them through <see cref="Shift.ReplacePeriods"/>.
/// </summary>
public static class PeriodGenerator
{
    public const int MinLessonMinutes = 10;
    public const int MaxLessonMinutes = 120;
    public const int MinBreakMinutes = 5;
    public const int MaxBreakMinutes = 120;

    public static IReadOnlyList<PeriodDraft> Generate(PeriodPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var hasBreak = plan.BreakAfterLesson is not null;
        new DomainErrors()
            .When(plan.LessonCount is < 1 or > Shift.MaxLessons, nameof(plan.LessonCount), DomainErrorCode.OutOfRange)
            .When(plan.LessonMinutes is < MinLessonMinutes or > MaxLessonMinutes, nameof(plan.LessonMinutes), DomainErrorCode.OutOfRange)
            .When(hasBreak && plan.BreakMinutes is < MinBreakMinutes or > MaxBreakMinutes, nameof(plan.BreakMinutes), DomainErrorCode.OutOfRange)
            .When(hasBreak && (plan.BreakAfterLesson < 1 || plan.BreakAfterLesson >= plan.LessonCount), nameof(plan.BreakAfterLesson), DomainErrorCode.OutOfRange)
            .ThrowIfAny();

        var totalMinutes = plan.LessonCount * plan.LessonMinutes + (hasBreak ? plan.BreakMinutes : 0);
        var dayEnd = plan.FirstStartTime.ToTimeSpan() + TimeSpan.FromMinutes(totalMinutes);
        if (dayEnd > TimeSpan.FromHours(24) - TimeSpan.FromMinutes(1))
            throw new DomainValidationException(nameof(plan.LessonCount), DomainErrorCode.InvalidTimeRange);

        var rows = new List<PeriodDraft>();
        var start = plan.FirstStartTime;
        for (var lesson = 1; lesson <= plan.LessonCount; lesson++)
        {
            var end = start.AddMinutes(plan.LessonMinutes);
            rows.Add(new PeriodDraft(PeriodKind.Lesson, start, end));
            start = end;
            if (lesson == plan.BreakAfterLesson)
            {
                end = start.AddMinutes(plan.BreakMinutes);
                rows.Add(new PeriodDraft(PeriodKind.Break, start, end, StartBell: false, EndBell: false));
                start = end;
            }
        }
        return rows;
    }
}
