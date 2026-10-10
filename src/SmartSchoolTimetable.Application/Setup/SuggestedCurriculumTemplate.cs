using System.Text.Json;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Setup;

/// <param name="Optional">Taught only in the schools that offer it (Kurdish, French, computing, حزب البعث); off by default.</param>
/// <param name="InStatedTotal">For an optional subject: counted in the official total (Kurdish) or added on top of it
/// (French, computing, حزب البعث). Mandatory subjects always count.</param>
/// <param name="Note">The source's remark on the row (Arabic data from the template).</param>
public sealed record SuggestedEntryTemplate(string Subject, int Lessons, bool Optional = false, bool? InStatedTotal = null, string? Note = null)
{
    public bool CountsInStatedTotal => !Optional || InStatedTotal == true;
}

/// <param name="StatedTotal">The total printed in the official plan.</param>
/// <param name="ComputedTotal">Sum of the rows that count in the official total (mandatory + Kurdish).</param>
/// <param name="NeedsReview">The stage carries a question for the owner (<paramref name="VerificationNote"/>).</param>
public sealed record SuggestedStageTemplate(
    string Name,
    string Level,
    string? Branch,
    int StatedTotal,
    IReadOnlyList<SuggestedEntryTemplate> Entries,
    int ComputedTotal,
    bool TotalMatchesPrinted,
    bool NeedsReview,
    string? VerificationNote = null)
{
    /// <summary>Sum of the rows counted in the official total (mandatory subjects and Kurdish).</summary>
    public int OfficialTotal() => Entries.Where(entry => entry.CountsInStatedTotal).Sum(entry => entry.Lessons);

    /// <summary>Sum of the mandatory rows and the chosen optional ones (canonical names).</summary>
    public int Total(IReadOnlySet<string> chosenOptional) =>
        Entries.Where(entry => !entry.Optional || chosenOptional.Contains(entry.Subject)).Sum(entry => entry.Lessons);
}

/// <param name="Status">official: the Ministry's published study plan.</param>
public sealed record CurriculumProvenance(string Source, string Status, string? TranscribedBy);

/// <summary>
/// The official Iraqi study plan 2026-2027 (Ministry of Education, regulation 22 of 2011; ADR 0028): weekly lessons
/// per subject and stage, transcribed from the owner's official images. Embedded data
/// (<c>Templates/iraq-curriculum.official-2026-2027.json</c>, template version 2); every value it fills stays editable
/// and is marked «مقترح» until the owner edits it.
/// </summary>
public sealed class SuggestedCurriculumTemplate
{
    public const string ResourceName = "iraq-curriculum.official-2026-2027.json";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<SuggestedCurriculumTemplate> Instance = new(Load);

    private sealed record TemplateFile(
        int TemplateVersion,
        CurriculumProvenance Provenance,
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
        Notes = file.Notes;
        _canonicalByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in Stages.SelectMany(stage => stage.Entries).Select(entry => entry.Subject).Distinct())
            _canonicalByName[ArabicText.Normalize(name)] = name;
        foreach (var (canonical, aliases) in Aliases)
            foreach (var alias in aliases.Append(canonical))
                _canonicalByName[ArabicText.Normalize(alias)] = canonical;
    }

    public static SuggestedCurriculumTemplate Current => Instance.Value;

    public int Version { get; }
    public CurriculumProvenance Provenance { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Aliases { get; }
    public IReadOnlyList<SuggestedStageTemplate> Stages { get; }
    public IReadOnlyList<string> Notes { get; }

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

    /// <summary>The template stage for a stage-template key (e.g. <c>preparatory-4-scientific</c>), or null.</summary>
    public SuggestedStageTemplate? StageForKey(string key) =>
        TemplateNameFor(key) is { } name ? Stages.FirstOrDefault(item => ArabicText.Normalize(item.Name) == ArabicText.Normalize(name)) : null;

    /// <summary>The suggested subjects of a stage: its mandatory rows under their canonical names, in template order.
    /// Optional subjects stay out until the owner ticks them in «تعبئة المنهج».</summary>
    public IReadOnlyList<string> MandatorySubjects(SuggestedStageTemplate stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        return stage.Entries.Where(entry => !entry.Optional).Select(entry => Canonical(entry.Subject) ?? entry.Subject).Distinct(StringComparer.Ordinal).ToArray();
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
        var resource = assembly.GetManifestResourceNames().Single(item => item.EndsWith($".Templates.{ResourceName}", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        var file = JsonSerializer.Deserialize<TemplateFile>(stream, Json)
            ?? throw new InvalidOperationException("The curriculum template is empty.");
        if (file.TemplateVersion != 2)
            throw new InvalidOperationException($"Unsupported curriculum template version {file.TemplateVersion}.");
        return new SuggestedCurriculumTemplate(file);
    }
}
