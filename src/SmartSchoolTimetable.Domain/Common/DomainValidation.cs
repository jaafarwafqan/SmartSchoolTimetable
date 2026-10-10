namespace SmartSchoolTimetable.Domain.Common;

/// <summary>
/// Business-rule violations raised by the Domain. Application maps each value to a stable API error code
/// (see Application/Common/DomainErrorMapping), so the Domain never contains API code strings.
/// </summary>
public enum DomainErrorCode
{
    Required,
    TooLong,
    OutOfRange,
    InvalidOption,
    InvalidDateRange,
    Duplicate,
    TermOutsideYear,
    TermsOverlap,
    InvalidTimeRange,
    PeriodsOverlap,
    PeriodsNotAscending,
    NoLessonPeriods,
    TooManyPeriods,
    NoWorkingDays,
    BlockedPeriodInvalid,
    MaxPerDayExceedsPeriods,
    MaxPerWeekExceedsCapacity,
    ShiftNotInYear,
    SessionLessonCountMismatch,
}

public sealed record DomainError(string Field, DomainErrorCode Code);

public sealed class DomainValidationException : Exception
{
    public DomainValidationException()
        : this([])
    {
    }

    public DomainValidationException(string message)
        : base(message)
    {
        Errors = [];
    }

    public DomainValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [];
    }

    public DomainValidationException(IReadOnlyList<DomainError> errors)
        : base("Domain validation failed.")
    {
        Errors = errors;
    }

    public DomainValidationException(string field, DomainErrorCode code)
        : this([new DomainError(field, code)])
    {
    }

    public IReadOnlyList<DomainError> Errors { get; }
}

/// <summary>Collects every violation of an operation, then throws once so the UI can show all fields.</summary>
public sealed class DomainErrors
{
    private readonly List<DomainError> _errors = [];

    public bool Any => _errors.Count > 0;

    public IReadOnlyList<DomainError> Errors => _errors;

    public DomainErrors Add(string field, DomainErrorCode code)
    {
        _errors.Add(new DomainError(field, code));
        return this;
    }

    public DomainErrors When(bool condition, string field, DomainErrorCode code) =>
        condition ? Add(field, code) : this;

    /// <summary>Validates a required text value and its maximum length.</summary>
    public DomainErrors Text(string? value, string field, int maxLength, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(value))
            return required ? Add(field, DomainErrorCode.Required) : this;
        return When(value.Trim().Length > maxLength, field, DomainErrorCode.TooLong);
    }

    public void ThrowIfAny()
    {
        if (_errors.Count > 0)
            throw new DomainValidationException(_errors.ToArray());
    }
}
