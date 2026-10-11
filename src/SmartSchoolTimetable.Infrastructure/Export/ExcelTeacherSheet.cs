using ClosedXML.Excel;
using SmartSchoolTimetable.Application.Teachers;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Infrastructure.Export;

/// <summary>The teachers workbook (ClosedXML): the empty Arabic template and the reader behind the Excel import (M2).</summary>
public sealed class ExcelTeacherSheet : ITeacherSheet
{
    private static readonly string[] Headers =
    [
        TeacherSheetColumns.FullName, TeacherSheetColumns.ShortName, TeacherSheetColumns.Specializations,
        TeacherSheetColumns.MaxPerDay, TeacherSheetColumns.MaxPerWeek, TeacherSheetColumns.Notes,
    ];

    /// <summary>Rows scanned for the header, so a title line above it does no harm.</summary>
    private const int HeaderSearchRows = 5;

    public byte[] CreateTemplate()
    {
        using var book = new XLWorkbook();
        var data = book.Worksheets.Add(TeacherSheetColumns.SheetName);
        data.RightToLeft = true;
        for (var column = 0; column < Headers.Length; column++)
        {
            var cell = data.Cell(1, column + 1);
            cell.Value = Headers[column];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE8F7");
            data.Column(column + 1).Width = column == 0 ? 34 : column == 2 ? 40 : 26;
        }
        data.SheetView.FreezeRows(1);

        var help = book.Worksheets.Add(TeacherSheetColumns.InstructionsSheetName);
        help.RightToLeft = true;
        help.Column(1).Width = 110;
        var lines = new[]
        {
            "تعليمات ملء القالب",
            "١. اكتب كل معلم في صف مستقل تحت صف العناوين في ورقة «المعلمون»، ولا تغيّر نص العناوين.",
            "٢. الاسم الكامل مطلوب. الاسم المختصر اختياري: إن تُرك فارغاً اقترحه البرنامج من الاسم الكامل.",
            "٣. التخصصات: أسماء مواد موجودة في البرنامج مفصولة بفاصلة عربية «،». المادة غير المعروفة تُتجاهل مع تنبيه.",
            "٤. الحد الأقصى للحصص يومياً وأسبوعياً: رقم صحيح أو فارغ (فارغ = بلا حد).",
            "٥. بعد رفع الملف تظهر معاينة بنتيجة كل صف دون حفظ أي شيء، ثم تختار استيراد الصفوف السليمة فقط.",
            "٦. معلم موجود مسبقاً بالاسم نفسه (مع تجاهل الفروق في الهمزات والياء والتاء المربوطة والمسافات) لا يُعدَّل ولا يُكرَّر؛ فإعادة رفع الملف نفسه لا تغيّر شيئاً.",
        };
        for (var index = 0; index < lines.Length; index++)
        {
            help.Cell(index + 1, 1).Value = lines[index];
            help.Cell(index + 1, 1).Style.Alignment.WrapText = true;
        }
        help.Cell(1, 1).Style.Font.Bold = true;

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    public IReadOnlyList<TeacherSheetRow> Read(Stream workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        try
        {
            return ReadRows(workbook);
        }
        catch (ImportFileException)
        {
            throw;
        }
#pragma warning disable CA1031 // Untrusted input: whatever the parser throws on a damaged file means "not a usable workbook", never a server error.
        catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
        {
            throw new ImportFileException(ImportFileProblem.NotAWorkbook);
        }
    }

    private static List<TeacherSheetRow> ReadRows(Stream workbook)
    {
        XLWorkbook book;
        try
        {
            book = new XLWorkbook(workbook);
        }
#pragma warning disable CA1031 // The file is untrusted input and the parser throws many exception types on damaged packages (even NullReferenceException).
        catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
        {
            throw new ImportFileException(ImportFileProblem.NotAWorkbook);
        }
        using (book)
        {
            var sheet = book.Worksheets.FirstOrDefault(item => item.Name == TeacherSheetColumns.SheetName) ?? book.Worksheets.FirstOrDefault()
                ?? throw new ImportFileException(ImportFileProblem.NotTheTemplate);
            var used = sheet.RangeUsed();
            if (used is null)
                throw new ImportFileException(ImportFileProblem.NotTheTemplate);

            var firstRow = used.FirstRow().RowNumber();
            var lastRow = used.LastRow().RowNumber();
            var columns = new int[Headers.Length];
            var headerRow = 0;
            for (var row = firstRow; row <= Math.Min(lastRow, firstRow + HeaderSearchRows - 1) && headerRow == 0; row++)
            {
                var found = Headers.Select(header => FindColumn(sheet, row, header, used)).ToArray();
                if (found[0] > 0)
                {
                    headerRow = row;
                    columns = found;
                }
            }
            if (headerRow == 0)
                throw new ImportFileException(ImportFileProblem.NotTheTemplate);

            var rows = new List<TeacherSheetRow>();
            for (var row = headerRow + 1; row <= lastRow; row++)
            {
                var cells = columns.Select(column => column > 0 ? sheet.Cell(row, column).GetFormattedString() : string.Empty).ToArray();
                if (cells.All(string.IsNullOrWhiteSpace))
                    continue;
                if (rows.Count >= TeacherImportService.MaxRows)
                    throw new ImportFileException(ImportFileProblem.TooManyRows);
                rows.Add(new TeacherSheetRow(row, cells[0], cells[1], cells[2], cells[3], cells[4], cells[5]));
            }
            return rows;
        }
    }

    /// <summary>The column whose header reads like <paramref name="header"/> (Arabic-normalised, so spacing and alef forms do not matter), or 0.</summary>
    private static int FindColumn(IXLWorksheet sheet, int row, string header, IXLRange used)
    {
        var wanted = ArabicText.Normalize(header);
        // The "(مطلوب)" hint and the "«،»" hint are part of the template text; a header that merely starts with the same words is accepted.
        var stem = ArabicText.Normalize(header.Split('(')[0]);
        for (var column = used.FirstColumn().ColumnNumber(); column <= used.LastColumn().ColumnNumber(); column++)
        {
            var text = ArabicText.Normalize(sheet.Cell(row, column).GetFormattedString());
            if (text.Length > 0 && (text == wanted || text.StartsWith(stem, StringComparison.Ordinal)))
                return column;
        }
        return 0;
    }
}
