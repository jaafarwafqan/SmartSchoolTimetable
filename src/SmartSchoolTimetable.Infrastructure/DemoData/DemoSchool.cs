using Microsoft.Extensions.DependencyInjection;
using SmartSchoolTimetable.Application.Calendar;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Curriculum;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Setup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Application.Teachers;

namespace SmartSchoolTimetable.Infrastructure.DemoData;

/// <summary>
/// The fictional sample school: an Iraqi secondary school (intermediate grades + the fourth preparatory grade in
/// both branches), morning-only or dual-shift. Everything is created through the same services and templates as the
/// wizard. The weekly curriculum numbers are SAMPLES for the demo, marked as such on every line; they are not
/// official. All names are invented; any resemblance to real people is coincidental.
/// </summary>
internal sealed class DemoSchool(IServiceProvider services, CancellationToken token)
{
    private static readonly int[] SundayToThursday = [7, 1, 2, 3, 4];

    /// <summary>Sample weekly lessons for the first grade (30 = its capacity of 6 lessons × 5 days).</summary>
    private static readonly (string Subject, string? Label, int Lessons)[] FirstGradeSample =
    [
        ("الرياضيات", null, 5), ("اللغة العربية", null, 4), ("اللغة العربية", "أدب", 2), ("اللغة الإنكليزية", null, 4),
        ("الأحياء", null, 3), ("الكيمياء", null, 2), ("الفيزياء", null, 2), ("التربية الإسلامية", null, 3),
        ("الاجتماعيات", null, 3), ("الحاسوب", null, 1), ("التربية الرياضية", null, 1),
    ];

    public async Task CreateAsync(bool dualShift)
    {
        var profile = services.GetRequiredService<SchoolProfileService>();
        var current = await profile.GetAsync(token);
        var saved = Ensure(await profile.UpdateAsync(new UpdateSchoolProfileCommand("ثانوية الرافدين التجريبية", "secondary", dualShift ? "dual" : "morning",
            "أ. سعاد عبد الكريم", "أ. مهند فاضل", "Asia/Baghdad", "arabicIndic", "gregorian", current.Version), token));

        var years = services.GetRequiredService<AcademicYearService>();
        var year = Ensure(await years.CreateAsync(new SaveAcademicYearCommand("2026-2027", "2026-09-21", "2027-06-30", 0), token));
        year = Ensure(await years.AddTermAsync(year.Id, new SaveTermCommand("الفصل الأول", "2026-09-21", "2027-01-20", year.Version), token));
        year = Ensure(await years.AddTermAsync(year.Id, new SaveTermCommand("الفصل الثاني", "2027-02-05", "2027-06-30", year.Version), token));
        Ensure(await years.SetCurrentTermAsync(year.Id, year.Terms[0].Id, year.Version, token));

        // Shift mode creates the morning (and evening) shift; each gets its own bell plan with its own break.
        var structure = services.GetRequiredService<TimetableStructureService>();
        var shifts = Ensure(await services.GetRequiredService<ShiftModeService>().SetAsync(new SetShiftModeCommand(saved.StudyType, saved.Version), token)).Shifts;
        var morning = await PlanAsync(structure, year.Id, shifts.Single(shift => shift.Kind == "morning"), "08:00", 45, new BreakSlotDto(3, 15));
        var evening = dualShift ? await PlanAsync(structure, year.Id, shifts.Single(shift => shift.Kind == "evening"), "13:00", 40, new BreakSlotDto(3, 10)) : null;

        // Stages from the template; in the dual variant the preparatory branches study in the evening.
        Ensure(await services.GetRequiredService<SetupTemplatesService>().StagesAsync(year.Id, new StageTemplateCommand("secondary",
        [
            new("intermediate-1", [], 3, morning.Id, "arabic"),
            new("intermediate-2", [], 3, morning.Id, "arabic"),
            new("intermediate-3", [], 3, morning.Id, "arabic"),
            new("preparatory-4", ["scientific", "literary"], 2, (evening ?? morning).Id, "arabic"),
        ]), apply: true, token));
        var stages = services.GetRequiredService<StagesSectionsService>();
        var first = (await services.GetRequiredService<StageCardsService>().ListAsync(year.Id, includeArchived: false, token))[0].Stage;
        // The first grade goes home earlier: 6 lessons a day instead of the shift's 7 (ADR 0027).
        Ensure(await stages.SetStageDayLessonsAsync(year.Id, first.Id,
            new SetStageDayLessonsCommand(SundayToThursday.Select(day => new DayLessonsDto(day, 6)).ToArray(), first.Version), token));

        await DemoCatalog.CreateSubjectsAsync(services.GetRequiredService<SubjectsService>(), token);
        await CreateCurriculumAsync(year.Id, first.Id);
        await DemoCatalog.CreateTeachersAsync(services.GetRequiredService<TeachersService>(), token);
        await DemoCatalog.CreateCalendarAsync(services.GetRequiredService<CalendarService>(), token);
    }

    private async Task<ShiftDto> PlanAsync(TimetableStructureService structure, long yearId, ShiftDto shift, string start, int lessonMinutes, BreakSlotDto pause)
    {
        var plan = Ensure(TimetableStructureService.Generate(new GeneratePeriodsCommand(start, lessonMinutes, 7, 0, null, [pause])));
        var periods = plan.Periods.Select(period => new PeriodInput(period.Kind, period.StartTime, period.EndTime, period.StartBell, period.EndBell)).ToArray();
        return Ensure(await structure.ReplacePeriodsAsync(yearId, shift.Id, new ReplacePeriodsCommand(periods, shift.Version), token));
    }

    /// <summary>Sample lines for the first grade, copied to the second; every line is noted as a demo number.</summary>
    private async Task CreateCurriculumAsync(long yearId, long firstStageId)
    {
        var curriculum = services.GetRequiredService<CurriculumService>();
        var table = await curriculum.GetTableAsync(yearId, token);
        foreach (var (subject, label, lessons) in FirstGradeSample)
        {
            var subjectId = table.Rows.First(row => row.SubjectName == subject).SubjectId;
            table = Ensure(await curriculum.SetCellAsync(yearId, new SetCurriculumCellCommand(firstStageId, subjectId, label, lessons, null, null), token));
        }
        var second = table.Stages[1].Id;
        Ensure(await services.GetRequiredService<CurriculumHelpersService>().CopyAsync(yearId, new CopyCurriculumCommand(firstStageId, [second]), apply: true, token));
        table = await curriculum.GetTableAsync(yearId, token);
        foreach (var cell in table.Rows.SelectMany(row => row.Cells.Select(cell => (row, cell))).Where(item => item.cell.EntryId is not null))
        {
            Ensure(await curriculum.UpdateEntryAsync(cell.cell.EntryId!.Value,
                new SaveCurriculumEntryCommand(cell.cell.WeeklyLessons!.Value, cell.row.Label, false, DemoDataSeeder.CurriculumSampleNote, cell.cell.Version!.Value), token));
        }
    }

    /// <summary>Demo data must satisfy every rule; a failure is a bug in the sample, so it stops the command.</summary>
    internal static T Ensure<T>(OperationResult<T> result) =>
        result.Succeeded
            ? result.Value!
            : throw new InvalidOperationException($"Demo data rejected: {result.ErrorCode} {string.Join(", ", result.FieldErrors.Select(error => $"{error.Field}={error.Code}"))}");
}
