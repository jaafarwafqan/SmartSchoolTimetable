using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Setup;

/// <param name="TemplateStage">The official stage name chosen from <see cref="SuggestedCurriculumPlanDto.TemplateStages"/>.</param>
public sealed record StageMatchInput(long StageId, string? TemplateStage);

/// <param name="OptionalSubjects">Optional subjects to include (اللغة الكردية، اللغة الفرنسية، الحاسوب، منهج جرائم حزب البعث); none by default.</param>
/// <param name="StageMatches">For a school stage the template cannot match by key or name: the official stage it follows
/// (chosen from a list, never typed). Not saved; it applies to this preview or apply only.</param>
public sealed record SuggestedCurriculumCommand(IReadOnlyList<string>? OptionalSubjects, bool Confirm = false, IReadOnlyList<StageMatchInput>? StageMatches = null);

/// <param name="Name">The template's canonical name (aliases folded: التربية الفنية is التربية الفنية والنشيد).</param>
/// <param name="Action">create (new subject) or exists (matched, possibly through an alias: <paramref name="ExistingName"/>).</param>
/// <param name="InStatedTotal">Optional subjects only: counted in the official total (Kurdish) or added on top of it.</param>
/// <param name="Note">The template's remark for the subject (Arabic data), if any.</param>
public sealed record SuggestedSubjectLineDto(string Name, string Action, string? ExistingName, bool Optional, bool Included, bool InStatedTotal, string? Note);

/// <param name="Action">create, exists (the stage already has this subject; never changed), update/unchanged (stage reset only), skipped (optional, not chosen).</param>
public sealed record SuggestedEntryLineDto(string Subject, int Lessons, string Action, bool Optional, int? CurrentLessons, bool InStatedTotal, string? Note);

/// <param name="StatedTotal">The total printed in the official plan.</param>
/// <param name="OfficialTotal">Sum of the rows counted in the official total (mandatory + Kurdish); equals
/// <paramref name="StatedTotal"/> in every stage of the current template.</param>
/// <param name="SuggestedTotal">Sum of the enabled rows (mandatory + chosen optional).</param>
/// <param name="ResultingTotal">The stage's planned lessons after applying.</param>
/// <param name="WeeklyCapacity">The most lessons a week the stage's shift(s) allow (as in the daily suggestion).</param>
/// <param name="WorkingDays">The number of working days, to tell how many lessons a day the total needs.</param>
/// <param name="VerificationNote">The source's question for the owner (Arabic data), shown but never blocking.</param>
public sealed record SuggestedStageDto(
    long StageId,
    string StageName,
    bool NeedsReview,
    int StatedTotal,
    int OfficialTotal,
    int SuggestedTotal,
    int CurrentTotal,
    int ResultingTotal,
    string? VerificationNote,
    IReadOnlyList<SuggestedEntryLineDto> Entries,
    int WeeklyCapacity,
    int WorkingDays);

/// <param name="TemplateStage">The official stage the owner chose for it in this request, or null.</param>
public sealed record UnmatchedStageDto(long StageId, string StageName, string? TemplateStage);

/// <param name="UnmatchedStages">Active stages the template cannot match by key or name (shown as «مراحل لا يوجد لها قالب رسمي»).</param>
/// <param name="TemplateStages">The official stage names, the only choices for <see cref="StageMatchInput.TemplateStage"/>.</param>
public sealed record SuggestedCurriculumPlanDto(
    int TemplateVersion,
    CurriculumProvenance Provenance,
    IReadOnlyList<SuggestedSubjectLineDto> Subjects,
    IReadOnlyList<SuggestedStageDto> Stages,
    IReadOnlyList<string> OptionalSubjects,
    int Changes,
    IReadOnlyList<UnmatchedStageDto> UnmatchedStages,
    IReadOnlyList<string> TemplateStages);

/// <summary>
/// "تعبئة المنهج" (ADR 0028, 0029): fills the curriculum of the school's existing stages from the official 2026-2027
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
        IReadOnlySet<string> Chosen,
        IReadOnlyList<UnmatchedStageDto> Unmatched,
        IReadOnlyDictionary<long, StageCapacityInfo> Capacities);

    public async Task<OperationResult<SuggestedCurriculumPlanDto>> PreviewAsync(long yearId, SuggestedCurriculumCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!await store.AnyAsync(store.Query<AcademicYear>().Where(year => year.Id == yearId), token))
            return OperationResult.Failure<SuggestedCurriculumPlanDto>(ErrorCodes.NotFound);
        var context = await LoadAsync(yearId, command, token);
        return context is null
            ? OperationResult.Invalid<SuggestedCurriculumPlanDto>(nameof(command.StageMatches), ErrorCodes.InvalidOption)
            : OperationResult.Success(Plan(context, resetStageId: null));
    }

    public async Task<OperationResult<SuggestedCurriculumPlanDto>> ApplyAsync(long yearId, SuggestedCurriculumCommand command, CancellationToken token)
    {
        var preview = await PreviewAsync(yearId, command, token);
        if (!preview.Succeeded || preview.Value!.Changes == 0)
            return preview;
        return await SetupTransaction.RunAsync(store, async () =>
        {
            var created = await CreateSubjectsAsync(preview.Value, token);
            var context = (await LoadAsync(yearId, command, token))!;
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
            AuditTrail.Record(store, clock, AuditEvents.SuggestedCurriculumApplied, $"academic-year:{yearId}",
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
        if (context is null)
            return OperationResult.Invalid<SuggestedCurriculumPlanDto>(nameof(command.StageMatches), ErrorCodes.InvalidOption);
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
            var context = (await LoadAsync(yearId, command, token))!;
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
            AuditTrail.Record(store, clock, AuditEvents.SuggestedCurriculumStageReset, $"stage:{stageId}", "Stage curriculum reset to the suggestion after confirmation.");
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
                var included = IsIncluded(entry, context.Chosen);
                var subject = Match(context.Subjects, entry.Subject);
                var name = CanonicalName(entry.Subject);
                var inTotal = entry.CountsInStatedTotal;
                subjectLines.TryAdd(name, new SuggestedSubjectLineDto(name, subject is null ? "create" : "exists", subject?.Name, entry.Optional, included, inTotal, entry.Note));
                if (included && !subjectLines[name].Included)
                    subjectLines[name] = subjectLines[name] with { Included = true };
                if (!included)
                {
                    lines.Add(new SuggestedEntryLineDto(entry.Subject, entry.Lessons, "skipped", entry.Optional, null, inTotal, entry.Note));
                    continue;
                }
                if (resetStageId is null)
                {
                    var existing = subject is null ? null : context.Entries.Where(line => line.StageId == stage.Id && line.SubjectId == subject.Id).ToArray();
                    var exists = existing is { Length: > 0 };
                    lines.Add(new SuggestedEntryLineDto(entry.Subject, entry.Lessons, exists ? "exists" : "create", entry.Optional, exists ? existing!.Sum(line => line.WeeklyLessons) : null, inTotal, entry.Note));
                    if (!exists) added += entry.Lessons;
                }
                else
                {
                    var main = subject is null ? null : MainLine(context.Entries, stage.Id, subject.Id);
                    var action = main is null ? "create" : main.WeeklyLessons == entry.Lessons && main.IsSuggested ? "unchanged" : "update";
                    lines.Add(new SuggestedEntryLineDto(entry.Subject, entry.Lessons, action, entry.Optional, main?.WeeklyLessons, inTotal, entry.Note));
                    added += entry.Lessons - (main?.WeeklyLessons ?? 0);
                }
            }
            var capacity = context.Capacities[stage.Id];
            stageLines.Add(new SuggestedStageDto(stage.Id, stage.Name, template.NeedsReview, template.StatedTotal, template.OfficialTotal(),
                template.Total(context.Chosen), current, current + added, template.VerificationNote, lines, capacity.Weekly, capacity.Days.Count));
        }
        var neededSubjects = subjectLines.Values.Where(line => line.Included).ToArray();
        var changes = neededSubjects.Count(line => line.Action == "create")
            + stageLines.Sum(stage => stage.Entries.Count(line => line.Action is "create" or "update"));
        var optional = Template.Stages.SelectMany(stage => stage.Entries).Where(entry => entry.Optional).Select(entry => CanonicalName(entry.Subject)).Distinct().ToArray();
        return new SuggestedCurriculumPlanDto(Template.Version, Template.Provenance, subjectLines.Values.ToArray(), stageLines, optional, changes,
            resetStageId is null ? context.Unmatched : [], Template.Stages.Select(stage => stage.Name).ToArray());
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

    /// <summary>The year's stages with their template (by key or name, else the owner's match); null when a match
    /// names an unknown stage, a stage the template already matches, or a stage name outside the template.</summary>
    private async Task<Context?> LoadAsync(long yearId, SuggestedCurriculumCommand command, CancellationToken token)
    {
        var stages = await store.ListAsync(store.Query<Stage>().Where(stage => stage.AcademicYearId == yearId && !stage.IsArchived)
            .OrderBy(stage => stage.DisplayOrder).ThenBy(stage => stage.NormalizedName), token);
        var automatic = stages.ToDictionary(stage => stage.Id, Template.StageFor);
        var choices = new Dictionary<long, SuggestedStageTemplate>();
        foreach (var match in command.StageMatches ?? [])
        {
            var template = Template.Stages.FirstOrDefault(item => item.Name == match.TemplateStage);
            if (template is null || !automatic.TryGetValue(match.StageId, out var known) || known is not null || !choices.TryAdd(match.StageId, template))
                return null;
        }
        var matched = stages.Select(stage => (Stage: stage, Template: automatic[stage.Id] ?? choices.GetValueOrDefault(stage.Id))).Where(item => item.Template is not null)
            .Select(item => (item.Stage, item.Template!)).ToArray();
        var unmatched = stages.Where(stage => automatic[stage.Id] is null)
            .Select(stage => new UnmatchedStageDto(stage.Id, stage.Name, choices.GetValueOrDefault(stage.Id)?.Name)).ToArray();
        var stageIds = matched.Select(item => item.Stage.Id).ToArray();
        var subjectList = await store.ListAsync(store.Query<Subject>().Where(subject => !subject.IsArchived), token);
        var entries = await store.ListAsync(store.Query<CurriculumEntry>().Where(entry => stageIds.Contains(entry.StageId) && !entry.IsArchived), token);
        var chosen = (command.OptionalSubjects ?? []).Select(name => Template.Canonical(name)).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var capacities = await StageCapacity.LoadAsync(store, yearId, stages, token);
        return new Context(matched, subjectList, entries, chosen, unmatched, capacities);
    }

    private static IEnumerable<SuggestedEntryTemplate> Included(SuggestedStageTemplate template, IReadOnlySet<string> chosen) =>
        template.Entries.Where(entry => IsIncluded(entry, chosen));

    /// <summary>Mandatory rows always; optional rows only when the owner ticked them (all unticked by default).</summary>
    private static bool IsIncluded(SuggestedEntryTemplate entry, IReadOnlySet<string> chosen) =>
        !entry.Optional || chosen.Contains(CanonicalName(entry.Subject));

    private static string CanonicalName(string templateSubject) => Template.Canonical(templateSubject) ?? templateSubject;

    private static Subject? Match(IReadOnlyList<Subject> subjects, string templateSubject) =>
        subjects.FirstOrDefault(subject => Template.SameSubject(templateSubject, subject.Name))
        ?? subjects.FirstOrDefault(subject => subject.NormalizedName == ArabicText.Normalize(templateSubject));

    private static CurriculumEntry? MainLine(IReadOnlyList<CurriculumEntry> entries, long stageId, long subjectId) =>
        entries.Where(line => line.StageId == stageId && line.SubjectId == subjectId).OrderBy(line => line.NormalizedLabel.Length).ThenBy(line => line.Id).FirstOrDefault();
}
