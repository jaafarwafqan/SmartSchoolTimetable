using System.Globalization;
using ClosedXML.Excel;
using SmartSchoolTimetable.Application.Generation;

namespace SmartSchoolTimetable.Infrastructure.Export;

/// <summary>
/// The Excel adapter (ClosedXML, ADR 0041): one sheet for the master timetable («الجدول العام»), one per section and
/// one per teacher. Every sheet is right-to-left with Arabic headers, the school's name and year on top, subject
/// cells filled with a light tint of the subject colour, and printing set to A4 (landscape for the master). With daily
/// sessions (R3) the clock times follow the session of each day in the exported semester, which the header names.
/// </summary>
public sealed class ExcelTimetableExporter : ITimetableExporter
{
    // Light fills matching tokens.css --color-subject-1..10 (printed documents use the same palette).
    private static readonly string[] SubjectFills =
        ["#DCE8FB", "#D9F2E3", "#FCEBC8", "#FBDDE2", "#E6E0FA", "#D0F0EE", "#FDDFC8", "#E2E8F0", "#E7F3C8", "#D4EDFB"];

    private static readonly Dictionary<int, string> DayNames = new()
    {
        [1] = "الاثنين", [2] = "الثلاثاء", [3] = "الأربعاء", [4] = "الخميس", [5] = "الجمعة", [6] = "السبت", [7] = "الأحد",
    };

    private const string MasterSheet = "الجدول العام";
    private const string DocumentTitle = "جدول الدروس الأسبوعي";
    private const int MaxSheetName = 31;

    public byte[] ToExcel(TimetableDocumentHeader header, TimetableDto timetable)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(timetable);
        using var workbook = new XLWorkbook { RightToLeft = true };
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var subjects = timetable.Subjects.ToDictionary(subject => subject.Id);
        var teachers = timetable.Teachers.ToDictionary(teacher => teacher.Id);
        var sections = timetable.Sections.ToDictionary(section => section.Id);
        string Number(int value) => Digits(value.ToString(CultureInfo.InvariantCulture), header.ArabicIndicNumerals);
        string SectionName(long id) => sections.TryGetValue(id, out var section) ? $"{section.StageName} / {section.Label}" : string.Empty;
        var lessonCount = Math.Max(1, timetable.Sections.SelectMany(section => section.AllowedByDay).Select(day => day.Lessons).DefaultIfEmpty(1).Max());
        var shifts = timetable.Shifts.ToDictionary(shift => shift.Id);
        var sessions = timetable.Sessions;
        // R3: with daily sessions, a day's clock comes from the session it falls in during the exported semester.
        string? DaySession(long? shiftId, int day) => sessions is not null && shiftId == sessions.ShiftId ? sessions.SessionOn(header.Term, day) : null;
        string Range(GridLessonTime time) =>
            $"{Clock12.Format(time.StartMinute, header.ArabicIndicNumerals)} – {Clock12.Format(time.EndMinute, header.ArabicIndicNumerals)}";
        // Lesson clock times in the Iraqi 12-hour form (R1), from the section's shift (or the day's session); empty when unknown.
        string Times(long? shiftId, int lesson, int? day = null)
        {
            var clock = day is { } value && DaySession(shiftId, value) is { } session ? sessions!.TimesOf(session)
                : shiftId is { } id && shifts.TryGetValue(id, out var shift) ? shift.Lessons : null;
            return clock?.FirstOrDefault(item => item.Number == lesson) is { } time ? Range(time) : string.Empty;
        }
        string WithTimes(string label, long? shiftId, int lesson, int? day = null) => Times(shiftId, lesson, day) is { Length: > 0 } times ? $"{label}\n{times}" : label;
        string DayLabel(long? shiftId, int day) =>
            DaySession(shiftId, day) is { } session ? $"{DayNames.GetValueOrDefault(day, string.Empty)}\n{SessionShort(session)}" : DayNames.GetValueOrDefault(day, string.Empty);
        long? OnlyShift(IEnumerable<long> ids) => ids.Distinct().Take(2).ToArray() is [var single] ? single : null;
        var masterShift = OnlyShift(timetable.Sections.Select(section => section.ShiftId));
        var term = sessions is null ? null : header.Term == 2 ? "الفصل الدراسي الثاني" : "الفصل الدراسي الأول";

        // Master: sections × (days × lessons).
        var master = AddSheet(workbook, MasterSheet, names);
        var top = WriteHeader(master, header, MasterSheet, Number, term);
        master.Cell(top, 1).Value = "الشعبة";
        master.Range(top, 1, top + 1, 1).Merge();
        for (var dayIndex = 0; dayIndex < timetable.Days.Count; dayIndex++)
        {
            var first = 2 + dayIndex * lessonCount;
            var day = timetable.Days[dayIndex];
            master.Cell(top, first).Value = DaySession(masterShift, day) is { } session
                ? $"{DayNames.GetValueOrDefault(day, string.Empty)} — {SessionShort(session)}"
                : DayNames.GetValueOrDefault(day, string.Empty);
            master.Range(top, first, top, first + lessonCount - 1).Merge();
            for (var lesson = 1; lesson <= lessonCount; lesson++)
                master.Cell(top + 1, first + lesson - 1).Value = WithTimes(Number(lesson), masterShift, lesson, day);
        }
        StyleHeader(master.Range(top, 1, top + 1, 1 + timetable.Days.Count * lessonCount));
        var masterAt = timetable.Lessons.ToDictionary(lesson => (lesson.SectionId, lesson.Day, lesson.Lesson));
        var row = top + 2;
        foreach (var section in timetable.Sections)
        {
            master.Cell(row, 1).Value = SectionName(section.Id);
            for (var dayIndex = 0; dayIndex < timetable.Days.Count; dayIndex++)
            {
                for (var lesson = 1; lesson <= lessonCount; lesson++)
                {
                    if (!masterAt.TryGetValue((section.Id, timetable.Days[dayIndex], lesson), out var item))
                        continue;
                    var cell = master.Cell(row, 2 + dayIndex * lessonCount + lesson - 1);
                    cell.Value = $"{Subject(item.SubjectId)}\n{Teacher(item.TeacherId)}";
                    Fill(cell, item.SubjectId);
                }
            }
            row++;
        }
        Finish(master, top, row - 1, 1 + timetable.Days.Count * lessonCount, landscape: true);

        foreach (var section in timetable.Sections)
        {
            var sheet = AddSheet(workbook, SectionName(section.Id), names);
            var lessons = timetable.Lessons.Where(lesson => lesson.SectionId == section.Id).ToArray();
            var count = Math.Max(1, section.AllowedByDay.Select(day => day.Lessons).DefaultIfEmpty(1).Max());
            WeekSheet(sheet, header, SectionName(section.Id), timetable.Days, count, lessons, lesson => Teacher(lesson.TeacherId), Number, section.ShiftId);
        }
        foreach (var teacher in timetable.Teachers)
        {
            var sheet = AddSheet(workbook, teacher.Name, names);
            var lessons = timetable.Lessons.Where(lesson => lesson.TeacherId == teacher.Id).ToArray();
            var teacherShift = OnlyShift(lessons.Select(lesson => sections.TryGetValue(lesson.SectionId, out var section) ? section.ShiftId : 0));
            WeekSheet(sheet, header, teacher.Name, timetable.Days, lessonCount, lessons, lesson => SectionName(lesson.SectionId), Number, teacherShift);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();

        string Subject(long id) => subjects.TryGetValue(id, out var subject) ? subject.Name : string.Empty;
        string Teacher(long id) => teachers.TryGetValue(id, out var teacher) ? teacher.ShortName : string.Empty;

        void Fill(IXLCell cell, long subjectId)
        {
            var index = subjects.TryGetValue(subjectId, out var subject) ? Math.Clamp(subject.ColorIndex, 1, SubjectFills.Length) : SubjectFills.Length - 2;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(SubjectFills[index - 1]);
        }

        void WeekSheet(IXLWorksheet sheet, TimetableDocumentHeader documentHeader, string title, IReadOnlyList<int> days, int count, GridLesson[] lessons,
            Func<GridLesson, string> second, Func<int, string> number, long? shiftId)
        {
            var first = WriteHeader(sheet, documentHeader, title, number, term);
            var top = first;
            sheet.Cell(first, 1).Value = "اليوم";
            // Daily sessions: the lesson numbers, then one row of clock times per session; each day names its session.
            var timings = sessions is not null && shiftId == sessions.ShiftId ? sessions.Timings : [];
            for (var lesson = 1; lesson <= count; lesson++)
                sheet.Cell(first, 1 + lesson).Value = timings.Count > 0 ? $"الحصة {number(lesson)}" : WithTimes($"الحصة {number(lesson)}", shiftId, lesson);
            foreach (var timing in timings)
            {
                first++;
                sheet.Cell(first, 1).Value = SessionName(timing.Session);
                for (var lesson = 1; lesson <= count; lesson++)
                    sheet.Cell(first, 1 + lesson).Value = timing.Lessons.FirstOrDefault(item => item.Number == lesson) is { } time ? Range(time) : string.Empty;
            }
            StyleHeader(sheet.Range(top, 1, first, 1 + count));
            var at = lessons.ToDictionary(lesson => (lesson.Day, lesson.Lesson));
            for (var dayIndex = 0; dayIndex < days.Count; dayIndex++)
            {
                var line = first + 1 + dayIndex;
                sheet.Cell(line, 1).Value = DayLabel(shiftId, days[dayIndex]);
                for (var lesson = 1; lesson <= count; lesson++)
                {
                    if (!at.TryGetValue((days[dayIndex], lesson), out var item))
                        continue;
                    var cell = sheet.Cell(line, 1 + lesson);
                    cell.Value = $"{Subject(item.SubjectId)}\n{second(item)}";
                    Fill(cell, item.SubjectId);
                }
            }
            Finish(sheet, top, first + days.Count, 1 + count, landscape: false);
        }
    }

    private static string SessionName(string session) => session switch
    {
        "evening" => "الدوام المسائي",
        "noon" => "الدوام الظهري",
        _ => "الدوام الصباحي",
    };

    private static string SessionShort(string session) => session switch
    {
        "evening" => "مسائي",
        "noon" => "ظهري",
        _ => "صباحي",
    };

    private static string Digits(string value, bool arabicIndic) =>
        arabicIndic ? string.Concat(value.Select(ch => ch is >= '0' and <= '9' ? (char)('٠' + (ch - '0')) : ch)) : value;

    /// <summary>A unique, valid sheet name (31 characters, none of : \ / ? * [ ]).</summary>
    private static IXLWorksheet AddSheet(XLWorkbook workbook, string name, HashSet<string> used)
    {
        var clean = new string(name.Select(ch => ":\\/?*[]".Contains(ch, StringComparison.Ordinal) ? '-' : ch).ToArray()).Trim();
        if (clean.Length == 0)
            clean = "ورقة";
        var candidate = clean.Length > MaxSheetName ? clean[..MaxSheetName] : clean;
        for (var suffix = 2; used.Contains(candidate); suffix++)
        {
            var tail = $" {suffix.ToString(CultureInfo.InvariantCulture)}";
            candidate = (clean.Length + tail.Length > MaxSheetName ? clean[..(MaxSheetName - tail.Length)] : clean) + tail;
        }
        used.Add(candidate);
        var sheet = workbook.Worksheets.Add(candidate);
        sheet.RightToLeft = true;
        return sheet;
    }

    /// <summary>School name, year and the sheet title on top; returns the first row of the table.</summary>
    /// <summary>
    /// MF5: the same header as the printed timetable — school, «جدول الدروس الأسبوعي», year and semester (and version), and
    /// the section or teacher — with the logo when there is one; the footer carries the principal's name, signature and
    /// stamp lines, the print date and page numbers.
    /// </summary>
    private static int WriteHeader(IXLWorksheet sheet, TimetableDocumentHeader header, string title, Func<int, string> number, string? term)
    {
        sheet.Cell(1, 1).Value = header.SchoolName;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = DocumentTitle;
        sheet.Cell(2, 1).Style.Font.Bold = true;
        var semester = term ?? header.TermName;
        sheet.Cell(3, 1).Value = semester is null
            ? $"السنة الدراسية {header.YearLabel} — الإصدار {number(header.VersionNumber)}"
            : $"السنة الدراسية {header.YearLabel} — {semester} — الإصدار {number(header.VersionNumber)}";
        sheet.Cell(4, 1).Value = title;
        sheet.Cell(4, 1).Style.Font.Bold = true;
        if (header.Logo is { Length: > 0 } logo)
        {
            using var image = new MemoryStream(logo);
            sheet.AddPicture(image).MoveTo(sheet.Cell(1, 4)).WithSize(64, 64);
        }
        var footer = sheet.PageSetup.Footer;
        var principal = string.IsNullOrWhiteSpace(header.PrincipalName) ? "مدير المدرسة: ____________" : $"مدير المدرسة: {header.PrincipalName}";
        footer.Center.AddText($"{principal}    التوقيع: ____________    الختم: ____________", XLHFOccurrence.AllPages);
        footer.Right.AddText("صفحة ", XLHFOccurrence.AllPages);
        footer.Right.AddText(XLHFPredefinedText.PageNumber, XLHFOccurrence.AllPages);
        footer.Right.AddText(" من ", XLHFOccurrence.AllPages);
        footer.Right.AddText(XLHFPredefinedText.NumberOfPages, XLHFOccurrence.AllPages);
        footer.Left.AddText(XLHFPredefinedText.Date, XLHFOccurrence.AllPages);
        return 6;
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2F7");
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void Finish(IXLWorksheet sheet, int top, int bottom, int right, bool landscape)
    {
        var table = sheet.Range(top, 1, Math.Max(top, bottom), right);
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Alignment.WrapText = true;
        table.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        table.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Column(1).Width = 22;
        for (var column = 2; column <= right; column++)
            sheet.Column(column).Width = landscape ? 11 : 16;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.PageOrientation = landscape ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);
    }
}
