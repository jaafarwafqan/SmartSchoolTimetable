namespace SmartSchoolTimetable.Application.Scheduling;

/// <summary>
/// H9, the most lessons of one subject a section may have on one day. Automatic: max(2, ceil(weekly ÷ working days)). The owner can
/// override it per subject («أولويات الجدول › متقدم»). The solver, the verifier and the pre-solve validator all use this one rule.
/// </summary>
public static class DailyCaps
{
    public static int For(SubjectInput? subject, int weekly, int days) =>
        subject?.DailyCap is { } owner and > 0 ? owner : Math.Max(2, days == 0 ? weekly : (weekly + days - 1) / days);
}
