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

    [LoggerMessage(EventId = 1101, Level = LogLevel.Error, Message = "Timetable generation run {RunId} failed.")]
    public static partial void GenerationFailed(ILogger logger, long runId, Exception exception);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Warning, Message = "{Count} generation runs left active by the previous start were marked interrupted.")]
    public static partial void GenerationsInterrupted(ILogger logger, int count);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "An automatic backup was created; {Pruned} old automatic backups were removed.")]
    public static partial void AutoBackupCreated(ILogger logger, int pruned);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Warning, Message = "The automatic backup could not be written; the next check tries again.")]
    public static partial void AutoBackupFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Warning, Message = "The solver engine did not load; generation is unavailable.")]
    public static partial void SolverUnavailable(ILogger logger);
}
