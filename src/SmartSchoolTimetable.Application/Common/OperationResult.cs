using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Application.Common;

public sealed record FieldError(string Field, string Code);

/// <summary>
/// Outcome of an application operation: a value, a top-level error code (NOT_FOUND, CONFLICT, ...),
/// or field-level validation errors (returned as VALIDATION_FAILED with the field list).
/// </summary>
public sealed class OperationResult<T>
{
    internal OperationResult(T? value, string? errorCode, IReadOnlyList<FieldError> fieldErrors)
    {
        Value = value;
        ErrorCode = errorCode;
        FieldErrors = fieldErrors;
    }

    public T? Value { get; }
    public string? ErrorCode { get; }
    public IReadOnlyList<FieldError> FieldErrors { get; }
    public bool Succeeded => ErrorCode is null;

    /// <summary>Re-types a failure (its code and field errors) for another result type.</summary>
    public OperationResult<TOther> Cast<TOther>() =>
        Succeeded
            ? throw new InvalidOperationException("Only failures can be cast.")
            : new OperationResult<TOther>(default, ErrorCode, FieldErrors);
}

/// <summary>Factory methods for <see cref="OperationResult{T}"/>.</summary>
public static class OperationResult
{
    public static OperationResult<T> Success<T>(T value) => new(value, null, []);

    public static OperationResult<T> Failure<T>(string errorCode) => new(default, errorCode, []);

    public static OperationResult<T> Invalid<T>(IReadOnlyList<FieldError> errors) =>
        new(default, ErrorCodes.ValidationFailed, errors);

    public static OperationResult<T> Invalid<T>(string field, string code) => Invalid<T>([new FieldError(field, code)]);

    public static OperationResult<T> FromDomain<T>(DomainValidationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Invalid<T>(exception.Errors
            .Select(error => new FieldError(error.Field, DomainErrorMapping.ToErrorCode(error.Code)))
            .ToArray());
    }
}

/// <summary>Collects field errors during input parsing (formats, required values) before domain rules run.</summary>
public sealed class InputErrors
{
    private readonly List<FieldError> _errors = [];

    public bool Any => _errors.Count > 0;
    public IReadOnlyList<FieldError> Errors => _errors;

    public void Add(string field, string code) => _errors.Add(new FieldError(field, code));

    public DateOnly Date(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(field, ErrorCodes.Required);
            return default;
        }
        if (InputParsing.TryParseDate(value, out var date))
            return date;
        Add(field, ErrorCodes.InvalidDate);
        return default;
    }

    public DateOnly? OptionalDate(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (InputParsing.TryParseDate(value, out var date))
            return date;
        Add(field, ErrorCodes.InvalidDate);
        return null;
    }

    public TimeOnly Time(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(field, ErrorCodes.Required);
            return default;
        }
        if (InputParsing.TryParseTime(value, out var time))
            return time;
        Add(field, ErrorCodes.InvalidTime);
        return default;
    }

    public TEnum Option<TEnum>(string? value, string field) where TEnum : struct, Enum
    {
        if (ApiText.TryParse<TEnum>(value, out var parsed))
            return parsed;
        Add(field, string.IsNullOrWhiteSpace(value) ? ErrorCodes.Required : ErrorCodes.InvalidOption);
        return default;
    }

    public OperationResult<T> ToResult<T>() => OperationResult.Invalid<T>(_errors.ToArray());
}
