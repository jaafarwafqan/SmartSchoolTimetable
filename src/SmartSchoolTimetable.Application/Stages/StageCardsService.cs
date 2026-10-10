using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Application.Stages;

/// <summary>
/// Stage cards (spec 2.5 §6): all stages of a year with their sections in one call, and the per-stage section
/// stepper (spec 2.5 §3.4): adding generates the next labels; removing deletes only the LAST sections.
/// </summary>
public sealed class StageCardsService(IDataStore store, TimeProvider clock)
{
    public const int MaxSections = 30;

    public async Task<IReadOnlyList<StageCardDto>> ListAsync(long yearId, bool includeArchived, CancellationToken token)
    {
        var stages = await store.ListAsync(
            store.Read<Stage>().Where(stage => stage.AcademicYearId == yearId && (includeArchived || !stage.IsArchived))
                .OrderBy(stage => stage.DisplayOrder).ThenBy(stage => stage.NormalizedName),
            token);
        var stageIds = stages.Select(stage => stage.Id).ToArray();
        var sections = await store.ListAsync(
            store.Read<Section>().Where(section => stageIds.Contains(section.StageId) && (includeArchived || !section.IsArchived)).OrderBy(section => section.Id),
            token);
        var mapper = await SectionMapper.LoadAsync(store, sections, token);
        return stages
            .Select(stage => new StageCardDto(StagesSectionsService.ToDto(stage), sections.Where(section => section.StageId == stage.Id).Select(mapper.ToDto).ToArray()))
            .ToArray();
    }

    public async Task<OperationResult<StageCardDto>> SetSectionCountAsync(long yearId, long stageId, SetSectionCountCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = new InputErrors();
        var style = string.IsNullOrWhiteSpace(command.LabelStyle) ? SectionLabelStyle.Arabic : input.Option<SectionLabelStyle>(command.LabelStyle, "LabelStyle");
        if (command.Count is < 0 or > MaxSections)
            input.Add("Count", ErrorCodes.ValueOutOfRange);
        if (input.Any)
            return input.ToResult<StageCardDto>();
        if (await store.FirstOrDefaultAsync(store.Query<Stage>().Where(stage => stage.Id == stageId && stage.AcademicYearId == yearId), token) is not { } stage)
            return OperationResult.Failure<StageCardDto>(ErrorCodes.NotFound);
        if (stage.IsArchived)
            return OperationResult.Failure<StageCardDto>(ErrorCodes.StageArchived);

        var all = await store.ListAsync(store.Query<Section>().Where(section => section.StageId == stageId), token);
        var active = all.Where(section => !section.IsArchived).OrderBy(section => section.Id).ToList();
        if (command.Count > active.Count)
        {
            if (!await store.AnyAsync(store.Query<Shift>().Where(shift => shift.Id == command.ShiftId && shift.AcademicYearId == yearId), token))
                return OperationResult.Invalid<StageCardDto>("ShiftId", ErrorCodes.ShiftNotInYear);
            // Labels are unique per stage including archived sections (database index), so skip every used label.
            var used = all.Select(section => section.NormalizedLabel).ToHashSet(StringComparer.Ordinal);
            for (var added = active.Count; added < command.Count; added++)
            {
                var label = SectionLabels.Next(style, candidate => used.Contains(ArabicText.Normalize(candidate)));
                used.Add(ArabicText.Normalize(label));
                store.Add(Section.Create(stageId, command.ShiftId, label, null));
            }
            AuditTrail.Record(store, clock, "SectionsAdded", $"stage:{stageId}", $"Sections increased to {command.Count}.");
        }
        else if (command.Count < active.Count)
        {
            // Only the last sections are removed, and never one that something refers to (workload assignments).
            var references = new ReferenceGuard(store);
            foreach (var section in active.Skip(command.Count))
            {
                if (await references.DeleteBlockedAsync(ReferenceKinds.Section, section.Id, token) is { } inUse)
                    return OperationResult.Failure<StageCardDto>(inUse);
                store.Remove(section);
            }
            AuditTrail.Record(store, clock, "SectionsRemoved", $"stage:{stageId}", $"Sections reduced to {command.Count}.");
        }
        var saved = await store.SaveAsync(() => true, "Count", token);
        if (!saved.Succeeded)
            return saved.Cast<StageCardDto>();
        var cards = await ListAsync(yearId, includeArchived: false, token);
        return OperationResult.Success(cards.Single(card => card.Stage.Id == stageId));
    }
}
