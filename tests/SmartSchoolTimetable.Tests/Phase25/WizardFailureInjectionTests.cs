using System.Net;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase25;

/// <summary>
/// The wizard's transaction design (ADR 0023) under a real failure: a save that throws in the middle of a step (here
/// a simulated disk error) rolls back everything the step had saved, also inside a service's own nested transaction.
/// </summary>
public sealed class WizardFailureInjectionTests
{
    private static readonly object School = new { name = "متوسطة الرافدين", schoolType = "intermediate", shiftMode = "morning", principalName = (string?)null };

    private static readonly object Year = new
    {
        label = "2026-2027",
        startDate = "2026-09-01",
        endDate = "2027-06-30",
        terms = new[]
        {
            new { name = "الفصل الأول", startDate = "2026-09-01", endDate = "2027-01-15" },
            new { name = "الفصل الثاني", startDate = "2027-02-01", endDate = "2027-06-30" },
        },
    };

    [Theory]
    [InlineData(2)] // inside MakeCurrentAsync's own (joined) transaction
    [InlineData(4)] // while adding the second term
    [InlineData(0)] // 0 = the last save of the step: recording it in the setup progress
    public async Task AFailedSaveRollsBackTheWholeYearStep(int failingSave)
    {
        var failures = new SaveFailureInjection();
        await using var host = new TestHost(failures);
        var (token, _) = await SetupOwnerAsync(host);
        var before = await ReadAsync<SetupProgressDto>(await host.Client.GetAsync("/api/v1/setup-progress/"));

        if (failingSave == 0)
        {
            // Count the saves of a successful run in a separate host, then fail exactly on the last one.
            var counter = new SaveFailureInjection();
            await using var measuring = new TestHost(counter);
            var (measureToken, _) = await SetupOwnerAsync(measuring);
            counter.Arm(int.MaxValue);
            await ReadAsync<SetupProgressDto>(await measuring.PutAsync("/api/v1/setup-wizard/year", Year, measureToken));
            failingSave = counter.Saves;
            Assert.True(failingSave >= 3);
        }
        failures.Arm(failingSave);
        var response = await host.PutAsync("/api/v1/setup-wizard/year", Year, token);
        failures.Arm(0);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Injected", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal); // no internal detail leaks

        Assert.Empty((await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync("/api/v1/academic-years/"))).Items);
        var after = await ReadAsync<SetupProgressDto>(await host.Client.GetAsync("/api/v1/setup-progress/"));
        Assert.Equal((before.CurrentStep, before.Version), (after.CurrentStep, after.Version));

        // The same step succeeds afterwards: nothing half-saved blocks it.
        Assert.Equal(3, (await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-wizard/year", Year, token))).CurrentStep);
        Assert.Equal(2, (await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync("/api/v1/academic-years/"))).Items.Single().Terms.Count);
    }

    [Fact]
    public async Task AFailedProgressSaveUndoesTheSchoolProfileChange()
    {
        var failures = new SaveFailureInjection();
        await using var host = new TestHost(failures);
        var (token, _) = await SetupOwnerAsync(host);
        failures.Arm(2); // the profile saves, then recording the step fails
        Assert.Equal(HttpStatusCode.InternalServerError, (await host.PutAsync("/api/v1/setup-wizard/school", School, token)).StatusCode);
        failures.Arm(0);
        var profile = await ReadAsync<SchoolProfileDto>(await host.Client.GetAsync("/api/v1/school-profile"));
        Assert.Equal(string.Empty, profile.Name);
        Assert.Equal(1, profile.Version);
    }
}
