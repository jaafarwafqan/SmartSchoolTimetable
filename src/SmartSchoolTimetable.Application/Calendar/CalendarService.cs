using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.Calendar;

namespace SmartSchoolTimetable.Application.Calendar;

/// <param name="OutsideCurrentYear">Warning only: some day lies outside the current academic year.</param>
public sealed record CalendarDayDto(long Id, string Title, string StartDate, string EndDate, string Kind, bool AffectsSchedule, bool OutsideCurrentYear, int Version);

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
        AuditTrail.Record(store, clock, "CalendarDayCreated", "calendar-day", "Calendar day created.");
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
        AuditTrail.Record(store, clock, "CalendarDayUpdated", $"calendar-day:{id}", "Calendar day updated.");
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
        AuditTrail.Record(store, clock, "CalendarDayDeleted", $"calendar-day:{id}", "Calendar day deleted.");
        return await store.SaveAsync(() => true, "Title", token);
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
            row.AffectsSchedule,
            year is not null && row.IsOutside(year.StartDate, year.EndDate),
            row.Version);
    }
}
