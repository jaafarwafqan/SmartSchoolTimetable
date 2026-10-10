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

    /// <summary>Defaults in display order: every rule enabled with the owner's weights.</summary>
    public static readonly IReadOnlyList<SchedulingRule> Defaults =
    [
        new(SpreadSubjectsAcrossDays, true, 20),
        new(AvoidTeacherGaps, true, 30),
        new(HeavySubjectsEarly, true, 15),
        new(AvoidSameSubjectRepeated, true, 25),
        new(KeepDoubleLessonsTogether, true, 10),
    ];
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

    private readonly List<SchedulingRule> _rules = [];

    private SchedulingProfile()
    {
    }

    public int ProfileVersion { get; private set; } = 1;
    public IReadOnlyList<SchedulingRule> Rules => _rules;

    public static SchedulingProfile CreateDefault()
    {
        var profile = new SchedulingProfile { Id = SingletonId };
        profile._rules.AddRange(SchedulingRuleKeys.Defaults.Select(rule => rule with { }));
        return profile;
    }

    /// <summary>Replaces the rules; every known rule exactly once, weights 0–100. Returns false when nothing changed.</summary>
    public bool Update(IReadOnlyCollection<SchedulingRule>? rules)
    {
        var input = rules ?? [];
        var keys = SchedulingRuleKeys.Defaults.Select(rule => rule.Key).ToArray();
        new DomainErrors()
            .When(input.Any(rule => rule is null || !keys.Contains(rule.Key)), RulesField, DomainErrorCode.InvalidOption)
            .When(input.Where(rule => rule is not null).GroupBy(rule => rule.Key).Any(group => group.Count() > 1), RulesField, DomainErrorCode.Duplicate)
            .When(keys.Any(key => input.All(rule => rule?.Key != key)), RulesField, DomainErrorCode.Required)
            .When(input.Any(rule => rule is not null && rule.Weight is < MinWeight or > MaxWeight), RulesField, DomainErrorCode.OutOfRange)
            .ThrowIfAny();
        return Replace(keys.Select(key => input.First(rule => rule.Key == key)));
    }

    /// <summary>«استعادة الإعدادات الافتراضية». Returns false when the rules were already the defaults.</summary>
    public bool RestoreDefaults() => Replace(SchedulingRuleKeys.Defaults);

    private bool Replace(IEnumerable<SchedulingRule> ordered)
    {
        // Fresh copies: owned rows must never share an instance (the defaults are static).
        var next = ordered.Select(rule => rule with { }).ToArray();
        if (next.Length == _rules.Count && next.All(_rules.Contains))
            return false;
        _rules.Clear();
        _rules.AddRange(next);
        ProfileVersion++;
        Touch();
        return true;
    }
}
