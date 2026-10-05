using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Setup;

/// <param name="OptionalSubjects">Optional subjects to include (اللغة الكردية، اللغة الفرنسية); none by default.</param>
public sealed record SuggestedCurriculumCommand(IReadOnlyList<string>? OptionalSubjects, bool Confirm = false);

/// <param name="Action">create (new subject) or exists (matched, possibly through an alias: <paramref name="ExistingName"/>).</param>
public sealed record SuggestedSubjectLineDto(string Name, string Action, string? ExistingName, bool Optional, bool Included);

/// <param name="Action">create, exists (the stage already has this subject; never changed), update/unchanged (stage reset only), skipped (optional, not chosen).</param>
public sealed record SuggestedEntryLineDto(string Subject, int Lessons, string Action, bool Optional, int? CurrentLessons);

/// <param name="SuggestedTotal">Sum of the included rows (computed; the source's stated total is not trusted).</param>
/// <param name="ResultingTotal">The stage's planned lessons after applying.</param>
public sealed record SuggestedStageDto(
    long StageId,
    string StageName,
    bool NeedsReview,
    int StatedTotal,
    int SuggestedTotal,
    int CurrentTotal,
    int ResultingTotal,
    IReadOnlyList<SuggestedEntryLineDto> Entries);

public sealed record SuggestedCurriculumPlanDto(
    string Provenance,
    IReadOnlyList<SuggestedSubjectLineDto> Subjects,
    IReadOnlyList<SuggestedStageDto> Stages,
    IReadOnlyList<string> OptionalSubjects,
    int Changes);

/// <summary>
/// "تعبئة المنهج المقترح" (ADR 0028, 0029): fills the curriculum of the school's existing stages from the suggested
/// template. Preview first; apply only ADDS missing subjects and missing (stage, subject) lines, marked «مقترح»; it never
/// overwrites a value or deletes anything, so running it twice changes nothing. Subjects are matched through aliases
/// and Arabic normalization (اللغة الإنجليزية is اللغة الإنكليزية), never duplicated. The per-stage reset changes values
/// back to the suggestion only after the owner confirmed its before/after preview.
/// </summary>
public sealed class SuggestedCurriculumService(IDataStore store, SubjectsService subjects, TimeProvider clock)
{
    private static SuggestedCurriculumTemplate Template => SuggestedCurriculumTemplate.Current;

    private sealed record Context(
        IReadOnlyList<(Stage Stage, SuggestedStageTemplate Template)> Stages,
        IReadOnlyList<Subject> Subjects,
        IReadOnlyList<CurriculumEntry> Entries,
        IReadOnlySet<string> Chosen);

    public async Task<OperationResult<SuggestedCurriculumPlanDto>> PreviewAsync(long yearId, SuggestedCurriculumCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!await store.AnyAsync(store.Query<AcademicYear>().Where(year => year.Id == yearId), token))
            return OperationResult.Failure<SuggestedCurriculumPlanDto>(ErrorCodes.NotFound);
        return OperationResult.Success(Plan(await LoadAsync(yearId, command, token), resetStageId: null));
    }

    public async Task<OperationResult<SuggestedCurriculumPlanDto>> ApplyAsync(long yearId, SuggestedCurriculumCommand command, CancellationToken token)
    {
        var preview = await PreviewAsync(yearId, command, token);
        if (!preview.Succeeded || preview.Value!.Changes == 0)
            return preview;
        return await SetupTransaction.RunAsync(store, async () =>
        {
            var created = await CreateSubjectsAsync(preview.Value, token);
            var context = await LoadAsync(yearId, command, token);
            foreach (var (stage, template) in context.Stages)
            {
                foreach (var entry in Included(template, context.Chosen))
                {
                    var subject = Match(context.Subjects, entry.Subject);
                    if (subject is null || context.Entries.Any(line => line.StageId == stage.Id && line.SubjectId == subject.Id))
                        continue;
                    store.Add(CurriculumEntry.CreateSuggested(stage.Id, subject.Id, entry.Lessons));
                }
            }
            AuditTrail.Record(store, clock, "SuggestedCurriculumApplied", $"academic-year:{yearId}",
                $"Suggested curriculum v{Template.Version} applied ({created} subjects created).");
            await store.SaveChangesAsync(token);
            return preview.Value;
        }, token);
    }

    /// <summary>Before/after of "إعادة المقترح لهذه المرحلة" for one stage (nothing is saved).</summary>
    public async Task<OperationResult<SuggestedCurriculumPlanDto>> PreviewResetAsync(long yearId, long stageId, SuggestedCurriculumCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var context = await LoadAsync(yearId, command, token);
        if (context.Stages.All(item => item.Stage.Id != stageId))
            return OperationResult.Failure<SuggestedCurriculumPlanDto>(ErrorCodes.NotFound);
        return OperationResult.Success(Plan(context, stageId));
    }

    /// <summary>Resets one stage's lines to the suggestion after confirmation; extra lines the owner added are kept.</summary>
    public async Task<OperationResult<SuggestedCurriculumPlanDto>> ResetStageAsync(long yearId, long stageId, SuggestedCurriculumCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Confirm)
            return OperationResult.Invalid<SuggestedCurriculumPlanDto>("Confirm", ErrorCodes.Required);
        var preview = await PreviewResetAsync(yearId, stageId, command, token);
        if (!preview.Succeeded || preview.Value!.Changes == 0)
            return preview;
        return await SetupTransaction.RunAsync(store, async () =>
        {
            await CreateSubjectsAsync(preview.Value, token);
            var context = await LoadAsync(yearId, command, token);
            var (stage, template) = context.Stages.Single(item => item.Stage.Id == stageId);
            foreach (var entry in Included(template, context.Chosen))
            {
                var subject = Match(context.Subjects, entry.Subject)!;
                var existing = MainLine(context.Entries, stage.Id, subject.Id);
                if (existing is null)
                    store.Add(CurriculumEntry.CreateSuggested(stage.Id, subject.Id, entry.Lessons));
                else if (existing.WeeklyLessons != entry.Lessons || !existing.IsSuggested)
                    existing.ResetToSuggestion(entry.Lessons);
            }
            AuditTrail.Record(store, clock, "SuggestedCurriculumStageReset", $"stage:{stageId}", "Stage curriculum reset to the suggestion after confirmation.");
            await store.SaveChangesAsync(token);
            return preview.Value;
        }, token);
    }

    private static SuggestedCurriculumPlanDto Plan(Context context, long? resetStageId)
    {
        var stages = context.Stages.Where(item => resetStageId is null || item.Stage.Id == resetStageId).ToArray();
        var subjectLines = new Dictionary<string, SuggestedSubjectLineDto>(StringComparer.Ordinal);
        var stageLines = new List<SuggestedStageDto>();
        foreach (var (stage, template) in stages)
        {
            var current = context.Entries.Where(line => line.StageId == stage.Id).Sum(line => line.WeeklyLessons);
            var lines = new List<SuggestedEntryLineDto>();
            var added = 0;
            foreach (var entry in template.Entries)
            {
                var included = !entry.Optional || context.Chosen.Contains(entry.Subject);
                var subject = Match(context.Subjects, entry.Subject);
                subjectLines.TryAdd(entry.Subject, new SuggestedSubjectLineDto(entry.Subject, subject is null ? "create" : "exists", subject?.Name, entry.Optional, included));
                if (included && !subjectLines[entry.Subject].Included)
                    subjectLines[entry.Subject] = subjectLines[entry.Subject] with { Included = true };
                if (!included)
                {
                    lines.Add(new SuggestedEntryLineDto(entry.Subject, entry.Lessons, "skipped", entry.Optional, null));
                    continue;
                }
                if (resetStageId is null)
                {
                    var existing = subject is null ? null : context.Entries.Where(line => line.StageId == stage.Id && line.SubjectId == subject.Id).ToArray();
                    var exists = existing is { Length: > 0 };
                    lines.Add(new SuggestedEntryLineDto(entry.Subject, entry.Lessons, exists ? "exists" : "create", entry.Optional, exists ? existing!.Sum(line => line.WeeklyLessons) : null));
                    if (!exists) added += entry.Lessons;
                }
                else
                {
                    var main = subject is null ? null : MainLine(context.Entries, stage.Id, subject.Id);
                    var action = main is null ? "create" : main.WeeklyLessons == entry.Lessons && main.IsSuggested ? "unchanged" : "update";
                    lines.Add(new SuggestedEntryLineDto(entry.Subject, entry.Lessons, action, entry.Optional, main?.WeeklyLessons));
                    added += entry.Lessons - (main?.WeeklyLessons ?? 0);
                }
            }
            stageLines.Add(new SuggestedStageDto(stage.Id, stage.Name, template.NeedsReview, template.StatedTotal,
                Included(template, context.Chosen).Sum(entry => entry.Lessons), current, current + added, lines));
        }
        var neededSubjects = subjectLines.Values.Where(line => line.Included).ToArray();
        var changes = neededSubjects.Count(line => line.Action == "create")
            + stageLines.Sum(stage => stage.Entries.Count(line => line.Action is "create" or "update"));
        var optional = Template.Stages.SelectMany(stage => stage.Entries).Where(entry => entry.Optional).Select(entry => entry.Subject).Distinct().ToArray();
        return new SuggestedCurriculumPlanDto(Template.Provenance, subjectLines.Values.ToArray(), stageLines, optional, changes);
    }

    private async Task<int> CreateSubjectsAsync(SuggestedCurriculumPlanDto plan, CancellationToken token)
    {
        var missing = plan.Subjects.Where(line => line.Included && line.Action == "create").ToArray();
        foreach (var line in missing)
        {
            // Automatic palette colour, priority 3, no advanced flags (same defaults as quick add).
            SetupTransaction.Require(await subjects.CreateAsync(new SaveSubjectCommand(line.Name, 0, 0, true, false, false, false, null, null, 0), token));
        }
        return missing.Length;
    }

    private async Task<Context> LoadAsync(long yearId, SuggestedCurriculumCommand command, CancellationToken token)
    {
        var stages = await store.ListAsync(store.Query<Stage>().Where(stage => stage.AcademicYearId == yearId && !stage.IsArchived)
            .OrderBy(stage => stage.DisplayOrder).ThenBy(stage => stage.NormalizedName), token);
        var matched = stages.Select(stage => (Stage: stage, Template: Template.StageFor(stage))).Where(item => item.Template is not null)
            .Select(item => (item.Stage, item.Template!)).ToArray();
        var stageIds = matched.Select(item => item.Stage.Id).ToArray();
        var subjectList = await store.ListAsync(store.Query<Subject>().Where(subject => !subject.IsArchived), token);
        var entries = await store.ListAsync(store.Query<CurriculumEntry>().Where(entry => stageIds.Contains(entry.StageId) && !entry.IsArchived), token);
        var chosen = (command.OptionalSubjects ?? []).Select(name => Template.Canonical(name)).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return new Context(matched, subjectList, entries, chosen);
    }

    private static IEnumerable<SuggestedEntryTemplate> Included(SuggestedStageTemplate template, IReadOnlySet<string> chosen) =>
        template.Entries.Where(entry => !entry.Optional || chosen.Contains(entry.Subject));

    private static Subject? Match(IReadOnlyList<Subject> subjects, string templateSubject) =>
        subjects.FirstOrDefault(subject => Template.SameSubject(templateSubject, subject.Name))
        ?? subjects.FirstOrDefault(subject => subject.NormalizedName == ArabicText.Normalize(templateSubject));

    private static CurriculumEntry? MainLine(IReadOnlyList<CurriculumEntry> entries, long stageId, long subjectId) =>
        entries.Where(line => line.StageId == stageId && line.SubjectId == subjectId).OrderBy(line => line.NormalizedLabel.Length).ThenBy(line => line.Id).FirstOrDefault();
}
