namespace SmartSchoolTimetable.Api;

/// <summary>Source-generated log messages. Messages must never include secrets, cookies or tokens.</summary>
internal static partial class LocalLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Rejected request with non-canonical local Host header.")]
    public static partial void RejectedHost(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Rejected request with non-canonical Origin header.")]
    public static partial void RejectedOrigin(ILogger logger);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "Unhandled API failure. CorrelationId: {CorrelationId}")]
    public static partial void UnhandledApiFailure(ILogger logger, string correlationId);
}
