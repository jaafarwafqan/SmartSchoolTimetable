using SmartSchoolTimetable.Application.Setup;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Tests.Phase25;

/// <summary>Section labels, curriculum entries and totals, and the shipped JSON templates (spec 2.5 §3.3, §3.4, §4.1).</summary>
public sealed class CurriculumDomainTests
{
    private static readonly string[] FirstSixArabic = ["أ", "ب", "ج", "د", "هـ", "و"];
    private static readonly string[] SchoolTypeValues = ["primary", "intermediate", "preparatory", "secondary"];

    [Fact]
    public void SectionLabelsFollowTheArabicOrderThenRepeatWithANumber()
    {
        Assert.Equal(FirstSixArabic, SectionLabels.First(SectionLabelStyle.Arabic, 6));
        Assert.Equal(("ي", "أ1", "ب1"), (SectionLabels.At(SectionLabelStyle.Arabic, 9), SectionLabels.At(SectionLabelStyle.Arabic, 28), SectionLabels.At(SectionLabelStyle.Arabic, 29)));
        Assert.Equal(("1", "12"), (SectionLabels.At(SectionLabelStyle.Numbers, 0), SectionLabels.At(SectionLabelStyle.Numbers, 11)));
        Assert.Equal(("A", "Z", "A1"), (SectionLabels.At(SectionLabelStyle.Latin, 0), SectionLabels.At(SectionLabelStyle.Latin, 25), SectionLabels.At(SectionLabelStyle.Latin, 26)));
        Assert.Empty(SectionLabels.First(SectionLabelStyle.Arabic, -1));
        Assert.Equal("ج", SectionLabels.Next(SectionLabelStyle.Arabic, label => label is "أ" or "ب"));
        Assert.Equal((4, -1), (SectionLabels.IndexOf(SectionLabelStyle.Arabic, "هـ"), SectionLabels.IndexOf(SectionLabelStyle.Arabic, "الأولى")));
        Assert.Throws<ArgumentOutOfRangeException>(() => SectionLabels.At(SectionLabelStyle.Arabic, -1));
    }

    [Fact]
    public void CurriculumEntriesValidateAndTellRepeatedLinesApartByLabel()
    {
        var entry = CurriculumEntry.Create(1, 2, 5, "  قواعد ", needsDoublePeriod: false, null);
        Assert.Equal(("قواعد", 5), (entry.Label, entry.WeeklyLessons));
        Assert.True(entry.SameLineAs(2, "قواعد"));
        Assert.False(entry.SameLineAs(2, null));
        Assert.False(entry.SameLineAs(3, "قواعد"));

        var errors = Assert.Throws<DomainValidationException>(() => CurriculumEntry.Create(1, 2, 16, new string('ا', 41), false, new string('ن', 301))).Errors;
        Assert.Equal(["WeeklyLessons", "Label", "Notes"], errors.Select(error => error.Field));
        Assert.Throws<DomainValidationException>(() => entry.SetWeeklyLessons(0));

        entry.Update(4, null, true, " ملاحظة ");
        Assert.Equal((4, null, true, "ملاحظة", 2), (entry.WeeklyLessons, entry.Label, entry.NeedsDoublePeriod, entry.Notes, entry.Version));
        var copy = entry.CopyTo(9);
        Assert.Equal((9L, 2L, 4, true), (copy.StageId, copy.SubjectId, copy.WeeklyLessons, copy.NeedsDoublePeriod));

        entry.Archive(DateTimeOffset.UnixEpoch);
        entry.Archive(DateTimeOffset.UnixEpoch);
        Assert.Equal((true, 3), (entry.IsArchived, entry.Version));
        entry.Restore();
        entry.Restore();
        Assert.Equal((false, null, 4), (entry.IsArchived, entry.ArchivedAt, entry.Version));
    }

    [Fact]
    public void TotalsCompareThePlanWithEachShiftOfTheStage()
    {
        var totals = CurriculumTotals.For(30, [new ShiftCapacity(1, "صباحي", 3, 35), new ShiftCapacity(2, "مسائي", 2, 30), new ShiftCapacity(3, "قصير", 1, 25)]);
        Assert.Equal([CapacityStatus.Under, CapacityStatus.Equal, CapacityStatus.Over], totals.Select(total => total.Status));
        Assert.Equal([5, 0, -5], totals.Select(total => total.Difference));
        Assert.Empty(CurriculumTotals.For(10, []));
    }

    [Fact]
    public void TemplatesAreConsistentAndShipNoInventedLessonCounts()
    {
        var catalog = TemplateCatalog.Current;
        Assert.All(catalog.Grades, grade => Assert.All(grade.SchoolTypes, type => Assert.Contains(type, SchoolTypeValues)));
        Assert.Equal(6, catalog.GradesFor(SchoolType.Primary).Count);
        Assert.Equal(3, catalog.GradesFor(SchoolType.Intermediate).Count);
        Assert.Equal(3, catalog.GradesFor(SchoolType.Preparatory).Count);
        Assert.Equal(
            catalog.GradesFor(SchoolType.Intermediate).Concat(catalog.GradesFor(SchoolType.Preparatory)).Select(grade => grade.Key),
            catalog.GradesFor(SchoolType.Secondary).Select(grade => grade.Key));
        Assert.Empty(catalog.GradesFor(SchoolType.Other));

        var fourth = catalog.Grade("preparatory-4")!;
        Assert.Equal(("preparatory-4-scientific", "الرابع العلمي"), TemplateCatalog.Stage(fourth, catalog.Branch("scientific")));
        Assert.Equal(("intermediate-1", "الأول المتوسط"), TemplateCatalog.Stage(catalog.Grade("intermediate-1")!, catalog.Branch("literary")));
        Assert.Null(catalog.Grade("unknown"));

        // One source for suggested subjects: every stage key has an official template stage, whose suggested subjects
        // are exactly its mandatory rows (canonical names); optional subjects stay out until ticked.
        var official = SuggestedCurriculumTemplate.Current;
        var stageKeys = catalog.Grades.SelectMany(grade => grade.BranchStem is null
            ? [grade.Key]
            : catalog.Branches.Select(branch => TemplateCatalog.Stage(grade, branch).Key)).ToArray();
        Assert.Equal(15, stageKeys.Length);
        foreach (var key in stageKeys)
        {
            var stage = official.StageForKey(key);
            Assert.NotNull(stage);
            Assert.Equal(stage.Entries.Where(entry => !entry.Optional).Select(entry => official.Canonical(entry.Subject)).Distinct(), official.MandatorySubjects(stage));
        }
        Assert.NotEqual(official.MandatorySubjects(official.StageForKey("preparatory-4-literary")!), official.MandatorySubjects(official.StageForKey("preparatory-4-scientific")!));
        Assert.DoesNotContain("اللغة الكردية", official.MandatorySubjects(official.StageForKey("preparatory-4-scientific")!));
        Assert.Null(official.StageForKey("unknown"));
        Assert.Null(official.StageForKey("preparatory-4")); // a branch grade alone is not a stage

        // The catalog carries each stage's official total and its total with every optional subject (السادس العلمي 33 → 37).
        var totals = catalog.ToDto().OfficialStages;
        Assert.Equal(stageKeys, totals.Select(stage => stage.Key));
        Assert.Equal((33, 37), totals.Where(stage => stage.Key == "preparatory-6-scientific").Select(stage => (stage.OfficialTotal, stage.AllOptionalTotal)).Single());
        Assert.Equal((31, 31), totals.Where(stage => stage.Key == "primary-6").Select(stage => (stage.OfficialTotal, stage.AllOptionalTotal)).Single());
        Assert.All(totals, stage => Assert.NotEmpty(stage.SchoolTypes));

        // Every period preset generates a valid day; working-day presets have exactly one default.
        Assert.All(catalog.PeriodPresets, preset =>
        {
            var plan = new PeriodPlan(TimeOnly.Parse(preset.FirstStart, System.Globalization.CultureInfo.InvariantCulture), preset.LessonMinutes, preset.LessonCount,
                preset.Breaks.Select(slot => new BreakSlot(slot.AfterLesson, slot.Minutes)).ToArray());
            Assert.Equal(preset.LessonCount, PeriodGenerator.Generate(plan).Count(period => period.Kind == PeriodKind.Lesson));
        });
        Assert.Single(catalog.WorkingDayPresets, preset => preset.IsDefault);
        Assert.All(catalog.WorkingDayPresets, preset => Assert.All(preset.Days, day => Assert.InRange(day, 1, 7)));
        Assert.Equal(catalog.Grades.Count, catalog.ToDto().Grades.Count);
    }
}
