using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClosedXML.Excel;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Application.Teachers;
using static SmartSchoolTimetable.Tests.ApiTestHelpers;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>
/// M2 Excel import of teachers on temporary databases: the template, a dry run that saves nothing, per-row Arabic-codes, valid rows only, Arabic name
/// normalisation (alef forms, ya, ta marbuta, spaces), an idempotent re-import that never overwrites, and unusable files.
/// </summary>
public sealed class TeacherImportTests
{
    private static readonly string[] CreatedNames = ["أحمد علي حسن", "فاطمة محمد"];

    private static byte[] Workbook(params string?[][] rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add(TeacherSheetColumns.SheetName);
        string[] headers = [TeacherSheetColumns.FullName, TeacherSheetColumns.ShortName, TeacherSheetColumns.Specializations, TeacherSheetColumns.MaxPerDay, TeacherSheetColumns.MaxPerWeek, TeacherSheetColumns.Notes];
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];
        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < rows[row].Length; column++)
            {
                if (rows[row][column] is { } value)
                    sheet.Cell(row + 2, column + 1).Value = value;
            }
        }
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    private static async Task<HttpResponseMessage> UploadAsync(TestHost host, string path, byte[] file, string token)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(content, "file", "teachers.xlsx");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form };
        request.Headers.TryAddWithoutValidation("Origin", "http://127.0.0.1:5080");
        request.Headers.TryAddWithoutValidation("X-Local-Launch-Token", token);
        return await host.Client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, text);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static async Task<string[]> TeacherNamesAsync(TestHost host) =>
        (await JsonAsync(await host.Client.GetAsync("/api/v1/teachers/?pageSize=100"))).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("fullName").GetString()!).ToArray();

    private static string[] Statuses(JsonElement preview) => preview.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("status").GetString()!).ToArray();

    private static string[] IssueCodes(JsonElement preview, int index) =>
        preview.GetProperty("rows")[index].GetProperty("issues").EnumerateArray().Select(issue => issue.GetProperty("code").GetString()!).ToArray();

    [Fact]
    public async Task TheTemplateIsAnEmptyArabicWorkbookWithTheInstructionsSheet()
    {
        await using var host = new TestHost();
        await SetupOwnerAsync(host);
        var response = await host.Client.GetAsync("/api/v1/teachers/import/template");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var book = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        var sheet = book.Worksheet(TeacherSheetColumns.SheetName);
        Assert.Equal(TeacherSheetColumns.FullName, sheet.Cell(1, 1).GetString());
        Assert.True(sheet.RightToLeft);
        Assert.Null(sheet.Cell(2, 1).GetString() is { Length: > 0 } text ? text : null); // no sample data that could be imported by accident
        Assert.NotNull(book.Worksheet(TeacherSheetColumns.InstructionsSheetName));
        // And the empty template itself is a valid file: it previews as zero rows.
        var preview = await JsonAsync(await UploadAsync(host, "/api/v1/teachers/import/preview", await response.Content.ReadAsByteArrayAsync(), (await host.GetBootstrapAsync()).LaunchToken));
        Assert.Equal(0, preview.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task ADryRunSavesNothingAndExplainsEveryRowThenOnlyTheValidRowsAreImported()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var file = Workbook(
            ["أحمد علي حسن", "أحمد علي", null, "6", "25", "ملاحظة"],     // ready, with its own short name
            ["فاطمة محمد", null, "مادة غير معروفة", null, null, null],     // ready: short name proposed, unknown subject warned
            ["أحمد علي حسن", null, null, null, null, null],               // the same name again in the file
            [null, "مختصر", null, null, null, null],                       // no name
            ["سعد كريم", "أحمد علي", null, null, null, null],             // short name taken by the first row of this file
            ["ليث جاسم", null, null, "abc", null, null],                  // limit not a number
            ["نور هادي", null, null, "9", "5", null]);                    // day limit above the week limit

        var preview = await JsonAsync(await UploadAsync(host, "/api/v1/teachers/import/preview", file, token));
        Assert.Equal(["ready", "ready", "error", "error", "error", "error", "error"], Statuses(preview));
        Assert.Equal((2, 0, 5), (preview.GetProperty("ready").GetInt32(), preview.GetProperty("existing").GetInt32(), preview.GetProperty("errors").GetInt32()));
        Assert.Contains("shortNameProposed", IssueCodes(preview, 1));
        Assert.Contains("unknownSubject", IssueCodes(preview, 1));
        Assert.Contains("duplicateInFile", IssueCodes(preview, 2));
        Assert.Contains("nameMissing", IssueCodes(preview, 3));
        Assert.Contains("shortNameTaken", IssueCodes(preview, 4));
        Assert.Contains("limitInvalid", IssueCodes(preview, 5));
        Assert.Contains("limitOrder", IssueCodes(preview, 6));
        Assert.Empty(await TeacherNamesAsync(host)); // the dry run saved nothing

        var applied = await JsonAsync(await UploadAsync(host, "/api/v1/teachers/import", file, token));
        Assert.Equal((2, 0, 5), (applied.GetProperty("created").GetInt32(), applied.GetProperty("existing").GetInt32(), applied.GetProperty("rejected").GetInt32()));
        Assert.Equal(CreatedNames.Order(StringComparer.Ordinal), (await TeacherNamesAsync(host)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ImportingTheSameFileAgainChangesNothingAndNamesDifferingOnlyInSpellingAreTheSamePerson()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var first = Workbook(["إبراهيم آل خليل", null, null, "5", null, "الأصل"], ["سلمى الهاشمية", null, null, null, null, null]);
        Assert.Equal(2, (await JsonAsync(await UploadAsync(host, "/api/v1/teachers/import", first, token))).GetProperty("created").GetInt32());

        // The same file: both exist, nothing is created or changed.
        var again = await JsonAsync(await UploadAsync(host, "/api/v1/teachers/import", first, token));
        Assert.Equal((0, 2), (again.GetProperty("created").GetInt32(), again.GetProperty("existing").GetInt32()));
        Assert.Equal(2, (await TeacherNamesAsync(host)).Length);

        // Other spellings of the same names: alef forms, ya/alef maqsura, ta marbuta/ha, extra spaces and diacritics.
        var spelled = Workbook(["ابراهيم   ال خليل", null, null, "9", null, "تغيير"], ["سلمى  الهاشميه", null, null, null, null, null], ["سَلمَى الهاشميّة", null, null, null, null, null]);
        var preview = await JsonAsync(await UploadAsync(host, "/api/v1/teachers/import/preview", spelled, token));
        Assert.Equal(["exists", "exists", "error"], Statuses(preview)); // the third is the second one spelled differently: a duplicate inside the file
        Assert.Contains("duplicateInFile", IssueCodes(preview, 2));
        var result = await JsonAsync(await UploadAsync(host, "/api/v1/teachers/import", spelled, token));
        Assert.Equal(0, result.GetProperty("created").GetInt32());
        // Never overwritten silently: the first teacher keeps their own note and limit.
        var teachers = await JsonAsync(await host.Client.GetAsync("/api/v1/teachers/?pageSize=100"));
        var first0 = teachers.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("fullName").GetString() == "إبراهيم آل خليل");
        Assert.Equal("الأصل", first0.GetProperty("notes").GetString());
        Assert.Equal(5, first0.GetProperty("maxLessonsPerDay").GetInt32());
    }

    [Fact]
    public async Task AFileThatIsNotAWorkbookOrNotTheTemplateIsRefusedWithACode()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        await AssertApiErrorAsync(await UploadAsync(host, "/api/v1/teachers/import/preview", "not a workbook"u8.ToArray(), token), ErrorCodes.ImportFileInvalid);

        using var other = new XLWorkbook();
        other.Worksheets.Add("ورقة").Cell(1, 1).Value = "عمود آخر";
        using var stream = new MemoryStream();
        other.SaveAs(stream);
        await AssertApiErrorAsync(await UploadAsync(host, "/api/v1/teachers/import/preview", stream.ToArray(), token), ErrorCodes.ImportFileInvalid);

        // A zip that is not a workbook, and a damaged package (the parser fails inside): a code, never a server error.
        using var zipped = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zipped, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            await using var entry = archive.CreateEntry("[Content_Types].xml").Open();
            await entry.WriteAsync("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>"u8.ToArray());
        }
        await AssertApiErrorAsync(await UploadAsync(host, "/api/v1/teachers/import/preview", zipped.ToArray(), token), ErrorCodes.ImportFileInvalid);

        // Too many rows.
        var many = Workbook(Enumerable.Range(0, TeacherImportService.MaxRows + 1).Select(index => new string?[] { $"معلم رقم {index}", null, null, null, null, null }).ToArray());
        await AssertApiErrorAsync(await UploadAsync(host, "/api/v1/teachers/import/preview", many, token), ErrorCodes.ImportTooManyRows);
        Assert.Empty(await TeacherNamesAsync(host));
    }

    [Fact]
    public async Task TheImportNeedsTheLaunchTokenAndASignedInOwner()
    {
        await using var host = new TestHost();
        var (token, _) = await SetupOwnerAsync(host);
        var file = Workbook(["معلم جديد", null, null, null, null, null]);
        Assert.NotEqual(HttpStatusCode.OK, (await UploadAsync(host, "/api/v1/teachers/import", file, "wrong-token")).StatusCode);
        Assert.Empty(await TeacherNamesAsync(host));
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(host, "/api/v1/teachers/import", file, token)).StatusCode);
    }
}
