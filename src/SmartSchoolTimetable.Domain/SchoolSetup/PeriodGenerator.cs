using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>A break of <paramref name="Minutes"/> that follows lesson <paramref name="AfterLesson"/>.</summary>
public sealed record BreakSlot(int AfterLesson, int Minutes);

/// <summary>
/// Input of the "Generate periods" helper (spec 2.5 §4.1; break model ADR 0026): any number of breaks (at most one
/// per gap between lessons, R2), each with its own position and duration (1–120 minutes), and an optional short gap
/// between consecutive lessons (0 by default).
/// </summary>
public sealed record PeriodPlan(TimeOnly FirstStartTime, int LessonMinutes, int LessonCount, IReadOnlyList<BreakSlot> Breaks)
{
    /// <summary>Minutes between two lessons that are not separated by a break (0 = back to back).</summary>
    public int GapMinutes { get; init; }

    /// <summary>The Phase 2 form: at most one break.</summary>
    public PeriodPlan(TimeOnly firstStartTime, int lessonMinutes, int lessonCount, int breakMinutes, int? breakAfterLesson)
        : this(firstStartTime, lessonMinutes, lessonCount, breakAfterLesson is { } after ? [new BreakSlot(after, breakMinutes)] : [])
    {
    }
}

/// <summary>
/// Builds an editable list of consecutive lessons with breaks (for example one break after the third lesson, or a
/// short break after every two lessons). Nothing is saved: the owner reviews and edits the rows, then saves them
/// through <see cref="Shift.ReplacePeriods"/>.
/// </summary>
public static class PeriodGenerator
{
    public const int MinLessonMinutes = 10;
    public const int MaxLessonMinutes = 120;
    public const int MinBreakMinutes = 1;
    public const int MaxBreakMinutes = 120;
    public const int MaxGapMinutes = 30;

    public static IReadOnlyList<PeriodDraft> Generate(PeriodPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var breaks = plan.Breaks ?? [];
        new DomainErrors()
            .When(plan.LessonCount is < 1 or > Shift.MaxLessons, nameof(plan.LessonCount), DomainErrorCode.OutOfRange)
            .When(plan.LessonMinutes is < MinLessonMinutes or > MaxLessonMinutes, nameof(plan.LessonMinutes), DomainErrorCode.OutOfRange)
            .When(breaks.Any(slot => slot.Minutes is < MinBreakMinutes or > MaxBreakMinutes), "BreakMinutes", DomainErrorCode.OutOfRange)
            .When(breaks.Any(slot => slot.AfterLesson < 1 || slot.AfterLesson >= plan.LessonCount), "BreakAfterLesson", DomainErrorCode.OutOfRange)
            .When(breaks.GroupBy(slot => slot.AfterLesson).Any(group => group.Count() > 1), "BreakAfterLesson", DomainErrorCode.Duplicate)
            .When(plan.GapMinutes is < 0 or > MaxGapMinutes, nameof(plan.GapMinutes), DomainErrorCode.OutOfRange)
            .ThrowIfAny();

        var gaps = Enumerable.Range(1, plan.LessonCount - 1).Count(lesson => breaks.All(slot => slot.AfterLesson != lesson));
        var totalMinutes = plan.LessonCount * plan.LessonMinutes + breaks.Sum(slot => slot.Minutes) + gaps * plan.GapMinutes;
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
            if (breaks.FirstOrDefault(slot => slot.AfterLesson == lesson) is { } pause)
            {
                end = start.AddMinutes(pause.Minutes);
                rows.Add(new PeriodDraft(PeriodKind.Break, start, end, StartBell: false, EndBell: false));
                start = end;
            }
            else if (lesson < plan.LessonCount)
            {
                start = start.AddMinutes(plan.GapMinutes);
            }
        }
        return rows;
    }
}
