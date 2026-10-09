using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Generation;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Generation;

/// <summary>Header of every printed or exported timetable: school, academic year and version.</summary>
/// <param name="ArabicIndicNumerals">The school's numeral setting (Arabic-Indic or Western digits).</param>
public sealed record TimetableDocumentHeader(string SchoolName, string YearLabel, int VersionNumber, bool ArabicIndicNumerals);

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

    public async Task<OperationResult<TimetableFile>> ExcelAsync(long versionId, CancellationToken token)
    {
        if (await store.FirstOrDefaultAsync(store.Read<TimetableVersion>().Where(item => item.Id == versionId), token) is not { } version)
            return OperationResult.Failure<TimetableFile>(ErrorCodes.NotFound);
        var profile = await store.FirstOrDefaultAsync(store.Read<SchoolProfile>(), token);
        var year = await store.FirstOrDefaultAsync(store.Read<AcademicYear>().Where(item => item.Id == version.AcademicYearId), token);
        var header = new TimetableDocumentHeader(profile?.Name ?? string.Empty, year?.Label ?? string.Empty, version.Number,
            profile?.NumeralSystem != NumeralSystem.Western);
        var dto = TimetableService.ToDto(version, TimetableService.SnapshotOf(version), version.InputHash);
        // An ASCII file name (the browser shows it as is); the workbook itself is Arabic.
        return OperationResult.Success(new TimetableFile($"timetable-v{version.Number}.xlsx", ExcelContentType, exporter.ToExcel(header, dto)));
    }
}
