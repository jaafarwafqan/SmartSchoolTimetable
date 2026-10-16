using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.Scheduling;

/// <summary>One soft rule of the profile: whether it is used and how much it weighs (0–100).</summary>
public sealed record SchedulingRule(string Key, bool Enabled, int Weight);

/// <summary>The soft rules the solver will weigh (Phase 3 §2.4) and their default weights.</summary>
public static class SchedulingRuleKeys
{
    public const string SpreadSubjectsAcrossDays = "spreadSubjectsAcrossDays";
    public const string AvoidTeacherGaps = "avoidTeacherGaps";
    public const string HeavySubjectsEarly = "heavySubjectsEarly";
    public const string AvoidSameSubjectRepeated = "avoidSameSubjectRepeated";
    public const string KeepDoubleLessonsTogether = "keepDoubleLessonsTogether";

    /// <summary>«توزيع حصص المعلم بفواصل»: lessons of a teacher on one day are not back to back (the default of the teacher's day).</summary>
    public const string SpreadTeacherLessons = "spreadTeacherLessons";

    /// <summary>«توزيع أيام المعلم بالتساوي»: a teacher's lessons are balanced over the days he is free.</summary>
    public const string BalanceTeacherDays = "balanceTeacherDays";

    /// <summary>«عدالة الحصص الأخيرة»: no teacher always gets the last periods of the day.</summary>
    public const string FairLastPeriods = "fairLastPeriods";

    /// <summary>Defaults in display order. <see cref="AvoidTeacherGaps"/> («متتالية») is off by default: it is the opposite of <see cref="SpreadTeacherLessons"/>.</summary>
    public static readonly IReadOnlyList<SchedulingRule> Defaults =
    [
        new(SpreadSubjectsAcrossDays, true, 20),
        new(SpreadTeacherLessons, true, 30),
        new(AvoidTeacherGaps, false, 30),
        new(BalanceTeacherDays, true, 40),
        new(FairLastPeriods, true, 20),
        new(HeavySubjectsEarly, true, 15),
        new(AvoidSameSubjectRepeated, true, 25),
        new(KeepDoubleLessonsTogether, true, 10),
    ];

    /// <summary>True when <paramref name="rules"/> turn on both the spread and the consecutive form of the teacher's day (they are mutually exclusive).</summary>
    public static bool Conflicting(IEnumerable<SchedulingRule> rules) =>
        rules.Any(rule => rule.Key == SpreadTeacherLessons && rule.Enabled && rule.Weight > 0)
        && rules.Any(rule => rule.Key == AvoidTeacherGaps && rule.Enabled && rule.Weight > 0);
}

/// <summary>
/// The single active scheduling profile (exactly one row, Id = 1; DECISIONS_PENDING #47). Hard constraints are never part of it; it only
/// weighs soft rules. <see cref="ProfileVersion"/> grows on every change so a generated timetable can record which
/// profile state produced it; <see cref="VersionedEntity.Version"/> stays the concurrency token.
/// </summary>
public sealed class SchedulingProfile : VersionedEntity
{
    public const int MinWeight = 0;
    public const int MaxWeight = 100;
    public const string RulesField = "Rules";
    public const long SingletonId = 1;
    public const int MinConsecutive = 2;
    public const int MaxConsecutive = 3;
    public const int DefaultConsecutive = 3;
    public const string ConsecutiveField = "DefaultMaxConsecutive";

    private readonly List<SchedulingRule> _rules = [];

    private SchedulingProfile()
    {
    }

    public int ProfileVersion { get; private set; } = 1;

    /// <summary>The school's «أقصى حصص متتالية للمعلم» (2 or 3), used by every teacher without his own.</summary>
    public int DefaultMaxConsecutive { get; private set; } = DefaultConsecutive;

    /// <summary>«جعل التوازن إلزامياً»: balanced teacher days become a hard rule instead of a priority.</summary>
    public bool BalanceHard { get; private set; }
    public IReadOnlyList<SchedulingRule> Rules => _rules;

    public static SchedulingProfile CreateDefault()
    {
        var profile = new SchedulingProfile { Id = SingletonId };
        profile._rules.AddRange(SchedulingRuleKeys.Defaults.Select(rule => rule with { }));
        return profile;
    }

    /// <summary>
    /// Replaces the rules; every known rule exactly once, weights 0–100, and not both forms of the teacher's day. A null
    /// <paramref name="defaultMaxConsecutive"/> or <paramref name="balanceHard"/> keeps the stored value. Returns false when nothing changed.
    /// </summary>
    public bool Update(IReadOnlyCollection<SchedulingRule>? rules, int? defaultMaxConsecutive = null, bool? balanceHard = null)
    {
        var input = rules ?? [];
        var keys = SchedulingRuleKeys.Defaults.Select(rule => rule.Key).ToArray();
        new DomainErrors()
            .When(input.Any(rule => rule is null || !keys.Contains(rule.Key)), RulesField, DomainErrorCode.InvalidOption)
            .When(input.Where(rule => rule is not null).GroupBy(rule => rule.Key).Any(group => group.Count() > 1), RulesField, DomainErrorCode.Duplicate)
            .When(keys.Any(key => input.All(rule => rule?.Key != key)), RulesField, DomainErrorCode.Required)
            .When(input.Any(rule => rule is not null && rule.Weight is < MinWeight or > MaxWeight), RulesField, DomainErrorCode.OutOfRange)
            .When(defaultMaxConsecutive is not null and not (MinConsecutive or MaxConsecutive), ConsecutiveField, DomainErrorCode.OutOfRange)
            .ThrowIfAny();
        var ordered = keys.Select(key => input.First(rule => rule.Key == key)).ToArray();
        new DomainErrors().When(SchedulingRuleKeys.Conflicting(ordered), RulesField, DomainErrorCode.InvalidOption).ThrowIfAny();
        return Replace(ordered, defaultMaxConsecutive ?? DefaultMaxConsecutive, balanceHard ?? BalanceHard);
    }

    /// <summary>«استعادة الإعدادات الافتراضية». Returns false when the rules were already the defaults.</summary>
    public bool RestoreDefaults() => Replace(SchedulingRuleKeys.Defaults, DefaultConsecutive, false);

    private bool Replace(IEnumerable<SchedulingRule> ordered, int defaultMaxConsecutive, bool balanceHard)
    {
        // Fresh copies: owned rows must never share an instance (the defaults are static).
        var next = ordered.Select(rule => rule with { }).ToArray();
        if (next.Length == _rules.Count && next.All(_rules.Contains) && defaultMaxConsecutive == DefaultMaxConsecutive && balanceHard == BalanceHard)
            return false;
        _rules.Clear();
        _rules.AddRange(next);
        DefaultMaxConsecutive = defaultMaxConsecutive;
        BalanceHard = balanceHard;
        ProfileVersion++;
        Touch();
        return true;
    }
}
