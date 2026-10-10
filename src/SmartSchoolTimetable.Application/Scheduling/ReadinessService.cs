using SmartSchoolTimetable.Application.Common;

namespace SmartSchoolTimetable.Application.Scheduling;

/// <param name="InputHash">The hash of the input that was checked (shown small on the screen).</param>
/// <param name="Lines">Section × line cells to schedule; <paramref name="Assigned"/> of them have a teacher.</param>
public sealed record ReadinessDto(
    bool Ready,
    int Errors,
    int Warnings,
    IReadOnlyList<ValidationFinding> Findings,
    string InputHash,
    DateTimeOffset CheckedAt,
    int Sections,
    int Lines,
    int Assigned,
    int Teachers);

/// <summary>«جاهزية الجدولة» (Phase 3 §3): builds the scheduling input, hashes it and runs the pre-solve validator.</summary>
public sealed class ReadinessService(IDataStore store, TimeProvider clock)
{
    public async Task<OperationResult<ReadinessDto>> CheckAsync(long yearId, CancellationToken token, ValidatorOptions? options = null)
    {
        if (await SchedulingInputBuilder.BuildAsync(store, yearId, token) is not { } input)
            return OperationResult.Failure<ReadinessDto>(ErrorCodes.NotFound);
        return OperationResult.Success(Check(input, clock.GetUtcNow(), options));
    }

    public static ReadinessDto Check(SchedulingInput input, DateTimeOffset now, ValidatorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var report = PreSolveValidator.Validate(input, options);
        var cells = input.Sections.Sum(section => input.Lines.Count(line => line.StageId == section.StageId));
        return new ReadinessDto(report.Ready, report.Errors, report.Warnings, report.Findings, SchedulingInputHash.Compute(input), now,
            input.Sections.Count, cells, input.Assignments.Count, input.Teachers.Count(teacher => !teacher.IsArchived));
    }
}
