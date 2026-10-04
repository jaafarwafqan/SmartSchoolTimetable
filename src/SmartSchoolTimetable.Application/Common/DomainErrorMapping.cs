using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Application.Common;

/// <summary>Maps Domain rule violations to stable API error codes. Every enum value must be mapped (tested).</summary>
public static class DomainErrorMapping
{
    public static string ToErrorCode(DomainErrorCode code) => code switch
    {
        DomainErrorCode.Required => ErrorCodes.Required,
        DomainErrorCode.TooLong => ErrorCodes.ValueTooLong,
        DomainErrorCode.OutOfRange => ErrorCodes.ValueOutOfRange,
        DomainErrorCode.InvalidOption => ErrorCodes.InvalidOption,
        DomainErrorCode.InvalidDateRange => ErrorCodes.InvalidDateRange,
        DomainErrorCode.Duplicate => ErrorCodes.DuplicateName,
        DomainErrorCode.TermOutsideYear => ErrorCodes.TermOutsideYear,
        DomainErrorCode.TermsOverlap => ErrorCodes.TermsOverlap,
        DomainErrorCode.InvalidTimeRange => ErrorCodes.InvalidTimeRange,
        DomainErrorCode.PeriodsOverlap => ErrorCodes.PeriodsOverlap,
        DomainErrorCode.PeriodsNotAscending => ErrorCodes.PeriodsNotAscending,
        DomainErrorCode.NoLessonPeriods => ErrorCodes.NoLessonPeriods,
        DomainErrorCode.TooManyPeriods => ErrorCodes.TooManyPeriods,
        DomainErrorCode.NoWorkingDays => ErrorCodes.NoWorkingDays,
        DomainErrorCode.BlockedPeriodInvalid => ErrorCodes.BlockedPeriodInvalid,
        DomainErrorCode.MaxPerDayExceedsPeriods => ErrorCodes.MaxPerDayExceedsPeriods,
        DomainErrorCode.MaxPerWeekExceedsCapacity => ErrorCodes.MaxPerWeekExceedsCapacity,
        DomainErrorCode.ShiftNotInYear => ErrorCodes.ShiftNotInYear,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unmapped domain error code."),
    };
}
