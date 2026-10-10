using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Curriculum;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Stages;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase25;

/// <summary>
/// One section moved to the other shift, and a middle section archived, on a dual-shift school: totals split per
/// shift, the stepper skips the archived label and removes only the last active section.
/// </summary>
public sealed class SectionEditsTests
{
    private static readonly int[] SundayToThursday = [7, 1, 2, 3, 4];

    [Fact]
    public async Task AMiddleSectionIsArchivedAndTheStepperSkipsItsLabel()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        await host.PutAsync("/api/v1/setup-wizard/school", new { name = "ثانوية دجلة", schoolType = "secondary" }, token);
        await host.PutAsync("/api/v1/setup-wizard/year", new { label = "2026-2027", startDate = "2026-09-01", endDate = "2027-06-30", terms = Array.Empty<object>() }, token);
        var main = new { kind = "morning", firstStartTime = "08:00", lessonMinutes = 40, lessonCount = 7, breaks = Array.Empty<object>(), dayLessons = Array.Empty<object>() };
        await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-wizard/timing", new { days = SundayToThursday, weekStartDay = 7, system = "morning", main }, token));
        var year = (await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync("/api/v1/academic-years/"))).Items.Single();
        var root = $"/api/v1/academic-years/{year.Id}";
        var morning = (await ReadAsync<PagedResult<ShiftDto>>(await host.Client.GetAsync($"{root}/shifts/?pageSize=100"))).Items.Single();
        await host.PostAsync($"{root}/templates/stages", new { schoolType = "secondary", grades = new[] { new { gradeKey = "intermediate-1", sections = 3, shiftId = morning.Id } } }, token);
        var card = (await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards"))).Single();
        Assert.Equal(["أ", "ب", "ج"], card.Sections.Select(section => section.Label));
        var totals = (await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum"))).Stages.Single().Totals;
        Assert.Equal([(3, 35)], totals.Select(total => (total.Sections, total.WeeklyCapacity)));

        var middle = card.Sections[1];
        var sectionsPath = $"{root}/stages/{card.Stage.Id}/sections";
        // Archive the middle section: the cards hide it, the stepper skips its label and removes only the last active one.
        await ReadAsync<SectionDto>(await host.PostAsync($"{sectionsPath}/{middle.Id}/archive", new { version = middle.Version }, token));
        card = (await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards"))).Single();
        Assert.Equal(["أ", "ج"], card.Sections.Select(section => section.Label));
        card = await ReadAsync<StageCardDto>(await host.PutAsync($"{root}/stage-cards/{card.Stage.Id}/section-count", new { count = 3, shiftId = morning.Id }, token));
        Assert.Equal(["أ", "ج", "د"], card.Sections.Select(section => section.Label));
        card = await ReadAsync<StageCardDto>(await host.PutAsync($"{root}/stage-cards/{card.Stage.Id}/section-count", new { count = 2, shiftId = morning.Id }, token));
        Assert.Equal(["أ", "ج"], card.Sections.Select(section => section.Label));
        var all = (await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards?includeArchived=true"))).Single();
        Assert.Contains(all.Sections, section => section is { Label: "ب", IsArchived: true }); // kept for history
    }
}
