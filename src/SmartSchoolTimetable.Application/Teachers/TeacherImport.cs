using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Teachers;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Teachers;

/// <summary>One data row of the teachers sheet, every cell as the text the owner typed (so a bad value is a row error, never a crash).</summary>
/// <param name="Row">The row number in the sheet (1-based, as Excel shows it).</param>
public sealed record TeacherSheetRow(int Row, string? FullName, string? ShortName, string? Specializations, string? MaxPerDay, string? MaxPerWeek, string? Notes);

/// <summary>Reads and writes the teachers workbook (Infrastructure: ClosedXML). A file that is not a usable workbook throws <see cref="ImportFileException"/>.</summary>
public interface ITeacherSheet
{
    /// <summary>The empty template: an Arabic header row and a sheet of instructions. It never contains data.</summary>
    byte[] CreateTemplate();

    IReadOnlyList<TeacherSheetRow> Read(Stream workbook);
}

public enum ImportFileProblem
{
    NotAWorkbook,
    NotTheTemplate,
    TooManyRows,
}

public sealed class ImportFileException(ImportFileProblem problem) : Exception($"The import file cannot be used: {problem}.")
{
    public ImportFileProblem Problem { get; } = problem;
}

/// <summary>The Arabic column headers of the template (a generated document, so Arabic only), shared by the writer and the reader.</summary>
public static class TeacherSheetColumns
{
    public const string FullName = "الاسم الكامل (مطلوب)";
    public const string ShortName = "الاسم المختصر";
    public const string Specializations = "التخصصات (مواد مفصولة بـ «،»)";
    public const string MaxPerDay = "الحد الأقصى للحصص يومياً";
    public const string MaxPerWeek = "الحد الأقصى للحصص أسبوعياً";
    public const string Notes = "ملاحظات";
    public const string SheetName = "المعلمون";
    public const string InstructionsSheetName = "تعليمات";
}

/// <param name="Code">A stable code the screen maps to Arabic (nameMissing, nameTooLong, duplicateInFile, shortNameTooLong, shortNameTaken, limitInvalid,
/// limitOrder, notesTooLong, unknownSubject, shortNameProposed, constraint).</param>
/// <param name="Severity">"error" (the row is not imported), "warning" (it is, with the note) or "info" (a normal automatic choice such as a proposed short name).</param>
/// <param name="Value">What the code is about: the other row, the unknown subject, the proposed short name, the field.</param>
public sealed record ImportIssueDto(string Code, string Severity, string? Value);

/// <param name="Status">"ready" (will be added), "exists" (a teacher with this name is already there: left as it is) or "error".</param>
public sealed record TeacherImportRowDto(int Row, string FullName, string? ShortName, string Status, IReadOnlyList<ImportIssueDto> Issues);

public sealed record TeacherImportPreviewDto(IReadOnlyList<TeacherImportRowDto> Rows, int Ready, int Existing, int Errors, int Warnings);

public sealed record TeacherImportResultDto(int Created, int Existing, int Rejected);

/// <summary>
/// Excel import of teachers with a dry run (M2): upload → a preview with the outcome of every row (nothing is saved) → apply, which re-reads the same
/// file and adds only the valid rows in one transaction. A teacher who already exists (same name after Arabic normalisation) is never overwritten:
/// importing the same file again changes nothing.
/// </summary>
public sealed class TeacherImportService(IDataStore store, ITeacherSheet sheet, TimeProvider clock)
{
    public const int MaxRows = 500;

    public byte[] Template() => sheet.CreateTemplate();

    public async Task<OperationResult<TeacherImportPreviewDto>> PreviewAsync(Stream workbook, CancellationToken token)
    {
        var classified = await ClassifyAsync(workbook, token);
        return classified.Failure is { } failure ? OperationResult.Failure<TeacherImportPreviewDto>(failure) : OperationResult.Success(classified.Preview!);
    }

    public async Task<OperationResult<TeacherImportResultDto>> ApplyAsync(Stream workbook, CancellationToken token)
    {
        var classified = await ClassifyAsync(workbook, token);
        if (classified.Failure is { } failure)
            return OperationResult.Failure<TeacherImportResultDto>(failure);
        var ready = classified.ToCreate;
        foreach (var teacher in ready)
            store.Add(teacher);
        var preview = classified.Preview!;
        if (ready.Count > 0)
            AuditTrail.Record(store, clock, AuditEvents.ImportApplied, "teacher", $"{ready.Count} teachers imported from a workbook.", new { teachers = ready.Count });
        return await store.SaveAsync(() => new TeacherImportResultDto(ready.Count, preview.Existing, preview.Errors), "File", token);
    }

    /// <summary>The key two spellings of one name share: the stored normalisation plus ة and ه treated as the same letter (not used for stored values).</summary>
    public static string MatchKey(string? value) => ArabicText.Normalize(value).Replace('ة', 'ه');

    private sealed record Classified(string? Failure, TeacherImportPreviewDto? Preview, List<Teacher> ToCreate);

    private async Task<Classified> ClassifyAsync(Stream workbook, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        IReadOnlyList<TeacherSheetRow> rows;
        try
        {
            rows = sheet.Read(workbook);
        }
        catch (ImportFileException exception)
        {
            return new Classified(exception.Problem == ImportFileProblem.TooManyRows ? ErrorCodes.ImportTooManyRows : ErrorCodes.ImportFileInvalid, null, []);
        }

        var existing = await store.ListAsync(store.Query<Teacher>().Select(teacher => new { teacher.FullName, teacher.NormalizedShortName }), token);
        var existingNames = existing.Select(teacher => MatchKey(teacher.FullName)).ToHashSet(StringComparer.Ordinal);
        var shortNames = existing.Select(teacher => teacher.NormalizedShortName).ToHashSet(StringComparer.Ordinal);
        var subjects = (await store.ListAsync(store.Read<Subject>().Where(subject => !subject.IsArchived).Select(subject => new { subject.Id, subject.Name }), token))
            .GroupBy(subject => MatchKey(subject.Name)).ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);
        var grid = await ScheduleGrids.LoadAsync(store, token);

        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var results = new List<TeacherImportRowDto>();
        var toCreate = new List<Teacher>();
        foreach (var row in rows)
        {
            var issues = new List<ImportIssueDto>();
            var fullName = ArabicText.Clean(row.FullName);
            string status;
            string? shortName = null;
            if (fullName.Length == 0)
            {
                issues.Add(new ImportIssueDto("nameMissing", "error", null));
                status = "error";
            }
            else if (fullName.Length > Teacher.FullNameMaxLength)
            {
                issues.Add(new ImportIssueDto("nameTooLong", "error", null));
                status = "error";
            }
            else if (seen.TryGetValue(MatchKey(fullName), out var firstRow))
            {
                issues.Add(new ImportIssueDto("duplicateInFile", "error", firstRow.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                status = "error";
            }
            else if (existingNames.Contains(MatchKey(fullName)))
            {
                seen[MatchKey(fullName)] = row.Row;
                status = "exists";
            }
            else
            {
                seen[MatchKey(fullName)] = row.Row;
                (status, shortName) = Validate(row, fullName, shortNames, subjects, grid, issues, out var teacher);
                if (status == "ready" && teacher is not null)
                {
                    toCreate.Add(teacher);
                    shortNames.Add(ArabicText.Normalize(shortName));
                }
            }
            results.Add(new TeacherImportRowDto(row.Row, fullName, shortName, status, issues));
        }
        var preview = new TeacherImportPreviewDto(results, results.Count(item => item.Status == "ready"), results.Count(item => item.Status == "exists"),
            results.Count(item => item.Status == "error"), results.Count(item => item.Issues.Any(issue => issue.Severity == "warning")));
        return new Classified(null, preview, toCreate);
    }

    private static (string Status, string? ShortName) Validate(TeacherSheetRow row, string fullName, HashSet<string> shortNames, Dictionary<string, long> subjects,
        ScheduleGrid grid, List<ImportIssueDto> issues, out Teacher? teacher)
    {
        teacher = null;
        var shortName = ArabicText.Clean(row.ShortName);
        if (shortName.Length == 0)
        {
            var proposed = TeacherNames.ProposeShortName(fullName, shortNames.Contains);
            if (proposed is null)
            {
                issues.Add(new ImportIssueDto("shortNameTaken", "error", null));
                return ("error", null);
            }
            shortName = proposed;
            issues.Add(new ImportIssueDto("shortNameProposed", "info", proposed));
        }
        else if (shortName.Length > Teacher.ShortNameMaxLength)
        {
            issues.Add(new ImportIssueDto("shortNameTooLong", "error", null));
            return ("error", shortName);
        }
        else if (shortNames.Contains(ArabicText.Normalize(shortName)))
        {
            issues.Add(new ImportIssueDto("shortNameTaken", "error", shortName));
            return ("error", shortName);
        }

        var perDay = ParseLimit(row.MaxPerDay, "day", issues);
        var perWeek = ParseLimit(row.MaxPerWeek, "week", issues);
        var notes = ArabicText.Clean(row.Notes);
        if (notes.Length > Teacher.NotesMaxLength)
            issues.Add(new ImportIssueDto("notesTooLong", "error", null));
        if (perDay is not null && perWeek is not null && perDay > perWeek)
            issues.Add(new ImportIssueDto("limitOrder", "error", null));

        var specializationIds = new List<long>();
        foreach (var name in SplitNames(row.Specializations))
        {
            if (subjects.TryGetValue(MatchKey(name), out var id))
            {
                if (!specializationIds.Contains(id))
                    specializationIds.Add(id);
            }
            else
            {
                issues.Add(new ImportIssueDto("unknownSubject", "warning", name));
            }
        }
        if (specializationIds.Count > Teacher.MaxSpecializations)
            issues.Add(new ImportIssueDto("constraint", "error", "SpecializationIds"));

        if (issues.Any(issue => issue.Severity == "error"))
            return ("error", shortName);
        try
        {
            teacher = Teacher.Create(new TeacherDetails(fullName, shortName, null, null, false, null, null, null, perDay, perWeek, notes.Length == 0 ? null : notes,
                specializationIds), grid);
        }
        catch (DomainValidationException exception)
        {
            issues.Add(new ImportIssueDto("constraint", "error", exception.Errors.Count > 0 ? exception.Errors[0].Field : null));
            return ("error", shortName);
        }
        return ("ready", shortName);
    }

    private static int? ParseLimit(string? text, string which, List<ImportIssueDto> issues)
    {
        var trimmed = ArabicText.Normalize(text); // folds Arabic-Indic digits too
        if (trimmed.Length == 0)
            return null;
        if (int.TryParse(trimmed, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= 60)
            return value;
        issues.Add(new ImportIssueDto("limitInvalid", "error", which));
        return null;
    }

    private static IEnumerable<string> SplitNames(string? text) =>
        (text ?? string.Empty).Split(['،', ',', ';', '؛', '\n', '\r', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ArabicText.Clean).Where(name => name.Length > 0);
}
