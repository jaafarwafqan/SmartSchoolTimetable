using System.Text.Json;
using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.Setup;

public sealed record BranchTemplate(string Key, string Name);

/// <param name="BranchStem">Set for grades taught in branches: the stage is "{stem} {branch}" (e.g. "الرابع العلمي").</param>
public sealed record GradeTemplate(string Key, string Name, string? BranchStem, IReadOnlyList<string> SchoolTypes);

public sealed record PeriodPresetTemplate(string Key, string Name, string FirstStart, int LessonMinutes, int LessonCount, IReadOnlyList<BreakSlotDto> Breaks);

public sealed record WorkingDayPresetTemplate(string Key, string Name, IReadOnlyList<int> Days, int WeekStart, bool IsDefault);

/// <summary>Suggested break length per school type (minutes); a suggestion, never an official number.</summary>
public sealed record BreakDefaultsTemplate(IReadOnlyDictionary<string, int> Minutes);

public sealed record TemplateCatalogDto(
    IReadOnlyList<BranchTemplate> Branches,
    IReadOnlyList<GradeTemplate> Grades,
    IReadOnlyList<PeriodPresetTemplate> PeriodPresets,
    IReadOnlyList<WorkingDayPresetTemplate> WorkingDayPresets,
    BreakDefaultsTemplate BreakDefaults);

/// <summary>
/// Data-driven setup templates (spec 2.5 §4.1), embedded JSON resources: Iraqi stages per school type with
/// branches, period and working-day presets. Suggested subjects come from the official curriculum template only
/// (<see cref="SuggestedCurriculumTemplate.MandatorySubjects"/>).
/// </summary>
public sealed class TemplateCatalog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<TemplateCatalog> Instance = new(Load);

    private sealed record StagesFile(IReadOnlyList<BranchTemplate> Branches, IReadOnlyList<GradeTemplate> Grades);
    private sealed record PresetsFile(IReadOnlyList<PeriodPresetTemplate> Periods, IReadOnlyList<WorkingDayPresetTemplate> WorkingDays, BreakDefaultsTemplate BreakDefaults);

    private TemplateCatalog(StagesFile stages, PresetsFile presets)
    {
        Branches = stages.Branches;
        Grades = stages.Grades;
        PeriodPresets = presets.Periods;
        WorkingDayPresets = presets.WorkingDays;
        BreakDefaults = presets.BreakDefaults;
    }

    public static TemplateCatalog Current => Instance.Value;

    public IReadOnlyList<BranchTemplate> Branches { get; }
    public IReadOnlyList<GradeTemplate> Grades { get; }
    public IReadOnlyList<PeriodPresetTemplate> PeriodPresets { get; }
    public IReadOnlyList<WorkingDayPresetTemplate> WorkingDayPresets { get; }
    public BreakDefaultsTemplate BreakDefaults { get; }

    public TemplateCatalogDto ToDto() => new(Branches, Grades, PeriodPresets, WorkingDayPresets, BreakDefaults);

    /// <summary>Grades of a school type in display order (ثانوية = متوسطة + إعدادية).</summary>
    public IReadOnlyList<GradeTemplate> GradesFor(SchoolType schoolType)
    {
        var type = ApiText.ToValue(schoolType);
        return Grades.Where(grade => grade.SchoolTypes.Contains(type)).ToArray();
    }

    public IReadOnlySet<string> StageKeysFor(SchoolType schoolType)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grade in GradesFor(schoolType))
        {
            if (grade.BranchStem is null)
            {
                keys.Add(grade.Key);
                continue;
            }

            foreach (var branch in Branches)
                keys.Add(Stage(grade, branch).Key);
        }
        return keys;
    }

    public GradeTemplate? Grade(string? key) => Grades.FirstOrDefault(grade => grade.Key == key);

    public BranchTemplate? Branch(string? key) => Branches.FirstOrDefault(branch => branch.Key == key);

    /// <summary>Stage key and name for a grade, optionally in a branch (only for grades with a branch stem).</summary>
    public static (string Key, string Name) Stage(GradeTemplate grade, BranchTemplate? branch)
    {
        ArgumentNullException.ThrowIfNull(grade);
        return branch is null || grade.BranchStem is null
            ? (grade.Key, grade.Name)
            : ($"{grade.Key}-{branch.Key}", $"{grade.BranchStem} {branch.Name}");
    }

    private static TemplateCatalog Load() =>
        new(Read<StagesFile>("stages.json"), Read<PresetsFile>("presets.json"));

    private static T Read<T>(string name)
    {
        var assembly = typeof(TemplateCatalog).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(item => item.EndsWith($".Setup.Templates.{name}", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        return JsonSerializer.Deserialize<T>(stream, Json) ?? throw new InvalidOperationException($"Template {name} is empty.");
    }
}
