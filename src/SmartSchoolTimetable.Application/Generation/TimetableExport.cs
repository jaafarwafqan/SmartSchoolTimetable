using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Generation;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Generation;

/// <summary>Header of every printed or exported timetable: school, academic year and version.</summary>
/// <param name="ArabicIndicNumerals">The school's numeral setting (Arabic-Indic or Western digits).</param>
/// <param name="Term">The semester (1 or 2) whose daily sessions give the clock times (R3; ignored for a single session).</param>
public sealed record TimetableDocumentHeader(string SchoolName, string YearLabel, int VersionNumber, bool ArabicIndicNumerals, int Term = 1);

public sealed record TimetableFile(string FileName, string ContentType, byte[] Content);

/// <summary>Writes a timetable to an Excel workbook (the adapter lives in Infrastructure; ADR 0041).</summary>
public interface ITimetableExporter
{
    byte[] ToExcel(TimetableDocumentHeader header, TimetableDto timetable);
}

/// <summary>«تصدير Excel» (Phase 4 M5): the version's grids as a right-to-left workbook.</summary>
public sealed class TimetableExportService(IDataStore store, ITimetableExporter exporter)
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<OperationResult<TimetableFile>> ExcelAsync(long versionId, CancellationToken token, int term = 1)
    {
        if (term is < 1 or > SessionPlan.MaxTerms)
            return OperationResult.Invalid<TimetableFile>("Term", ErrorCodes.ValueOutOfRange);
        if (await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == versionId), token) is not { } version)
            return OperationResult.Failure<TimetableFile>(ErrorCodes.NotFound);
        var profile = await store.FirstOrDefaultAsync(store.Read<SchoolProfile>(), token);
        var year = await store.FirstOrDefaultAsync(store.Read<AcademicYear>().Where(item => item.Id == version.AcademicYearId), token);
        var header = new TimetableDocumentHeader(profile?.Name ?? string.Empty, year?.Label ?? string.Empty, version.Number,
            profile?.NumeralSystem != NumeralSystem.Western, term);
        var input = TimetableService.SnapshotOf(version);
        var sessions = await TimetableService.SessionsAsync(store, version.AcademicYearId, input, token);
        var dto = TimetableService.ToDto(version, input, version.InputHash, sessions);
        // An ASCII file name (the browser shows it as is); the workbook itself is Arabic. Two-session schools get the semester.
        var name = sessions is null ? $"timetable-v{version.Number}.xlsx" : $"timetable-v{version.Number}-term{term}.xlsx";
        return OperationResult.Success(new TimetableFile(name, ExcelContentType, exporter.ToExcel(header, dto)));
    }
}
