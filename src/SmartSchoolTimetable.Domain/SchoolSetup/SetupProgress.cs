using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>
/// Progress of the setup wizard (spec 2.5 §3.5), exactly one row (Id = 1), so the wizard can be resumed. The
/// choices themselves (school type, shift mode) live on <see cref="SchoolProfile"/>; this row records which steps
/// are done or skipped, the step to resume at, and whether the owner finished the wizard.
/// </summary>
public sealed class SetupProgress : VersionedEntity
{
    public const long SingletonId = 1;
    public const int FirstStep = 1;
    public const int LastStep = 8;

    private SetupProgress()
    {
    }

    public int CurrentStep { get; private set; } = FirstStep;
    public int CompletedMask { get; private set; }
    public int SkippedMask { get; private set; }
    public bool IsFinished { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<int> CompletedSteps => StepsIn(CompletedMask);
    public IReadOnlyList<int> SkippedSteps => StepsIn(SkippedMask);

    public static SetupProgress CreateDefault(DateTimeOffset now) => new() { Id = SingletonId, UpdatedAt = now };

    public void Record(int currentStep, IReadOnlyCollection<int>? completed, IReadOnlyCollection<int>? skipped, bool finished, DateTimeOffset now)
    {
        completed ??= [];
        skipped ??= [];
        new DomainErrors()
            .When(!IsStep(currentStep), nameof(CurrentStep), DomainErrorCode.OutOfRange)
            .When(completed.Any(step => !IsStep(step)), "CompletedSteps", DomainErrorCode.OutOfRange)
            .When(skipped.Any(step => !IsStep(step)), "SkippedSteps", DomainErrorCode.OutOfRange)
            .ThrowIfAny();
        CurrentStep = currentStep;
        CompletedMask = Mask(completed);
        // A step that is done is no longer "skipped".
        SkippedMask = Mask(skipped) & ~CompletedMask;
        IsFinished = finished;
        UpdatedAt = now;
        Touch();
    }

    private static bool IsStep(int step) => step is >= FirstStep and <= LastStep;

    private static int Mask(IEnumerable<int> steps) => steps.Aggregate(0, (mask, step) => mask | (1 << step));

    private static int[] StepsIn(int mask) =>
        Enumerable.Range(FirstStep, LastStep).Where(step => (mask & (1 << step)) != 0).ToArray();
}
