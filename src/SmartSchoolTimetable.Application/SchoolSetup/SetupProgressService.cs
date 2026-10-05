using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.SchoolSetup;

/// <param name="SchoolType">The choice made in step 1, read from the school profile.</param>
/// <param name="ShiftMode">The shift mode (the profile's study type).</param>
public sealed record SetupProgressDto(
    int CurrentStep,
    IReadOnlyList<int> CompletedSteps,
    IReadOnlyList<int> SkippedSteps,
    bool IsFinished,
    string SchoolType,
    string ShiftMode,
    int Version);

public sealed record SaveSetupProgressCommand(int CurrentStep, IReadOnlyList<int>? CompletedSteps, IReadOnlyList<int>? SkippedSteps, bool IsFinished, int Version);

/// <summary>Resumable setup wizard progress (spec 2.5 §3.5): one row, versioned, audited.</summary>
public sealed class SetupProgressService(IDataStore store, TimeProvider clock)
{
    public async Task<SetupProgressDto> GetAsync(CancellationToken token)
    {
        var (progress, profile) = await LoadAsync(token);
        return ToDto(progress, profile);
    }

    public async Task<OperationResult<SetupProgressDto>> SaveAsync(SaveSetupProgressCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var (progress, profile) = await LoadAsync(token);
        if (!progress.IsVersion(command.Version))
            return OperationResult.Failure<SetupProgressDto>(ErrorCodes.Conflict);
        if (StoreSaving.TryDomain<SetupProgressDto>(() =>
                progress.Record(command.CurrentStep, command.CompletedSteps, command.SkippedSteps, command.IsFinished, clock.GetUtcNow())) is { } invalid)
            return invalid;
        AuditTrail.Record(store, clock, command.IsFinished ? "SetupFinished" : "SetupProgressSaved", "setup", $"Setup step {command.CurrentStep}.");
        return await store.SaveAsync(() => ToDto(progress, profile), "CurrentStep", token);
    }

    private async Task<(SetupProgress Progress, SchoolProfile Profile)> LoadAsync(CancellationToken token)
    {
        var progress = await store.FirstOrDefaultAsync(store.Query<SetupProgress>(), token)
            ?? throw new InvalidOperationException("The setup progress row is created at database initialization.");
        var profile = await store.FirstOrDefaultAsync(store.Query<SchoolProfile>(), token)
            ?? throw new InvalidOperationException("The school profile row is created at database initialization.");
        return (progress, profile);
    }

    private static SetupProgressDto ToDto(SetupProgress progress, SchoolProfile profile) => new(
        progress.CurrentStep,
        progress.CompletedSteps,
        progress.SkippedSteps,
        progress.IsFinished,
        ApiText.ToValue(profile.SchoolType),
        ApiText.ToValue(profile.StudyType),
        progress.Version);
}
