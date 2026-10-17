using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Application.Teachers;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Infrastructure;
using SmartSchoolTimetable.Tests.Phase3;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>The generation rules through the API and the corrective migration of an existing school's priorities (Phase 5, ADR 0053).</summary>
public sealed class GenerationRulesApiTests
{
    private const string ProfilePath = "/api/v1/scheduling-profile/";

    [Fact]
    public async Task ASubjectLimitForAGradeIsSetClearedAndValidated()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host);
        var path = $"/api/v1/subjects/{school.Subject.Id}/cap-override";
        var set = await ReadAsync<SubjectDto>(await host.PutAsync(path, new SetCapOverrideCommand("stage", school.Stage.Id, 1, school.Subject.Version), school.Token));
        Assert.Equal([new CapOverrideDto("stage", school.Stage.Id, 1)], set.CapOverrides!);
        await AssertApiErrorAsync(await host.PutAsync(path, new SetCapOverrideCommand("stage", school.Stage.Id, 2, school.Subject.Version), school.Token), ErrorCodes.Conflict);
        var invalid = await AssertApiErrorAsync(await host.PutAsync(path, new SetCapOverrideCommand("grade", school.Stage.Id, 2, set.Version), school.Token), ErrorCodes.ValidationFailed);
        Assert.Equal("Scope", invalid.Errors.Single().Field);
        var unknown = await AssertApiErrorAsync(await host.PutAsync(path, new SetCapOverrideCommand("section", 999_999, 2, set.Version), school.Token), ErrorCodes.ValidationFailed);
        Assert.Equal("ScopeId", unknown.Errors.Single().Field);
        await AssertApiErrorAsync(await host.PutAsync(path, new SetCapOverrideCommand("stage", school.Stage.Id, 7, set.Version), school.Token), ErrorCodes.ValidationFailed);
        var cleared = await ReadAsync<SubjectDto>(await host.PutAsync(path, new SetCapOverrideCommand("stage", school.Stage.Id, null, set.Version), school.Token));
        Assert.Empty(cleared.CapOverrides!);
        // The subject's own limit accepts one lesson a day now.
        var one = await ReadAsync<SubjectDto>(await host.PutAsync($"/api/v1/subjects/{school.Subject.Id}/daily-cap", new SetDailyCapCommand(1, cleared.Version), school.Token));
        Assert.Equal(1, one.DailyCap);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PutAsync(path, new SetCapOverrideCommand("stage", school.Stage.Id, 1, one.Version), "wrong-token")).StatusCode);
    }

    [Fact]
    public async Task ATeacherKeepsHisOwnConsecutiveLimitAndTheSchoolDefaultIsTwoOrThree()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host);
        SaveTeacherCommand Command(int version, int? limit) => new("أحمد علي حسن", "أحمد", [], [], false, null, null, null, null, null, null, version, null, limit);
        var teacher = await ReadAsync<TeacherDto>(await host.PostAsync("/api/v1/teachers/", Command(0, 2), school.Token));
        Assert.Equal(2, teacher.MaxConsecutive);
        var error = await AssertApiErrorAsync(await host.PutAsync($"/api/v1/teachers/{teacher.Id}", Command(teacher.Version, 4), school.Token), ErrorCodes.ValidationFailed);
        Assert.Equal("MaxConsecutive", error.Errors.Single().Field);
        teacher = await ReadAsync<TeacherDto>(await host.PutAsync($"/api/v1/teachers/{teacher.Id}", Command(teacher.Version, null), school.Token));
        Assert.Null(teacher.MaxConsecutive);

        var profile = await ReadAsync<SchedulingProfileDto>(await host.Client.GetAsync(ProfilePath));
        Assert.Equal((3, false), (profile.DefaultMaxConsecutive, profile.BalanceHard));
        var rules = profile.Rules.Select(rule => new SchedulingRuleInput(rule.Key, rule.Enabled, rule.Weight)).ToArray();
        var saved = await ReadAsync<SchedulingProfileDto>(await host.PutAsync(ProfilePath, new SaveSchedulingProfileCommand(rules, profile.Version, 2, true), school.Token));
        Assert.Equal((2, true, false), (saved.DefaultMaxConsecutive, saved.BalanceHard, saved.IsDefault));
        Assert.Equal(profile.ProfileVersion + 1, saved.ProfileVersion);
        var bad = await AssertApiErrorAsync(await host.PutAsync(ProfilePath, new SaveSchedulingProfileCommand(rules, saved.Version, 5, null), school.Token), ErrorCodes.ValidationFailed);
        Assert.Equal(SchedulingProfile.ConsecutiveField, bad.Errors.Single().Field);
        // Both forms of the teacher's day at once are refused.
        var both = rules.Select(rule => rule.Key == SchedulingRuleKeys.AvoidTeacherGaps ? rule with { Enabled = true, Weight = 30 } : rule).ToArray();
        Assert.Equal(ErrorCodes.InvalidOption, (await AssertApiErrorAsync(await host.PutAsync(ProfilePath, new SaveSchedulingProfileCommand(both, saved.Version), school.Token), ErrorCodes.ValidationFailed)).Errors.Single().Code);
        // The school's choice reaches the scheduling input.
        var yearId = long.Parse(school.Root.Split('/')[^1], System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync($"/api/v1/academic-years/{yearId}/readiness/")).StatusCode);
    }

    [Theory]
    [InlineData(true, 30, false, true)] // the old default: «متتالية» off, «بفواصل» on
    [InlineData(true, 60, true, false)] // the school's own weight for «متتالية»: kept, «بفواصل» off
    [InlineData(false, 0, false, true)] // already off: «بفواصل» on
    public async Task TheMigrationGivesAnExistingSchoolTheNewPrioritiesWithoutBothFormsOfTheTeachersDay(bool gapsOn, int gapsWeight, bool gapsAfter, bool spreadAfter)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"smart-school-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite($"Data Source={Path.Combine(directory, "old.db")};Pooling=False").Options;
            await using (var old = new LocalDbContext(options))
            {
                var previous = old.Database.GetMigrations().Last(name => name.EndsWith("_Phase5SubjectDailyCap", StringComparison.Ordinal));
                await old.GetService<IMigrator>().MigrateAsync(previous);
                await old.Database.ExecuteSqlRawAsync("INSERT INTO SchedulingProfile (Id, ProfileVersion, Version) VALUES (1, 4, 4)");
                foreach (var (key, enabled, weight) in new[] { ("spreadSubjectsAcrossDays", true, 20), ("avoidTeacherGaps", gapsOn, gapsWeight), ("heavySubjectsEarly", true, 15), ("avoidSameSubjectRepeated", true, 25), ("keepDoubleLessonsTogether", true, 10) })
                    await old.Database.ExecuteSqlAsync($"INSERT INTO SchedulingProfileRules (ProfileId, Key, Enabled, Weight) VALUES (1, {key}, {enabled}, {weight})");
            }
            await using var upgraded = new LocalDbContext(options);
            await upgraded.Database.MigrateAsync();
            var profile = await upgraded.Set<SchedulingProfile>().AsNoTracking().SingleAsync();
            Assert.Equal(9, profile.Rules.Count);
            Assert.Equal(8, profile.ProfileVersion); // one step per corrective migration
            Assert.Equal(new SchedulingRule(SchedulingRuleKeys.SpreadSubjectsAcrossDays, true, 80), profile.Rules.Single(rule => rule.Key == SchedulingRuleKeys.SpreadSubjectsAcrossDays));
            Assert.Equal(new SchedulingRule(SchedulingRuleKeys.MaxConsecutiveLessons, true, 40), profile.Rules.Single(rule => rule.Key == SchedulingRuleKeys.MaxConsecutiveLessons));
            Assert.False(profile.ConsecutiveHard);
            Assert.Equal((3, false), (profile.DefaultMaxConsecutive, profile.BalanceHard));
            Assert.Equal(gapsAfter, profile.Rules.Single(rule => rule.Key == SchedulingRuleKeys.AvoidTeacherGaps).Enabled);
            Assert.Equal(spreadAfter, profile.Rules.Single(rule => rule.Key == SchedulingRuleKeys.SpreadTeacherLessons).Enabled);
            Assert.False(SchedulingRuleKeys.Conflicting(profile.Rules));
            Assert.Equal(new SchedulingRule(SchedulingRuleKeys.BalanceTeacherDays, true, 40), profile.Rules.Single(rule => rule.Key == SchedulingRuleKeys.BalanceTeacherDays));
            Assert.Equal(new SchedulingRule(SchedulingRuleKeys.FairLastPeriods, true, 20), profile.Rules.Single(rule => rule.Key == SchedulingRuleKeys.FairLastPeriods));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
