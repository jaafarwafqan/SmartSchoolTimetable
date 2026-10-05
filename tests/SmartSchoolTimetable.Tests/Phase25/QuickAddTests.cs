using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Application.Teachers;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Tests.Phase2;

namespace SmartSchoolTimetable.Tests.Phase25;

/// <summary>Quick add (spec 2.5 §2.5): one typed name, everything else defaulted by the server.</summary>
public sealed class QuickAddTests
{
    private static async Task<FakeDataStore> StoreAsync()
    {
        var store = new FakeDataStore();
        store.Add(WorkingWeek.CreateDefault());
        await store.SaveChangesAsync(default);
        return store;
    }

    private static SaveSubjectCommand NameOnly(string name) => new(name, 0, 0, true, false, false, false, null, null, 0);

    [Fact]
    public async Task SubjectsGetTheNextFreePaletteColourThenCycle()
    {
        var service = new SubjectsService(await StoreAsync(), TimeProvider.System);
        var first = (await service.CreateAsync(NameOnly("الرياضيات"), default)).Value!;
        Assert.Equal((1, 3), (first.ColorIndex, first.Priority));
        var chosen = (await service.CreateAsync(new SaveSubjectCommand("العلوم", 3, 5, true, false, false, false, null, null, 0), default)).Value!;
        Assert.Equal(3, chosen.ColorIndex);
        Assert.Equal(2, (await service.CreateAsync(NameOnly("الفيزياء"), default)).Value!.ColorIndex); // 2 is the first free one
        Assert.Equal(4, (await service.CreateAsync(NameOnly("الكيمياء"), default)).Value!.ColorIndex);
        for (var index = 5; index <= 10; index++)
            Assert.Equal(index, (await service.CreateAsync(NameOnly($"مادة {index}"), default)).Value!.ColorIndex);
        Assert.Equal(1, (await service.CreateAsync(NameOnly("مادة 11"), default)).Value!.ColorIndex); // 10 used: cycle
    }

    private static SaveTeacherCommand FullNameOnly(string fullName) => new(fullName, null, null, null, false, null, null, null, null, null, null, 0);

    [Fact]
    public async Task TeachersGetAProposedShortNameWhenNoneIsTyped()
    {
        var service = new TeachersService(await StoreAsync(), TimeProvider.System);
        Assert.Equal("علي حسين", (await service.CreateAsync(FullNameOnly("علي حسين كاظم"), default)).Value!.ShortName);
        Assert.Equal("علي حسين جاسم", (await service.CreateAsync(FullNameOnly("علي حسين جاسم"), default)).Value!.ShortName);
        Assert.Equal("سارة", (await service.CreateAsync(new SaveTeacherCommand("سارة محمود", "سارة", null, null, false, null, null, null, null, null, null, 0), default)).Value!.ShortName);
        var noFreeName = await service.CreateAsync(FullNameOnly("علي حسين"), default);
        Assert.Contains(noFreeName.FieldErrors, error => error is { Field: "ShortName", Code: ErrorCodes.Required });
    }
}
