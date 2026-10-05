using System.Text.Json;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Setup;

public sealed record SuggestedEntryTemplate(string Subject, int Lessons, bool Optional = false);

/// <param name="StatedTotal">The total written in the owner's source; never trusted (totals are computed from rows).</param>
/// <param name="NeedsReview">The stated total does not equal the rows; the owner must confirm (decision #41).</param>
public sealed record SuggestedStageTemplate(
    string Name,
    string Level,
    string? Branch,
    int StatedTotal,
    IReadOnlyList<SuggestedEntryTemplate> Entries,
    int ComputedTotal,
    int ComputedTotalWithoutOptional,
    bool NeedsReview,
    string TotalBasis)
{
    /// <summary>Sum of the rows: all, or without the optional subjects (Kurdish, French).</summary>
    public int Total(bool includeOptional) => Entries.Where(entry => includeOptional || !entry.Optional).Sum(entry => entry.Lessons);
}

/// <summary>
/// The suggested Iraqi curriculum (ADR 0028): weekly lessons per subject and stage, supplied by the owner from a
/// secondary source and NOT verified against an official Ministry document. Embedded data
/// (<c>Templates/iraq-curriculum.suggested.json</c>); every value it fills stays editable and is marked «مقترح».
/// </summary>
public sealed class SuggestedCurriculumTemplate
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<SuggestedCurriculumTemplate> Instance = new(Load);

    private sealed record TemplateFile(
        int TemplateVersion,
        string Provenance,
        IReadOnlyDictionary<string, IReadOnlyList<string>> SubjectAliases,
        IReadOnlyList<SuggestedStageTemplate> Stages,
        IReadOnlyList<string> Notes);

    private readonly Dictionary<string, string> _canonicalByName;

    private SuggestedCurriculumTemplate(TemplateFile file)
    {
        Version = file.TemplateVersion;
        Provenance = file.Provenance;
        Aliases = file.SubjectAliases;
        Stages = file.Stages;
        _canonicalByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in Stages.SelectMany(stage => stage.Entries).Select(entry => entry.Subject).Distinct())
            _canonicalByName[ArabicText.Normalize(name)] = name;
        foreach (var (canonical, aliases) in Aliases)
            foreach (var alias in aliases.Append(canonical))
                _canonicalByName[ArabicText.Normalize(alias)] = canonical;
    }

    public static SuggestedCurriculumTemplate Current => Instance.Value;

    public int Version { get; }
    public string Provenance { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Aliases { get; }
    public IReadOnlyList<SuggestedStageTemplate> Stages { get; }

    /// <summary>The template's spelling for a subject name (aliases and Arabic normalization), or null when unknown.</summary>
    public string? Canonical(string? name) => _canonicalByName.GetValueOrDefault(ArabicText.Normalize(name));

    /// <summary>True when an existing subject name means the template subject (اللغة الإنجليزية = اللغة الإنكليزية).</summary>
    public bool SameSubject(string templateSubject, string existingName) =>
        string.Equals(Canonical(existingName) ?? ArabicText.Normalize(existingName), Canonical(templateSubject), StringComparison.Ordinal);

    /// <summary>The template stage for a school stage: by its template key's name, else by its own (normalized) name.</summary>
    public SuggestedStageTemplate? StageFor(Stage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        var names = new List<string> { stage.Name };
        if (stage.TemplateKey is { } key && TemplateNameFor(key) is { } templateName)
            names.Insert(0, templateName);
        return names.Select(name => Stages.FirstOrDefault(item => ArabicText.Normalize(item.Name) == ArabicText.Normalize(name))).FirstOrDefault(item => item is not null);
    }

    private static string? TemplateNameFor(string key)
    {
        var catalog = TemplateCatalog.Current;
        foreach (var grade in catalog.Grades)
        {
            if (grade.Key == key)
                return grade.Name;
            foreach (var branch in catalog.Branches)
            {
                var (stageKey, name) = TemplateCatalog.Stage(grade, branch);
                if (stageKey == key)
                    return name;
            }
        }
        return null;
    }

    private static SuggestedCurriculumTemplate Load()
    {
        var assembly = typeof(SuggestedCurriculumTemplate).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(item => item.EndsWith(".Templates.iraq-curriculum.suggested.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        return new SuggestedCurriculumTemplate(JsonSerializer.Deserialize<TemplateFile>(stream, Json)
            ?? throw new InvalidOperationException("The suggested curriculum template is empty."));
    }
}
