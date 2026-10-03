using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>Built-in tones synthesised by the browser (Web Audio API); no audio files are shipped (ADR 0018).</summary>
public enum BellTone { Classic, Chime, Beeps, Soft }

/// <summary>
/// Bell configuration (exactly one row, Id = 1): the tone and whether breaks ring. Per-lesson start/end flags
/// live on each <see cref="LessonPeriod"/>. Live ringing is Phase 7; Phase 2 stores settings and previews tones.
/// </summary>
public sealed class BellSettings : VersionedEntity
{
    public const long SingletonId = 1;

    private BellSettings()
    {
    }

    public BellTone Tone { get; private set; } = BellTone.Classic;
    public bool BreakBell { get; private set; } = true;

    public static BellSettings CreateDefault() => new() { Id = SingletonId };

    public void Update(BellTone tone, bool breakBell)
    {
        new DomainErrors().When(!Enum.IsDefined(tone), nameof(Tone), DomainErrorCode.InvalidOption).ThrowIfAny();
        Tone = tone;
        BreakBell = breakBell;
        Touch();
    }
}
