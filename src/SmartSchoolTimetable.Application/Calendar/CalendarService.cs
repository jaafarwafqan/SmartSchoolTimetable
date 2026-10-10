using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.Calendar;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Calendar;

/// <param name="OutsideCurrentYear">Warning only: some day lies outside the current academic year.</param>
/// <param name="Source">"manual" or "iraqTemplate" (MF8).</param>
/// <param name="IsApproximate">MF8: the date is a Hijri calculation; the official announcement may differ.</param>
/// <param name="IsEnabled">MF8: a disabled entry is ignored (not shown as a holiday, no effect on the schedule).</param>
public sealed record CalendarDayDto(long Id, string Title, string StartDate, string EndDate, string Kind, bool AffectsSchedule, bool OutsideCurrentYear,
    string Source, bool IsApproximate, bool IsEnabled, int Version);

public sealed record SetCalendarDayEnabledCommand(bool Enabled, int Version);

public sealed record ImportIraqHolidaysCommand(long YearId);

/// <summary>A holiday the template would add to a year (MF8). <paramref name="AlreadyAdded"/>: an entry with the same holiday and date exists.</summary>
public sealed record IraqHolidayPreviewDto(string Key, string Title, string StartDate, string EndDate, bool Approximate, bool AlreadyAdded);

public sealed record IraqHolidayPreviewResponse(string YearLabel, IReadOnlyList<IraqHolidayPreviewDto> Holidays);

public sealed record ImportIraqHolidaysResult(int Added, int Skipped);

public sealed record SaveCalendarDayCommand(string? Title, string? StartDate, string? EndDate, string? Kind, bool AffectsSchedule, int Version);

/// <summary>Academic calendar entries. Not year-scoped and never copied to a new year; hard delete after confirmation.</summary>
public sealed class CalendarService(IDataStore store, TimeProvider clock)
{
    /// <param name="from">Optional: only entries overlapping [from, to] (month view).</param>
    public async Task<OperationResult<PagedResult<CalendarDayDto>>> ListAsync(ListQuery query, string? from, string? to, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(query);
        var input = new InputErrors();
        var rangeStart = input.OptionalDate(from, "From");
        var rangeEnd = input.OptionalDate(to, "To");
        if (input.Any)
            return input.ToResult<PagedResult<CalendarDayDto>>();
        var rows = store.Query<CalendarDay>();
        if (rangeStart is { } start)
            rows = rows.Where(row => row.EndDate >= start);
        if (rangeEnd is { } end)
            rows = rows.Where(row => row.StartDate <= end);
        var search = query.NormalizedSearch;
        if (search.Length > 0)
            rows = rows.Where(row => row.NormalizedTitle.Contains(search));
        rows = query.SortKey("startDate") switch
        {
            ("startDate", true) => rows.OrderByDescending(row => row.StartDate),
            ("title", false) => rows.OrderBy(row => row.NormalizedTitle),
            ("title", true) => rows.OrderByDescending(row => row.NormalizedTitle),
            _ => rows.OrderBy(row => row.StartDate).ThenBy(row => row.NormalizedTitle),
        };
        var mapper = await MapperAsync(token);
        return OperationResult.Success(await store.ToPageAsync(rows, query, mapper, token));
    }

    public async Task<OperationResult<CalendarDayDto>> CreateAsync(SaveCalendarDayCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = new InputErrors();
        var (start, end, kind) = Parse(command, input);
        if (input.Any)
            return input.ToResult<CalendarDayDto>();
        CalendarDay? day = null;
        if (StoreSaving.TryDomain<CalendarDayDto>(() => day = CalendarDay.Create(command.Title, start, end, kind, command.AffectsSchedule)) is { } invalid)
            return invalid;
        store.Add(day!);
        AuditTrail.Record(store, clock, AuditEvents.CalendarDayCreated, "calendar-day", "Calendar day created.");
        var mapper = await MapperAsync(token);
        return await store.SaveAsync(() => mapper(day!), nameof(command.Title), token);
    }

    public async Task<OperationResult<CalendarDayDto>> UpdateAsync(long id, SaveCalendarDayCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = new InputErrors();
        var (start, end, kind) = Parse(command, input);
        if (input.Any)
            return input.ToResult<CalendarDayDto>();
        if (await FindAsync(id, token) is not { } day)
            return OperationResult.Failure<CalendarDayDto>(ErrorCodes.NotFound);
        if (!day.IsVersion(command.Version))
            return OperationResult.Failure<CalendarDayDto>(ErrorCodes.Conflict);
        if (StoreSaving.TryDomain<CalendarDayDto>(() => day.Update(command.Title, start, end, kind, command.AffectsSchedule)) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, AuditEvents.CalendarDayUpdated, $"calendar-day:{id}", "Calendar day updated.");
        var mapper = await MapperAsync(token);
        return await store.SaveAsync(() => mapper(day), nameof(command.Title), token);
    }

    public async Task<OperationResult<bool>> DeleteAsync(long id, int version, CancellationToken token)
    {
        if (await FindAsync(id, token) is not { } day)
            return OperationResult.Failure<bool>(ErrorCodes.NotFound);
        if (!day.IsVersion(version))
            return OperationResult.Failure<bool>(ErrorCodes.Conflict);
        store.Remove(day);
        AuditTrail.Record(store, clock, AuditEvents.CalendarDayDeleted, $"calendar-day:{id}", "Calendar day deleted.");
        return await store.SaveAsync(() => true, "Title", token);
    }

    /// <summary>MF8: enables or disables an entry for the owner's purposes (kept in the list, ignored everywhere else).</summary>
    public async Task<OperationResult<CalendarDayDto>> SetEnabledAsync(long id, SetCalendarDayEnabledCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await FindAsync(id, token) is not { } day)
            return OperationResult.Failure<CalendarDayDto>(ErrorCodes.NotFound);
        if (!day.IsVersion(command.Version))
            return OperationResult.Failure<CalendarDayDto>(ErrorCodes.Conflict);
        day.SetEnabled(command.Enabled);
        AuditTrail.Record(store, clock, AuditEvents.CalendarDayEnabledChanged, $"calendar-day:{id}", "Calendar day enabled or disabled.", new { enabled = command.Enabled });
        var mapper = await MapperAsync(token);
        return await store.SaveAsync(() => mapper(day), nameof(command.Enabled), token);
    }

    /// <summary>MF8: the Iraqi official holidays that fall inside an academic year, and which of them are already in the calendar.</summary>
    public async Task<OperationResult<IraqHolidayPreviewResponse>> PreviewIraqHolidaysAsync(long yearId, CancellationToken token)
    {
        if (await store.FirstOrDefaultAsync(store.Query<AcademicYear>().Where(row => row.Id == yearId), token) is not { } year)
            return OperationResult.Failure<IraqHolidayPreviewResponse>(ErrorCodes.NotFound);
        var placed = await PlaceAsync(year, token);
        return OperationResult.Success(new IraqHolidayPreviewResponse(year.Label, placed.Select(item => new IraqHolidayPreviewDto(
            item.Holiday.Key, item.Holiday.Title, InputParsing.Format(item.Start), InputParsing.Format(item.End), item.Approximate, item.Existing)).ToList()));
    }

    /// <summary>MF8: adds the holidays that are not in the calendar yet; entries already there (even edited or disabled) are kept as they are.</summary>
    public async Task<OperationResult<ImportIraqHolidaysResult>> ImportIraqHolidaysAsync(ImportIraqHolidaysCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FirstOrDefaultAsync(store.Query<AcademicYear>().Where(row => row.Id == command.YearId), token) is not { } year)
            return OperationResult.Failure<ImportIraqHolidaysResult>(ErrorCodes.NotFound);
        var placed = await PlaceAsync(year, token);
        var fresh = placed.Where(item => !item.Existing).ToList();
        foreach (var item in fresh)
            store.Add(CalendarDay.FromTemplate(item.Holiday.Key, item.Holiday.Title, item.Start, item.End, item.Approximate));
        if (fresh.Count > 0)
            AuditTrail.Record(store, clock, AuditEvents.CalendarHolidaysImported, $"academic-year:{year.Id}", $"{fresh.Count} template holidays added.", new { count = fresh.Count });
        var result = new ImportIraqHolidaysResult(fresh.Count, placed.Count - fresh.Count);
        return await store.SaveAsync(() => result, "YearId", token);
    }

    private const int MovedDaysTolerance = 30;

    private sealed record Placement(IraqHoliday Holiday, DateOnly Start, DateOnly End, bool Approximate, bool Existing);

    private async Task<List<Placement>> PlaceAsync(AcademicYear year, CancellationToken token)
    {
        var placed = IraqHolidayTemplate.PlaceIn(year.StartDate, year.EndDate);
        var existing = await store.ListAsync(store.Query<CalendarDay>().Where(row => row.TemplateKey != null && row.EndDate >= year.StartDate.AddDays(-MovedDaysTolerance) && row.StartDate <= year.EndDate.AddDays(MovedDaysTolerance))
            .Select(row => new { row.TemplateKey, row.StartDate }), token);
        // An entry the owner moved by a few days (the announced date differs) still counts as the holiday being present.
        return placed.Select(item => new Placement(item.Holiday, item.Start, item.End, item.Approximate,
            existing.Any(row => row.TemplateKey == item.Holiday.Key && Math.Abs(row.StartDate.DayNumber - item.Start.DayNumber) <= MovedDaysTolerance))).ToList();
    }

    private static (DateOnly Start, DateOnly? End, CalendarDayKind Kind) Parse(SaveCalendarDayCommand command, InputErrors input) =>
        (input.Date(command.StartDate, nameof(command.StartDate)),
         input.OptionalDate(command.EndDate, nameof(command.EndDate)),
         input.Option<CalendarDayKind>(command.Kind, nameof(command.Kind)));

    private Task<CalendarDay?> FindAsync(long id, CancellationToken token) =>
        store.FirstOrDefaultAsync(store.Query<CalendarDay>().Where(row => row.Id == id), token);

    /// <summary>Maps entries, flagging those outside the current year (no current year: no warning).</summary>
    private async Task<Func<CalendarDay, CalendarDayDto>> MapperAsync(CancellationToken token)
    {
        var year = await SchoolContextService.CurrentYearAsync(store, token);
        return row => new CalendarDayDto(
            row.Id,
            row.Title,
            InputParsing.Format(row.StartDate),
            InputParsing.Format(row.EndDate),
            ApiText.ToValue(row.Kind),
            row.AffectsSchedule && row.IsEnabled,
            year is not null && row.IsOutside(year.StartDate, year.EndDate),
            row.Source == CalendarDaySource.IraqTemplate ? "iraqTemplate" : "manual",
            row.IsApproximate,
            row.IsEnabled,
            row.Version);
    }
}
