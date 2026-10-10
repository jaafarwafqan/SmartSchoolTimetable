using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Domain.Generation;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>M1: the lifecycle table of a saved timetable and the comparer, as pure logic.</summary>
public sealed class TimetableLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 8, 0, 0, TimeSpan.Zero);

    private static TimetableVersion Version(params TimetableLesson[] lessons) =>
        TimetableVersion.Create(1, 1, TimetableSource.Generated, null, null, GenerationModes.Standard, "hash", "{}", 0, null, null,
            lessons.Length == 0 ? [new TimetableLesson(1, 1, 1, 1, 1)] : lessons, Now);

    public static TheoryData<TimetableStatus, TimetableStatus, bool> Transitions => new()
    {
        { TimetableStatus.Draft, TimetableStatus.Approved, true },
        { TimetableStatus.Draft, TimetableStatus.Archived, true },
        { TimetableStatus.Approved, TimetableStatus.Archived, true },
        { TimetableStatus.Draft, TimetableStatus.Draft, false },
        { TimetableStatus.Approved, TimetableStatus.Approved, false },
        { TimetableStatus.Approved, TimetableStatus.Draft, false },
        { TimetableStatus.Archived, TimetableStatus.Draft, false },
        { TimetableStatus.Archived, TimetableStatus.Approved, false },
        { TimetableStatus.Archived, TimetableStatus.Archived, false },
    };

    [Theory]
    [MemberData(nameof(Transitions))]
    public void OnlyTheDocumentedTransitionsAreValid(TimetableStatus from, TimetableStatus to, bool valid) =>
        Assert.Equal(valid, TimetableTransitions.IsValid(from, to));

    [Fact]
    public void ANewVersionIsADraftAndApprovingKeepsTheStatusAndTheFlagTogether()
    {
        var version = Version();
        Assert.Equal((TimetableStatus.Draft, false), (version.Status, version.IsApproved));
        Assert.True(version.Approve(Now));
        Assert.Equal((TimetableStatus.Approved, true, Now), (version.Status, version.IsApproved, version.ApprovedAt));
        Assert.False(version.Approve(Now));
        Assert.True(version.Archive(Now));
        Assert.Equal((TimetableStatus.Archived, false, Now), (version.Status, version.IsApproved, version.ArchivedAt));
    }

    [Fact]
    public void AnArchivedVersionNeverChangesAgainAndItsLessonsAreFixed()
    {
        var version = Version(new TimetableLesson(1, 1, 1, 1, 1), new TimetableLesson(1, 1, 1, 2, 1));
        var number = version.Version;
        Assert.True(version.Archive(Now));
        Assert.False(version.Approve(Now.AddDays(1)));
        Assert.False(version.Archive(Now.AddDays(1)));
        Assert.Equal((TimetableStatus.Archived, Now, 2), (version.Status, version.ArchivedAt, version.Lessons.Count));
        // Rejected transitions do not touch the concurrency version either.
        Assert.Equal(number + 1, version.Version);
        Assert.IsAssignableFrom<IReadOnlyList<TimetableLesson>>(version.Lessons);
    }

    private static TimetableLesson L(long section, long line, long teacher, int day, int lesson) => new(section, line, teacher, day, lesson);

    private static readonly Dictionary<long, long> Subjects = new() { [10] = 100, [11] = 101 };

    [Fact]
    public void IdenticalTimetablesHaveNoChanges()
    {
        var lessons = new[] { L(1, 10, 7, 1, 1), L(1, 10, 7, 2, 1), L(1, 11, 8, 3, 1) };
        var (totals, changes) = TimetableComparer.Compare(lessons, lessons, Subjects);
        Assert.Empty(changes);
        Assert.Equal(new CompareTotals(0, 0, 0, 0, 3), totals);
    }

    [Fact]
    public void AMovedLessonIsOneMoveWithBothSlotsAndTheSubject()
    {
        var older = new[] { L(1, 10, 7, 1, 1), L(1, 10, 7, 2, 1) };
        var newer = new[] { L(1, 10, 7, 1, 1), L(1, 10, 7, 2, 3) };
        var (totals, changes) = TimetableComparer.Compare(older, newer, Subjects);
        var change = Assert.Single(changes);
        Assert.Equal(new LessonChange(ChangeKinds.Moved, 1, 10, 100, 7, 7, 2, 1, 2, 3), change);
        Assert.Equal(new CompareTotals(0, 0, 1, 0, 1), totals);
    }

    [Fact]
    public void ASameSlotWithAnotherTeacherIsReassignedNotMoved()
    {
        var (totals, changes) = TimetableComparer.Compare([L(1, 10, 7, 1, 1)], [L(1, 10, 9, 1, 1)], Subjects);
        var change = Assert.Single(changes);
        Assert.Equal((ChangeKinds.Reassigned, 7L, 9L), (change.Kind, change.FromTeacherId, change.ToTeacherId));
        Assert.Equal(1, totals.Reassigned);
        var teachers = TimetableComparer.ByTeacher(changes);
        Assert.Equal([new TeacherChangeSummary(7, 0, 1, 0), new TeacherChangeSummary(9, 1, 0, 0)], teachers);
    }

    [Fact]
    public void LessonsOnlyOnOneSideAreAddedOrRemovedAndSectionsAreSummarised()
    {
        var older = new[] { L(1, 10, 7, 1, 1), L(2, 11, 8, 1, 1) };
        var newer = new[] { L(1, 10, 7, 1, 1), L(2, 11, 8, 1, 1), L(2, 11, 8, 2, 1) };
        var (totals, changes) = TimetableComparer.Compare(older, newer, Subjects);
        Assert.Equal(new CompareTotals(1, 0, 0, 0, 2), totals);
        Assert.Equal([new SectionChangeSummary(2, 1, 0, 0, 0)], TimetableComparer.BySection(changes));
        var (backTotals, back) = TimetableComparer.Compare(newer, older, Subjects);
        Assert.Equal(new CompareTotals(0, 1, 0, 0, 2), backTotals);
        Assert.Equal(ChangeKinds.Removed, Assert.Single(back).Kind);
    }

    [Fact]
    public void ALessonLineThatLeftTheSchoolIsRemovedAndANewOneIsAdded()
    {
        var (totals, changes) = TimetableComparer.Compare([L(1, 10, 7, 1, 1)], [L(1, 11, 8, 1, 1)], Subjects);
        Assert.Equal(new CompareTotals(1, 1, 0, 0, 0), totals);
        Assert.Equal([ChangeKinds.Removed, ChangeKinds.Added], changes.Select(change => change.Kind).Order(StringComparer.Ordinal).Reverse().ToArray());
    }

    [Fact]
    public void TheCountsAlwaysAddUpOnRandomTimetables()
    {
        var random = new Random(20261010);
        for (var round = 0; round < 200; round++)
        {
            var older = RandomLessons(random);
            var newer = RandomLessons(random);
            var (totals, changes) = TimetableComparer.Compare(older, newer, Subjects);
            // Every older lesson is unchanged, moved, reassigned or removed; every newer one is unchanged, moved, reassigned or added.
            Assert.Equal(older.Length, totals.Unchanged + totals.Moved + totals.Reassigned + totals.Removed);
            Assert.Equal(newer.Length, totals.Unchanged + totals.Moved + totals.Reassigned + totals.Added);
            Assert.Equal(totals.Changed, changes.Count);
            // Deterministic.
            Assert.Equal(changes, TimetableComparer.Compare(older, newer, Subjects).Changes);
            // Comparing the other way swaps added and removed and keeps the moves.
            var reverse = TimetableComparer.Compare(newer, older, Subjects).Totals;
            Assert.Equal((totals.Added, totals.Removed, totals.Moved, totals.Reassigned), (reverse.Removed, reverse.Added, reverse.Moved, reverse.Reassigned));
        }
    }

    private static TimetableLesson[] RandomLessons(Random random)
    {
        var slots = new HashSet<(long Section, int Day, int Lesson)>();
        var lessons = new List<TimetableLesson>();
        for (var index = 0; index < random.Next(0, 25); index++)
        {
            var section = random.Next(1, 4);
            var day = random.Next(1, 6);
            var lesson = random.Next(1, 7);
            if (!slots.Add((section, day, lesson)))
                continue;
            lessons.Add(L(section, 10 + random.Next(0, 2), 7 + random.Next(0, 3), day, lesson));
        }
        return [.. lessons];
    }
}
