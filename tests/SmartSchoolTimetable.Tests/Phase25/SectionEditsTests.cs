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
    public async Task OneSectionChangesShiftAndAMiddleSectionIsArchived()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        await host.PutAsync("/api/v1/setup-wizard/school", new { name = "ثانوية دجلة", schoolType = "secondary", shiftMode = "dual" }, token);
        await host.PutAsync("/api/v1/setup-wizard/year", new { label = "2026-2027", startDate = "2026-09-01", endDate = "2027-06-30", terms = Array.Empty<object>() }, token);
        var shiftsInput = new[] { ("morning", "08:00", 7), ("evening", "13:00", 6) }
            .Select(item => new { kind = item.Item1, firstStartTime = item.Item2, lessonMinutes = 40, lessonCount = item.Item3, breaks = Array.Empty<object>(), dayLessons = Array.Empty<object>() });
        await ReadAsync<SetupProgressDto>(await host.PutAsync("/api/v1/setup-wizard/timing", new { days = SundayToThursday, weekStartDay = 7, shifts = shiftsInput }, token));
        var year = (await ReadAsync<PagedResult<AcademicYearDto>>(await host.Client.GetAsync("/api/v1/academic-years/"))).Items.Single();
        var root = $"/api/v1/academic-years/{year.Id}";
        var shifts = (await ReadAsync<PagedResult<ShiftDto>>(await host.Client.GetAsync($"{root}/shifts/?pageSize=100"))).Items;
        var morning = shifts.Single(shift => shift.Kind == "morning");
        var evening = shifts.Single(shift => shift.Kind == "evening");
        await host.PostAsync($"{root}/templates/stages", new { schoolType = "secondary", grades = new[] { new { gradeKey = "intermediate-1", sections = 3, shiftId = morning.Id } } }, token);
        var card = (await ReadAsync<List<StageCardDto>>(await host.Client.GetAsync($"{root}/stage-cards"))).Single();
        Assert.Equal(["أ", "ب", "ج"], card.Sections.Select(section => section.Label));

        // Move only ب to the evening shift: the stage's totals now come per shift, with each shift's capacity.
        var middle = card.Sections[1];
        var sectionsPath = $"{root}/stages/{card.Stage.Id}/sections";
        var moved = await ReadAsync<SectionDto>(await host.PutAsync($"{sectionsPath}/{middle.Id}", new { label = middle.Label, shiftId = evening.Id, studentCount = (int?)null, version = middle.Version }, token));
        Assert.Equal((evening.Id, 30), (moved.ShiftId, moved.WeeklyCapacity));
        var totals = (await ReadAsync<CurriculumTableDto>(await host.Client.GetAsync($"{root}/curriculum"))).Stages.Single().Totals;
        Assert.Equal([("الدوام الصباحي", 2, 35), ("الدوام المسائي", 1, 30)], totals.Select(total => (total.ShiftName, total.Sections, total.WeeklyCapacity)));

        // Archive the middle section: the cards hide it, the stepper skips its label and removes only the last active one.
        await ReadAsync<SectionDto>(await host.PostAsync($"{sectionsPath}/{middle.Id}/archive", new { version = moved.Version }, token));
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
