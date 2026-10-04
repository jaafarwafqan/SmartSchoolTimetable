using SmartSchoolTimetable.Application.Calendar;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Application.Teachers;

namespace SmartSchoolTimetable.Infrastructure.DemoData;

/// <summary>Subjects, teachers and calendar days of the sample school (fictional names; 7 lessons, Sunday–Thursday).</summary>
internal static class DemoCatalog
{
    private sealed record SubjectSeed(string Name, int Priority, bool Spread, bool Heavy, bool Double, bool Distribution, BlockedPeriodDto[] Blocked, string? Notes = null);

    private static readonly SubjectSeed[] Subjects =
    [
        new("الرياضيات", 5, true, true, false, true, []),
        new("اللغة العربية", 5, true, true, false, true, []),
        new("اللغة الإنكليزية", 4, true, false, false, true, []),
        new("الفيزياء", 4, true, true, true, true, [new(4, 7)], "حصة مزدوجة للمختبر."),
        new("الكيمياء", 4, true, false, true, true, [new(4, 6), new(4, 7)]),
        new("الأحياء", 3, true, false, false, true, []),
        new("التربية الإسلامية", 3, true, false, false, true, []),
        new("الاجتماعيات", 2, false, false, false, true, []),
        new("الحاسوب", 2, false, false, true, true, [new(7, 1)]),
        new("التربية الرياضية", 1, false, false, false, false, [new(7, 1), new(1, 1), new(2, 1), new(3, 1), new(4, 1)], "لا توضع في الحصة الأولى."),
    ];

    private sealed record TeacherSeed(string FullName, string ShortName, int[] OffDays, BlockedPeriodDto[] Blocked, int? PerDay, int? PerWeek, bool Released = false);

    private static readonly TeacherSeed[] Teachers =
    [
        new("علي حسين كاظم", "علي حسين", [], [], 5, 24),
        new("زينب جاسم محمد", "زينب جاسم", [4], [], 5, 20),
        new("حيدر عبد الأمير صالح", "حيدر عبد الأمير", [], [new(7, 1), new(7, 2)], 6, 24),
        new("نور الهدى فاضل", "نور الهدى", [], [], null, 22),
        new("مصطفى كريم عباس", "مصطفى كريم", [7], [], 5, 18),
        new("فاطمة رحيم جواد", "فاطمة رحيم", [], [new(3, 6), new(3, 7)], 4, 20),
        new("أحمد سلمان داود", "أحمد سلمان", [], [], 6, 26),
        new("هدى ناصر علوان", "هدى ناصر", [1], [], 5, 20),
        new("محمد جواد كاظم", "محمد جواد", [], [new(2, 1)], null, null),
        new("سارة عادل حسن", "سارة عادل", [], [], 5, 22),
        new("كرار فلاح مهدي", "كرار فلاح", [2], [new(4, 7)], 5, 20),
        new("رقية ستار جبار", "رقية ستار", [], [], 6, 24),
        new("عباس حميد ياسين", "عباس حميد", [], [], 4, 16),
        new("آمنة كاظم عبيد", "آمنة كاظم", [3], [], 5, 18),
        new("حسن علي مطر", "حسن علي", [], [new(7, 7), new(1, 7)], 5, 22),
        new("مريم وليد سعيد", "مريم وليد", [], [], null, 20),
        new("يوسف رعد حمزة", "يوسف رعد", [], [], 6, 28),
        new("إيمان صادق حسون", "إيمان صادق", [4], [], 4, 15),
        new("جعفر ماجد عزيز", "جعفر ماجد", [], [], null, null, Released: true),
        new("بتول عدنان شاكر", "بتول عدنان", [], [], null, null, Released: true),
    ];

    private static readonly (string Title, string Start, string? End, string Kind, bool Affects)[] CalendarDays =
    [
        ("عطلة نصف السنة", "2027-01-21", "2027-02-04", "schoolHoliday", true),
        ("امتحانات نصف السنة", "2027-01-10", "2027-01-20", "exam", true),
        ("عطلة رسمية", "2026-10-03", null, "officialHoliday", true),
        ("رأس السنة الميلادية", "2027-01-01", null, "officialHoliday", true),
        ("يوم المعلم", "2027-03-01", null, "specialDay", false),
        ("عيد نوروز", "2027-03-21", null, "officialHoliday", true),
        ("الامتحانات النهائية", "2027-05-25", "2027-06-10", "exam", true),
        ("حفل التخرج", "2027-06-20", null, "specialDay", false),
    ];

    public static async Task CreateSubjectsAsync(SubjectsService service, CancellationToken token)
    {
        for (var index = 0; index < Subjects.Length; index++)
        {
            var seed = Subjects[index];
            DemoSchool.Ensure(await service.CreateAsync(new SaveSubjectCommand(seed.Name, index + 1, seed.Priority, seed.Distribution,
                seed.Spread, seed.Heavy, seed.Double, seed.Blocked, seed.Notes, 0), token));
        }
    }

    public static async Task CreateTeachersAsync(TeachersService service, CancellationToken token)
    {
        foreach (var seed in Teachers)
        {
            DemoSchool.Ensure(await service.CreateAsync(new SaveTeacherCommand(seed.FullName, seed.ShortName, seed.OffDays, seed.Blocked,
                seed.Released, seed.Released ? "تفرغ إداري" : null, seed.Released ? "2026-09-21" : null, seed.Released ? "2027-01-20" : null,
                seed.PerDay, seed.PerWeek, null, 0), token));
        }
    }

    public static async Task CreateCalendarAsync(CalendarService service, CancellationToken token)
    {
        foreach (var (title, start, end, kind, affects) in CalendarDays)
            DemoSchool.Ensure(await service.CreateAsync(new SaveCalendarDayCommand(title, start, end, kind, affects, 0), token));
    }
}
