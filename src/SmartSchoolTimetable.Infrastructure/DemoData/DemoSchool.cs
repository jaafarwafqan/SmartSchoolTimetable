using Microsoft.Extensions.DependencyInjection;
using SmartSchoolTimetable.Application.Calendar;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Application.Teachers;

namespace SmartSchoolTimetable.Infrastructure.DemoData;

/// <summary>The fictional sample school. All names are invented; any resemblance to real people is coincidental.</summary>
internal sealed class DemoSchool(IServiceProvider services, CancellationToken token)
{
    private static readonly string[] StageNames = ["الأول المتوسط", "الثاني المتوسط", "الثالث المتوسط", "الرابع العلمي"];
    private static readonly string[] SectionLabels = ["أ", "ب", "ج"];

    public async Task CreateAsync(bool dualShift)
    {
        var profile = services.GetRequiredService<SchoolProfileService>();
        var current = await profile.GetAsync(token);
        Ensure(await profile.UpdateAsync(new UpdateSchoolProfileCommand("ثانوية النموذج التجريبية", "secondary", dualShift ? "dual" : "morning",
            "أ. سعاد عبد الكريم", "أ. مهند فاضل", "Asia/Baghdad", "arabicIndic", "gregorian", current.Version), token));

        var years = services.GetRequiredService<AcademicYearService>();
        var year = Ensure(await years.CreateAsync(new SaveAcademicYearCommand("2026-2027", "2026-09-21", "2027-06-30", 0), token));
        year = Ensure(await years.AddTermAsync(year.Id, new SaveTermCommand("الفصل الأول", "2026-09-21", "2027-01-20", year.Version), token));
        year = Ensure(await years.AddTermAsync(year.Id, new SaveTermCommand("الفصل الثاني", "2027-02-05", "2027-06-30", year.Version), token));
        Ensure(await years.SetCurrentTermAsync(year.Id, year.Terms[0].Id, year.Version, token));

        var structure = services.GetRequiredService<TimetableStructureService>();
        var shifts = new List<ShiftDto> { await CreateShiftAsync(structure, year.Id, "الدوام الصباحي", 1, "08:00") };
        if (dualShift)
            shifts.Add(await CreateShiftAsync(structure, year.Id, "الدوام المسائي", 2, "13:00"));

        var stages = services.GetRequiredService<StagesSectionsService>();
        for (var index = 0; index < StageNames.Length; index++)
        {
            var stage = Ensure(await stages.CreateStageAsync(year.Id, new SaveStageCommand(StageNames[index], index + 1, 0), token));
            foreach (var label in SectionLabels)
            {
                var shift = shifts[dualShift && index >= 2 ? 1 : 0];
                Ensure(await stages.CreateSectionAsync(year.Id, stage.Id, new SaveSectionCommand(label, shift.Id, 28 + index, 0), token));
            }
        }

        await DemoCatalog.CreateSubjectsAsync(services.GetRequiredService<SubjectsService>(), token);
        await DemoCatalog.CreateTeachersAsync(services.GetRequiredService<TeachersService>(), token);
        await DemoCatalog.CreateCalendarAsync(services.GetRequiredService<CalendarService>(), token);
    }

    private async Task<ShiftDto> CreateShiftAsync(TimetableStructureService structure, long yearId, string name, int order, string start)
    {
        var shift = Ensure(await structure.CreateShiftAsync(yearId, new SaveShiftCommand(name, order, 0), token));
        var plan = Ensure(TimetableStructureService.Generate(new GeneratePeriodsCommand(start, 45, 7, 15, 4)));
        var periods = plan.Periods.Select(period => new PeriodInput(period.Kind, period.StartTime, period.EndTime, period.StartBell, period.EndBell)).ToArray();
        return Ensure(await structure.ReplacePeriodsAsync(yearId, shift.Id, new ReplacePeriodsCommand(periods, shift.Version), token));
    }

    /// <summary>Demo data must satisfy every rule; a failure is a bug in the sample, so it stops the command.</summary>
    internal static T Ensure<T>(OperationResult<T> result) =>
        result.Succeeded
            ? result.Value!
            : throw new InvalidOperationException($"Demo data rejected: {result.ErrorCode} {string.Join(", ", result.FieldErrors.Select(error => $"{error.Field}={error.Code}"))}");
}
