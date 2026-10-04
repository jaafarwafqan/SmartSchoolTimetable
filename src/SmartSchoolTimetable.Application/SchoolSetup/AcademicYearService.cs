using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.SchoolSetup;

/// <summary>Academic years and terms. Exactly one current year; the current term belongs to the current year.</summary>
public sealed class AcademicYearService(IDataStore store, TimeProvider clock, IYearStructure structure)
{
    public async Task<PagedResult<AcademicYearDto>> ListAsync(ListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var years = store.Query<AcademicYear>();
        var search = query.NormalizedSearch;
        if (search.Length > 0)
            years = years.Where(year => year.NormalizedLabel.Contains(search));
        years = query.SortKey("-startDate") switch
        {
            ("label", false) => years.OrderBy(year => year.NormalizedLabel),
            ("label", true) => years.OrderByDescending(year => year.NormalizedLabel),
            ("startDate", false) => years.OrderBy(year => year.StartDate),
            _ => years.OrderByDescending(year => year.StartDate),
        };
        return await store.ToPageAsync(years, query, ToDto, cancellationToken);
    }

    public async Task<OperationResult<AcademicYearDto>> GetAsync(long id, CancellationToken cancellationToken) =>
        await FindAsync(id, cancellationToken) is { } year
            ? OperationResult.Success<AcademicYearDto>(ToDto(year))
            : OperationResult.Failure<AcademicYearDto>(ErrorCodes.NotFound);

    public async Task<OperationResult<AcademicYearDto>> CreateAsync(SaveAcademicYearCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = new InputErrors();
        var start = input.Date(command.StartDate, nameof(command.StartDate));
        var end = input.Date(command.EndDate, nameof(command.EndDate));
        if (input.Any)
            return input.ToResult<AcademicYearDto>();
        if (await LabelTakenAsync(command.Label, null, cancellationToken))
            return OperationResult.Invalid<AcademicYearDto>(nameof(command.Label), ErrorCodes.DuplicateName);

        AcademicYear? source = null;
        if (command.CopyStructureFromYearId is { } sourceId && (source = await FindAsync(sourceId, cancellationToken)) is null)
            return OperationResult.Invalid<AcademicYearDto>(nameof(command.CopyStructureFromYearId), ErrorCodes.InvalidOption);

        AcademicYear year;
        try
        {
            year = AcademicYear.Create(command.Label, start, end);
        }
        catch (DomainValidationException exception)
        {
            return OperationResult.FromDomain<AcademicYearDto>(exception);
        }

        var hasCurrent = await store.AnyAsync(store.Query<AcademicYear>().Where(existing => existing.IsCurrent), cancellationToken);
        year.MarkCurrent(!hasCurrent);
        store.Add(year);
        AuditTrail.Record(store, clock, "AcademicYearCreated", "academic-year", "Academic year created.");
        var saved = await SaveAsync(year, cancellationToken);
        if (saved.Succeeded && source is not null)
        {
            await structure.CopyAsync(source.Id, year.Id, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
        }
        return saved;
    }

    public async Task<OperationResult<AcademicYearDto>> UpdateAsync(long id, SaveAcademicYearCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = new InputErrors();
        var start = input.Date(command.StartDate, nameof(command.StartDate));
        var end = input.Date(command.EndDate, nameof(command.EndDate));
        if (input.Any)
            return input.ToResult<AcademicYearDto>();
        if (await FindAsync(id, cancellationToken) is not { } year)
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.NotFound);
        if (!year.IsVersion(command.Version))
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.Conflict);
        if (await LabelTakenAsync(command.Label, id, cancellationToken))
            return OperationResult.Invalid<AcademicYearDto>(nameof(command.Label), ErrorCodes.DuplicateName);

        return await MutateAsync(year, current => current.Update(command.Label, start, end), "AcademicYearUpdated", cancellationToken);
    }

    public async Task<OperationResult<bool>> DeleteAsync(long id, int version, CancellationToken cancellationToken)
    {
        if (await FindAsync(id, cancellationToken) is not { } year)
            return OperationResult.Failure<bool>(ErrorCodes.NotFound);
        if (!year.IsVersion(version))
            return OperationResult.Failure<bool>(ErrorCodes.Conflict);
        if (await structure.DeletionBlockAsync(id, cancellationToken) is { } blocker)
            return OperationResult.Failure<bool>(blocker);
        // Deleting the current year while other years exist would leave the school without a current year.
        if (year.IsCurrent && await store.AnyAsync(store.Query<AcademicYear>().Where(other => other.Id != id), cancellationToken))
            return OperationResult.Failure<bool>(ErrorCodes.CurrentYearRequired);

        store.Remove(year);
        AuditTrail.Record(store, clock, "AcademicYearDeleted", $"academic-year:{id}", "Academic year deleted.");
        try
        {
            await store.SaveChangesAsync(cancellationToken);
            return OperationResult.Success<bool>(true);
        }
        catch (ConcurrencyConflictException)
        {
            return OperationResult.Failure<bool>(ErrorCodes.Conflict);
        }
    }

    public async Task<OperationResult<AcademicYearDto>> SetCurrentAsync(long id, int version, CancellationToken cancellationToken)
    {
        if (await FindAsync(id, cancellationToken) is not { } year)
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.NotFound);
        if (!year.IsVersion(version))
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.Conflict);
        return await MakeCurrentAsync(year, _ => { }, "AcademicYearMadeCurrent", cancellationToken);
    }

    public async Task<OperationResult<AcademicYearDto>> AddTermAsync(long yearId, SaveTermCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await TermOperationAsync(yearId, command.Version, command.StartDate, command.EndDate,
            (year, start, end) => year.AddTerm(command.Name, start, end), "TermCreated", cancellationToken);
    }

    public async Task<OperationResult<AcademicYearDto>> UpdateTermAsync(long yearId, long termId, SaveTermCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await TermOperationAsync(yearId, command.Version, command.StartDate, command.EndDate, (year, start, end) =>
        {
            if (!year.HasTerm(termId))
                throw new KeyNotFoundException();
            year.UpdateTerm(termId, command.Name, start, end);
        }, "TermUpdated", cancellationToken);
    }

    public async Task<OperationResult<AcademicYearDto>> DeleteTermAsync(long yearId, long termId, int version, CancellationToken cancellationToken)
    {
        if (await FindAsync(yearId, cancellationToken) is not { } year || !year.HasTerm(termId))
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.NotFound);
        if (!year.IsVersion(version))
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.Conflict);
        return await MutateAsync(year, current => current.RemoveTerm(termId), "TermDeleted", cancellationToken);
    }

    public async Task<OperationResult<AcademicYearDto>> SetCurrentTermAsync(long yearId, long termId, int version, CancellationToken cancellationToken)
    {
        if (await FindAsync(yearId, cancellationToken) is not { } year || !year.HasTerm(termId))
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.NotFound);
        if (!year.IsVersion(version))
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.Conflict);
        return await MakeCurrentAsync(year, current => current.SetCurrentTerm(termId), "TermMadeCurrent", cancellationToken);
    }

    private async Task<OperationResult<AcademicYearDto>> TermOperationAsync(
        long yearId,
        int version,
        string? startText,
        string? endText,
        Action<AcademicYear, DateOnly, DateOnly> operation,
        string eventType,
        CancellationToken cancellationToken)
    {
        var input = new InputErrors();
        var start = input.Date(startText, "StartDate");
        var end = input.Date(endText, "EndDate");
        if (input.Any)
            return input.ToResult<AcademicYearDto>();
        if (await FindAsync(yearId, cancellationToken) is not { } year)
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.NotFound);
        if (!year.IsVersion(version))
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.Conflict);
        try
        {
            return await MutateAsync(year, current => operation(current, start, end), eventType, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.NotFound);
        }
    }

    private async Task<OperationResult<AcademicYearDto>> MutateAsync(
        AcademicYear year,
        Action<AcademicYear> mutation,
        string eventType,
        CancellationToken cancellationToken)
    {
        try
        {
            mutation(year);
        }
        catch (DomainValidationException exception)
        {
            return OperationResult.FromDomain<AcademicYearDto>(exception);
        }
        AuditTrail.Record(store, clock, eventType, $"academic-year:{year.Id}", "Academic year structure changed.");
        return await SaveAsync(year, cancellationToken);
    }

    /// <summary>
    /// Makes the year the only current one. The previous current year is cleared and saved first, then the
    /// new one is marked, inside one transaction, so the database "one current year" index is never violated.
    /// </summary>
    private async Task<OperationResult<AcademicYearDto>> MakeCurrentAsync(
        AcademicYear year,
        Action<AcademicYear> alsoApply,
        string eventType,
        CancellationToken cancellationToken)
    {
        try
        {
            await store.ExecuteInTransactionAsync(async () =>
            {
                var others = await store.ListAsync(
                    store.Query<AcademicYear>().Where(other => other.IsCurrent && other.Id != year.Id),
                    cancellationToken);
                foreach (var other in others)
                    other.MarkCurrent(false);
                if (others.Count > 0)
                    await store.SaveChangesAsync(cancellationToken);
                year.MarkCurrent(true);
                alsoApply(year);
                AuditTrail.Record(store, clock, eventType, $"academic-year:{year.Id}", "Current academic year or term changed.");
                await store.SaveChangesAsync(cancellationToken);
            }, cancellationToken);
            return OperationResult.Success(ToDto(year));
        }
        catch (ConcurrencyConflictException)
        {
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.Conflict);
        }
    }

    private async Task<OperationResult<AcademicYearDto>> SaveAsync(AcademicYear year, CancellationToken cancellationToken)
    {
        try
        {
            await store.SaveChangesAsync(cancellationToken);
            return OperationResult.Success<AcademicYearDto>(ToDto(year));
        }
        catch (ConcurrencyConflictException)
        {
            return OperationResult.Failure<AcademicYearDto>(ErrorCodes.Conflict);
        }
        catch (DataConflictException)
        {
            return OperationResult.Invalid<AcademicYearDto>("Label", ErrorCodes.DuplicateName);
        }
    }

    private Task<AcademicYear?> FindAsync(long id, CancellationToken cancellationToken) =>
        store.FirstOrDefaultAsync(store.Query<AcademicYear>().Where(year => year.Id == id), cancellationToken);

    private async Task<bool> LabelTakenAsync(string? label, long? exceptId, CancellationToken cancellationToken)
    {
        var normalized = ArabicText.Normalize(label);
        return normalized.Length > 0 && await store.AnyAsync(
            store.Query<AcademicYear>().Where(year => year.NormalizedLabel == normalized && year.Id != exceptId),
            cancellationToken);
    }

    internal static AcademicYearDto ToDto(AcademicYear year) => new(
        year.Id,
        year.Label,
        InputParsing.Format(year.StartDate),
        InputParsing.Format(year.EndDate),
        year.IsCurrent,
        year.Terms
            .OrderBy(term => term.StartDate)
            .Select(term => new TermDto(
                term.Id,
                term.Name,
                InputParsing.Format(term.StartDate),
                InputParsing.Format(term.EndDate),
                year.CurrentTermId == term.Id))
            .ToArray(),
        year.Version);
}

/// <summary>Year-scoped structure (shifts, periods, stages, sections), implemented by later checkpoints.</summary>
public interface IYearStructure
{
    Task<bool> HasStructureAsync(long yearId, CancellationToken cancellationToken);

    async Task<string?> DeletionBlockAsync(long yearId, CancellationToken cancellationToken) =>
        await HasStructureAsync(yearId, cancellationToken) ? ErrorCodes.RecordInUse : null;

    /// <summary>Copies the structure of one year into another (never calendar days). Saved by the caller.</summary>
    Task CopyAsync(long sourceYearId, long targetYearId, CancellationToken cancellationToken);
}
