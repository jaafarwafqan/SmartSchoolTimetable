namespace SmartSchoolTimetable.Application.Scheduling;

/// <summary>
/// H9, the most lessons of one subject a section may have on one day. Resolution, most specific first: the owner's limit for the
/// section, for the section's grade, for the subject («أولويات الجدول › متقدم»), then automatic: max(2, ceil(weekly ÷ working days)).
/// The solver, the verifier and the pre-solve validator all use this one rule.
/// </summary>
public sealed class DailyCaps
{
    private readonly Dictionary<(long Subject, long Stage), int> _byStage;
    private readonly Dictionary<(long Subject, long Section), int> _bySection;

    public DailyCaps(SchedulingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var overrides = input.CapOverrides ?? [];
        _byStage = overrides.Where(item => item.StageId is not null).GroupBy(item => (item.SubjectId, item.StageId!.Value)).ToDictionary(group => group.Key, group => group.First().Cap);
        _bySection = overrides.Where(item => item.SectionId is not null).GroupBy(item => (item.SubjectId, item.SectionId!.Value)).ToDictionary(group => group.Key, group => group.First().Cap);
    }

    /// <summary>The limit of <paramref name="subject"/> in <paramref name="section"/> for <paramref name="weekly"/> lessons over <paramref name="days"/> days.</summary>
    public int For(SubjectInput? subject, SectionInput section, int weekly, int days)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (subject is not null)
        {
            if (_bySection.TryGetValue((subject.Id, section.Id), out var bySection))
                return bySection;
            if (_byStage.TryGetValue((subject.Id, section.StageId), out var byStage))
                return byStage;
        }
        return For(subject, weekly, days);
    }

    /// <summary>The limit without grade or section overrides: the subject's own, else automatic.</summary>
    public static int For(SubjectInput? subject, int weekly, int days) =>
        subject?.DailyCap is { } owner and > 0 ? owner : Math.Max(2, days == 0 ? weekly : (weekly + days - 1) / days);
}
