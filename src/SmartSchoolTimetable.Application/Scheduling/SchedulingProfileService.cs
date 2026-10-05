using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Scheduling;

namespace SmartSchoolTimetable.Application.Scheduling;

public sealed record SchedulingRuleDto(string Key, bool Enabled, int Weight, bool EnabledByDefault, int DefaultWeight);

/// <param name="ProfileVersion">Grows on every change; a generated timetable records it (Phase 4).</param>
/// <param name="IsDefault">The rules equal the defaults («استعادة الإعدادات الافتراضية» has nothing to do).</param>
public sealed record SchedulingProfileDto(IReadOnlyList<SchedulingRuleDto> Rules, int ProfileVersion, bool IsDefault, int Version);

public sealed record SchedulingRuleInput(string? Key, bool Enabled, int Weight);

public sealed record SaveSchedulingProfileCommand(IReadOnlyList<SchedulingRuleInput>? Rules, int Version);

/// <param name="Confirm">The owner confirmed in the dialog; without it nothing changes.</param>
public sealed record RestoreSchedulingDefaultsCommand(bool Confirm, int Version);

/// <summary>«ملف الجدولة» (Phase 3 §2.4): the single profile of soft-rule switches and weights.</summary>
public sealed class SchedulingProfileService(IDataStore store, TimeProvider clock)
{
    public async Task<SchedulingProfileDto> GetAsync(CancellationToken token) => ToDto(await LoadAsync(token));

    public async Task<OperationResult<SchedulingProfileDto>> UpdateAsync(SaveSchedulingProfileCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var profile = await LoadAsync(token);
        if (!profile.IsVersion(command.Version))
            return OperationResult.Failure<SchedulingProfileDto>(ErrorCodes.Conflict);
        var rules = (command.Rules ?? []).Select(rule => new SchedulingRule(rule?.Key ?? string.Empty, rule?.Enabled ?? false, rule?.Weight ?? -1)).ToArray();
        var changed = false;
        if (StoreSaving.TryDomain<SchedulingProfileDto>(() => changed = profile.Update(rules)) is { } invalid)
            return invalid;
        if (changed)
            AuditTrail.Record(store, clock, "SchedulingProfileUpdated", "scheduling-profile", $"Profile version {profile.ProfileVersion}.");
        return await store.SaveAsync(() => ToDto(profile), SchedulingProfile.RulesField, token);
    }

    public async Task<OperationResult<SchedulingProfileDto>> RestoreDefaultsAsync(RestoreSchedulingDefaultsCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Confirm)
            return OperationResult.Invalid<SchedulingProfileDto>(nameof(command.Confirm), ErrorCodes.Required);
        var profile = await LoadAsync(token);
        if (!profile.IsVersion(command.Version))
            return OperationResult.Failure<SchedulingProfileDto>(ErrorCodes.Conflict);
        if (profile.RestoreDefaults())
            AuditTrail.Record(store, clock, "SchedulingProfileDefaultsRestored", "scheduling-profile", $"Profile version {profile.ProfileVersion}.");
        return await store.SaveAsync(() => ToDto(profile), SchedulingProfile.RulesField, token);
    }

    /// <summary>The profile row is created at start-up; a missing row (an old test database) is created on first use.</summary>
    private async Task<SchedulingProfile> LoadAsync(CancellationToken token)
    {
        if (await store.FirstOrDefaultAsync(store.Query<SchedulingProfile>(), token) is { } profile)
            return profile;
        profile = SchedulingProfile.CreateDefault();
        store.Add(profile);
        await store.SaveChangesAsync(token);
        return profile;
    }

    private static SchedulingProfileDto ToDto(SchedulingProfile profile)
    {
        // Display order is the defaults' order, whatever order the rows were stored in.
        var ordered = SchedulingRuleKeys.Defaults
            .Select(fallback => (Fallback: fallback, Rule: profile.Rules.FirstOrDefault(rule => rule.Key == fallback.Key) ?? fallback))
            .ToArray();
        var rules = ordered.Select(pair => new SchedulingRuleDto(pair.Rule.Key, pair.Rule.Enabled, pair.Rule.Weight, pair.Fallback.Enabled, pair.Fallback.Weight)).ToArray();
        return new SchedulingProfileDto(rules, profile.ProfileVersion, ordered.All(pair => pair.Rule == pair.Fallback), profile.Version);
    }
}
