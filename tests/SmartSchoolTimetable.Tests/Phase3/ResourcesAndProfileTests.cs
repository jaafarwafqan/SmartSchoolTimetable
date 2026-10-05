using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Resources;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Application.Teachers;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Resources;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Infrastructure;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase3;

/// <summary>
/// Phase 3B: resources (kind, capacity, protection through the subjects that require them), the subject's required
/// resource, teacher specializations and the scheduling profile with its version and «استعادة الإعدادات الافتراضية».
/// </summary>
public sealed class ResourcesAndProfileTests
{
    private static readonly int[] Weights = [20, 30, 15, 25, 10];
    private const string ResourcesPath = "/api/v1/resources/";
    private const string ProfilePath = "/api/v1/scheduling-profile/";

    private static ScheduleGrid Grid => ScheduleGrid.Uniform([7, 1, 2, 3, 4], 6);

    [Fact]
    public void ResourceValidatesNameKindAndCapacity()
    {
        var lab = Resource.Create(" مختبر  الحاسوب ", ResourceKind.Lab, 2, null);
        Assert.Equal(("مختبر الحاسوب", 2, 1), (lab.Name, lab.Capacity, lab.Version));
        var errors = Assert.Throws<DomainValidationException>(() => Resource.Create("", (ResourceKind)9, 0, null)).Errors;
        Assert.Equal([("Name", DomainErrorCode.Required), ("Kind", DomainErrorCode.InvalidOption), ("Capacity", DomainErrorCode.OutOfRange)], errors.Select(error => (error.Field, error.Code)));
        Assert.Throws<DomainValidationException>(() => lab.Update("مختبر", ResourceKind.Lab, Resource.MaxCapacity + 1, null));
        lab.Update("مختبر", ResourceKind.Hall, 20, "قاعة كبيرة");
        Assert.Equal((ResourceKind.Hall, 20, 2), (lab.Kind, lab.Capacity, lab.Version));
    }

    [Fact]
    public void ProfileStartsWithTheOwnersDefaultsAndVersionsEveryChange()
    {
        var profile = SchedulingProfile.CreateDefault();
        Assert.Equal(Weights, profile.Rules.Select(rule => rule.Weight));
        Assert.All(profile.Rules, rule => Assert.True(rule.Enabled));
        Assert.False(profile.RestoreDefaults()); // already default: nothing changes

        var changed = profile.Rules.Select(rule => rule.Key == SchedulingRuleKeys.AvoidTeacherGaps ? rule with { Weight = 80, Enabled = false } : rule).ToArray();
        Assert.True(profile.Update(changed));
        Assert.Equal((2, 2), (profile.ProfileVersion, profile.Version));
        Assert.False(profile.Update(changed.Reverse().ToArray())); // same rules in another order: no new version
        Assert.Equal(2, profile.ProfileVersion);

        Assert.Equal(DomainErrorCode.Required, Assert.Throws<DomainValidationException>(() => profile.Update(changed.Skip(1).ToArray())).Errors.Single().Code);
        Assert.Contains(Assert.Throws<DomainValidationException>(() => profile.Update([.. changed, new SchedulingRule("unknown", true, 5)])).Errors, error => error.Code == DomainErrorCode.InvalidOption);
        Assert.Contains(Assert.Throws<DomainValidationException>(() => profile.Update([.. changed, changed[0]])).Errors, error => error.Code == DomainErrorCode.Duplicate);
        Assert.Equal(DomainErrorCode.OutOfRange, Assert.Throws<DomainValidationException>(() => profile.Update(changed.Select(rule => rule with { Weight = 101 }).ToArray())).Errors.Single().Code);

        Assert.True(profile.RestoreDefaults());
        Assert.Equal((3, 3), (profile.ProfileVersion, profile.Version));
        Assert.Equal(Weights, profile.Rules.Select(rule => rule.Weight));
    }

    [Fact]
    public void SpecializationsAreKeptWhenNotSentAndAddedOnce()
    {
        var details = new TeacherDetails("أحمد علي", "أحمد", null, null, false, null, null, null, null, null, null, [3, 1]);
        var teacher = Teacher.Create(details, Grid);
        Assert.Equal([1L, 3L], teacher.Specializations.Select(item => item.SubjectId));
        teacher.Update(details with { SpecializationIds = null, Notes = "ملاحظة" }, Grid);
        Assert.Equal([1L, 3L], teacher.Specializations.Select(item => item.SubjectId).Order());
        var version = teacher.Version;
        Assert.True(teacher.AddSpecialization(2));
        Assert.False(teacher.AddSpecialization(2));
        Assert.Equal(version + 1, teacher.Version);
        Assert.Throws<DomainValidationException>(() => teacher.Update(details with { SpecializationIds = Enumerable.Range(1, 31).Select(id => (long)id).ToArray() }, Grid));
    }

    [Fact]
    public async Task ResourcesApiValidatesPagesAndProtectsRequiredResources()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host);
        var field = await ReadAsync<ResourceDto>(await host.PostAsync(ResourcesPath, new SaveResourceCommand("الساحة الرياضية", "field", null, null, 0), school.Token));
        Assert.Equal(("field", 1), (field.Kind, field.Capacity));
        await ReadAsync<ResourceDto>(await host.PostAsync(ResourcesPath, new SaveResourceCommand("مختبر الحاسوب", "lab", 2, null, 0), school.Token));
        await ReadAsync<ResourceDto>(await host.PostAsync(ResourcesPath, new SaveResourceCommand("قاعة المسرح", "hall", 1, null, 0), school.Token));
        Assert.Equal(ErrorCodes.DuplicateName, (await AssertApiErrorAsync(await host.PostAsync(ResourcesPath, new SaveResourceCommand(" الساحة  الرياضية ", "field", 1, null, 0), school.Token), ErrorCodes.ValidationFailed)).Errors.Single().Code);
        Assert.Equal("Kind", (await AssertApiErrorAsync(await host.PostAsync(ResourcesPath, new SaveResourceCommand("ملعب", "pool", 1, null, 0), school.Token), ErrorCodes.ValidationFailed)).Errors.Single().Field);
        Assert.Equal("Capacity", (await AssertApiErrorAsync(await host.PostAsync(ResourcesPath, new SaveResourceCommand("ملعب", "field", 0, null, 0), school.Token), ErrorCodes.ValidationFailed)).Errors.Single().Field);

        var page = await ReadAsync<PagedResult<ResourceDto>>(await host.Client.GetAsync($"{ResourcesPath}?pageSize=2&sort=-capacity"));
        Assert.Equal((3, 2, "مختبر الحاسوب"), (page.Total, page.Items.Count, page.Items[0].Name));
        Assert.Equal("الساحة الرياضية", (await ReadAsync<PagedResult<ResourceDto>>(await host.Client.GetAsync($"{ResourcesPath}?search=الساحة"))).Items.Single().Name);
        await AssertApiErrorAsync(await host.PutAsync($"{ResourcesPath}{field.Id}", new SaveResourceCommand("الساحة", "field", 1, null, field.Version + 1), school.Token), ErrorCodes.Conflict);

        // The subject requires the field: archive and delete are refused and the report names the subject.
        var subjectPath = $"/api/v1/subjects/{school.Subject.Id}";
        var subject = await ReadAsync<SubjectDto>(await host.PutAsync(subjectPath, SubjectBody(school.Subject, field.Id), school.Token));
        Assert.Equal(field.Id, subject.RequiredResourceId);
        var report = await ReferenceProtectionTests.ReferencesAsync(host, ReferenceKinds.Resource, field.Id);
        Assert.Equal((DependentKinds.Subject, "الرياضيات", ErrorCodes.ResourceInUse), (report.Dependents.Single().Kind, report.Dependents.Single().Samples.Single(), report.DeleteBlockedBy));
        await AssertApiErrorAsync(await host.PostAsync($"{ResourcesPath}{field.Id}/archive", new { version = field.Version }, school.Token), ErrorCodes.ResourceInUse);
        await AssertApiErrorAsync(await host.SendJsonAsync(HttpMethod.Delete, $"{ResourcesPath}{field.Id}?version={field.Version}", new { }, school.Token), ErrorCodes.ResourceInUse);

        // An archived resource cannot be newly chosen; clearing the requirement frees the resource.
        var hall = (await ReadAsync<PagedResult<ResourceDto>>(await host.Client.GetAsync($"{ResourcesPath}?search=المسرح"))).Items.Single();
        await ReadAsync<ResourceDto>(await host.PostAsync($"{ResourcesPath}{hall.Id}/archive", new { version = hall.Version }, school.Token));
        var rejected = await AssertApiErrorAsync(await host.PutAsync(subjectPath, SubjectBody(subject, hall.Id), school.Token), ErrorCodes.ValidationFailed);
        Assert.Equal(("RequiredResourceId", ErrorCodes.InvalidOption), (rejected.Errors.Single().Field, rejected.Errors.Single().Code));
        subject = await ReadAsync<SubjectDto>(await host.PutAsync(subjectPath, SubjectBody(subject, null), school.Token));
        Assert.Null(subject.RequiredResourceId);
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendJsonAsync(HttpMethod.Delete, $"{ResourcesPath}{field.Id}?version={field.Version}", new { }, school.Token)).StatusCode);

        using var anonymous = host.CreateClientWithoutCookies();
        await AssertApiErrorAsync(await anonymous.GetAsync(ResourcesPath), ErrorCodes.Unauthenticated);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PostAsync(ResourcesPath, new SaveResourceCommand("قاعة", "hall", 1, null, 0), "wrong-token")).StatusCode);
    }

    [Fact]
    public async Task TeacherSpecializationsAreSavedAddedAndRemovedWithTheSubject()
    {
        await using var host = new TestHost();
        var school = await ReferenceProtectionTests.SeedAsync(host);
        var physics = await ReadAsync<SubjectDto>(await host.PostAsync("/api/v1/subjects/", SubjectBody("الفيزياء"), school.Token));
        var teacher = await ReadAsync<TeacherDto>(await host.PostAsync("/api/v1/teachers/", TeacherCommand(0, [school.Subject.Id]), school.Token));
        Assert.Equal([school.Subject.Id], teacher.SpecializationIds!);

        // An update without the list keeps it; an unknown subject is refused.
        teacher = await ReadAsync<TeacherDto>(await host.PutAsync($"/api/v1/teachers/{teacher.Id}", TeacherCommand(teacher.Version, null), school.Token));
        Assert.Equal([school.Subject.Id], teacher.SpecializationIds!);
        Assert.Equal("SpecializationIds", (await AssertApiErrorAsync(await host.PutAsync($"/api/v1/teachers/{teacher.Id}", TeacherCommand(teacher.Version, [999]), school.Token), ErrorCodes.ValidationFailed)).Errors.Single().Field);

        // «إضافة المادة لتخصصاته»: once; a second add changes nothing.
        teacher = await ReadAsync<TeacherDto>(await host.PostAsync($"/api/v1/teachers/{teacher.Id}/specializations/{physics.Id}", new AddSpecializationCommand(teacher.Version), school.Token));
        Assert.Equal([school.Subject.Id, physics.Id], teacher.SpecializationIds!);
        var again = await ReadAsync<TeacherDto>(await host.PostAsync($"/api/v1/teachers/{teacher.Id}/specializations/{physics.Id}", new AddSpecializationCommand(teacher.Version), school.Token));
        Assert.Equal(teacher.Version, again.Version);

        // Deleting the subject removes it from the specializations (DECISIONS_PENDING #53).
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendJsonAsync(HttpMethod.Delete, $"/api/v1/subjects/{physics.Id}?version={physics.Version}", new { }, school.Token)).StatusCode);
        var listed = (await ReadAsync<PagedResult<TeacherDto>>(await host.Client.GetAsync("/api/v1/teachers/"))).Items.Single();
        Assert.Equal([school.Subject.Id], listed.SpecializationIds!);
        Assert.Equal(teacher.Version, listed.Version);
    }

    [Fact]
    public async Task SchedulingProfileApiVersionsChangesAndRestoresDefaults()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var profile = await ReadAsync<SchedulingProfileDto>(await host.Client.GetAsync(ProfilePath));
        Assert.Equal((1, true), (profile.ProfileVersion, profile.IsDefault));
        Assert.Equal(Weights, profile.Rules.Select(rule => rule.Weight));

        var rules = profile.Rules.Select(rule => new SchedulingRuleInput(rule.Key, rule.Key != SchedulingRuleKeys.HeavySubjectsEarly, rule.Key == SchedulingRuleKeys.AvoidTeacherGaps ? 60 : rule.Weight)).ToArray();
        var saved = await ReadAsync<SchedulingProfileDto>(await host.PutAsync(ProfilePath, new SaveSchedulingProfileCommand(rules, profile.Version), token));
        Assert.Equal((2, false, 60), (saved.ProfileVersion, saved.IsDefault, saved.Rules.Single(rule => rule.Key == SchedulingRuleKeys.AvoidTeacherGaps).Weight));
        Assert.Equal(30, saved.Rules.Single(rule => rule.Key == SchedulingRuleKeys.AvoidTeacherGaps).DefaultWeight);
        await AssertApiErrorAsync(await host.PutAsync(ProfilePath, new SaveSchedulingProfileCommand(rules, profile.Version), token), ErrorCodes.Conflict);
        var invalid = await AssertApiErrorAsync(await host.PutAsync(ProfilePath, new SaveSchedulingProfileCommand(rules.Select(rule => rule with { Weight = 150 }).ToArray(), saved.Version), token), ErrorCodes.ValidationFailed);
        Assert.Equal(("Rules", ErrorCodes.ValueOutOfRange), (invalid.Errors.Single().Field, invalid.Errors.Single().Code));

        var unconfirmed = await AssertApiErrorAsync(await host.PostAsync($"{ProfilePath}restore-defaults", new RestoreSchedulingDefaultsCommand(false, saved.Version), token), ErrorCodes.ValidationFailed);
        Assert.Equal("Confirm", unconfirmed.Errors.Single().Field);
        var restored = await ReadAsync<SchedulingProfileDto>(await host.PostAsync($"{ProfilePath}restore-defaults", new RestoreSchedulingDefaultsCommand(true, saved.Version), token));
        Assert.Equal((3, true), (restored.ProfileVersion, restored.IsDefault));
        Assert.Equal(Weights, restored.Rules.Select(rule => rule.Weight));
    }

    [Fact]
    public async Task TheMigrationUpgradesAnExistingDatabase()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"smart-school-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite($"Data Source={Path.Combine(directory, "old.db")};Pooling=False").Options;
            await using (var old = new LocalDbContext(options))
            {
                var previous = old.Database.GetMigrations().Last(name => name.EndsWith("_Phase25SuggestedCurriculum", StringComparison.Ordinal));
                await old.GetService<IMigrator>().MigrateAsync(previous);
                // Rows written by the previous version, before resources and specializations existed.
                await old.Database.ExecuteSqlRawAsync("INSERT INTO Subjects (Name, NormalizedName, ColorIndex, Priority, DistributionEnabled, SpreadAcrossDays, Heavy, RequiresDoublePeriod, IsArchived, Version) VALUES ('الكيمياء', 'الكيمياء', 1, 3, 1, 0, 0, 0, 0, 1)");
            }
            await using var upgraded = new LocalDbContext(options);
            await upgraded.Database.MigrateAsync();
            Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync());
            var subject = await upgraded.Set<Subject>().AsNoTracking().SingleAsync();
            Assert.Null(subject.RequiredResourceId);
            upgraded.Add(Resource.Create("مختبر العلوم", ResourceKind.Lab, 1, null));
            upgraded.Add(SchedulingProfile.CreateDefault());
            await upgraded.SaveChangesAsync();
            Assert.Equal(5, (await upgraded.Set<SchedulingProfile>().AsNoTracking().SingleAsync()).Rules.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static object SubjectBody(string name) => new
    {
        name, colorIndex = 0, priority = 0, distributionEnabled = true, spreadAcrossDays = false, heavy = false,
        requiresDoublePeriod = false, blockedPeriods = Array.Empty<object>(), notes = (string?)null, version = 0,
    };

    private static SaveSubjectCommand SubjectBody(SubjectDto subject, long? resourceId) => new(
        subject.Name, subject.ColorIndex, subject.Priority, subject.DistributionEnabled, subject.SpreadAcrossDays, subject.Heavy,
        subject.RequiresDoublePeriod, subject.BlockedPeriods, subject.Notes, subject.Version, resourceId);

    private static SaveTeacherCommand TeacherCommand(int version, IReadOnlyList<long>? specializations) => new(
        "أحمد علي حسن", "أحمد", [], [], false, null, null, null, null, null, null, version, specializations);
}
