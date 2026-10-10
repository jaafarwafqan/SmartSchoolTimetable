using System.Diagnostics;
using Google.OrTools.Sat;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Domain.Scheduling;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Infrastructure.Solver;

/// <summary>
/// Explains an infeasible timetable (Phase 4 §5, ADR 0040): every restriction family sits behind an assumption literal;
/// CP-SAT returns a sufficient set of families, which is shrunk by deletion inside the budget, then each family of the
/// core is tested alone for "relaxing it makes the timetable possible". Workload (H1), section clashes (H2) and teacher
/// clashes (H4) are never relaxed: they define what a timetable is.
/// </summary>
internal static class CpSatDiagnostics
{
    private const double MinTrySeconds = 0.5;

    public static SolverDiagnostics Explain(SchedulingInput input, GenerationSettings settings, double budgetSeconds, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        double Remaining() => Math.Max(0, budgetSeconds - clock.Elapsed.TotalSeconds);
        var builder = CpSatModelBuilder.Build(input, settings.DoublePeriodsRequired, diagnostic: true, settings.Locked);
        var families = builder.AllFamilies.ToArray();

        var (status, core) = Run(builder, settings, families, Math.Max(MinTrySeconds, Remaining() * 0.4), wantCore: true, token);
        if (status != CpSolverStatus.Infeasible)
            return new SolverDiagnostics([Fundamental()], Minimal: false);
        if (core.Count == 0)
            return new SolverDiagnostics([Fundamental()], Minimal: true);

        // Deletion-based shrinking: drop a family when the rest of the core is still impossible on its own.
        var minimal = true;
        var kept = core.ToList();
        foreach (var family in core)
        {
            if (!kept.Contains(family))
                continue;
            if (token.IsCancellationRequested || Remaining() < MinTrySeconds)
            {
                minimal = false;
                break;
            }
            var candidate = kept.Where(item => item != family).ToArray();
            var (tryStatus, smaller) = Run(builder, settings, candidate, Math.Max(MinTrySeconds, Remaining() / Math.Max(1, kept.Count)), wantCore: true, token);
            if (tryStatus == CpSolverStatus.Infeasible)
                kept = smaller.Count > 0 ? smaller.ToList() : candidate.ToList();
            else if (tryStatus == CpSolverStatus.Unknown)
                minimal = false;
        }
        if (kept.Count == 0)
            return new SolverDiagnostics([Fundamental()], minimal);

        // Relaxation hints: everything enforced except this one family.
        var findings = new List<SolverFinding>();
        foreach (var family in kept)
        {
            bool? helps = null;
            if (!token.IsCancellationRequested && Remaining() >= MinTrySeconds)
            {
                var (relaxed, _) = Run(builder, settings, families.Where(item => item != family).ToArray(),
                    Math.Max(MinTrySeconds, Remaining() / Math.Max(1, kept.Count)), wantCore: false, token);
                helps = relaxed switch
                {
                    CpSolverStatus.Feasible or CpSolverStatus.Optimal => true,
                    CpSolverStatus.Infeasible => false,
                    _ => null,
                };
            }
            findings.Add(Translate(input, family, helps));
        }
        return new SolverDiagnostics(findings, minimal);
    }

    private static (CpSolverStatus Status, IReadOnlyList<Family> Core) Run(CpSatModelBuilder builder, GenerationSettings settings, IReadOnlyCollection<Family> enforced,
        double seconds, bool wantCore, CancellationToken token)
    {
        builder.Model.ClearAssumptions();
        builder.Model.AddAssumptions(enforced.Select(family => (ILiteral)family.Literal));
        var solver = new CpSolver { StringParameters = CpSatSolver.Parameters(settings, seconds, diagnostic: true) };
        CpSolverStatus status;
        using (token.Register(solver.StopSearch))
            status = solver.Solve(builder.Model);
        if (status != CpSolverStatus.Infeasible || !wantCore)
            return (status, []);
        var core = solver.SufficientAssumptionsForInfeasibility().Select(builder.FamilyOf).OfType<Family>().Distinct().ToArray();
        return (status, core);
    }

    private static SolverFinding Fundamental() =>
        new(DiagnosticCodes.Fundamental, new FindingEntity(FindingEntities.School, 0, string.Empty), [], null, null, ["moveWorkload", "increaseLessons"], null);

    private static FindingEntity SectionEntity(SectionInput section) => new(FindingEntities.Section, section.Id, $"{section.StageName} / {section.Label}");

    /// <summary>Turns one family into an Arabic-renderable finding: entity, numbers where computable, fixes with links.</summary>
    private static SolverFinding Translate(SchedulingInput input, Family family, bool? helps)
    {
        var lines = input.Lines.ToDictionary(line => line.Id);
        var sections = input.Sections.ToDictionary(section => section.Id);
        switch (family.Kind)
        {
            case Families.TeacherAvailability:
            case Families.TeacherLimits:
            {
                var teacher = input.Teachers.First(item => item.Id == family.Id);
                var rows = input.Assignments.Where(row => row.TeacherId == teacher.Id && lines.ContainsKey(row.LineId) && sections.ContainsKey(row.SectionId)).ToArray();
                var required = rows.Sum(row => lines[row.LineId].WeeklyLessons);
                var related = rows.Select(row => sections[row.SectionId]).Distinct().Select(SectionEntity).ToArray();
                var slots = rows.Select(row => sections[row.SectionId]).Distinct().SelectMany(section => section.AllowedByDay
                    .Where(day => input.WorkingDays.Contains(day.Day))
                    .SelectMany(day => Enumerable.Range(1, Math.Max(0, day.Lessons)).Select(lesson => new ShiftSlot(section.ShiftId, day.Day, lesson))));
                var entity = new FindingEntity(FindingEntities.Teacher, teacher.Id, teacher.Name);
                if (family.Kind == Families.TeacherAvailability)
                {
                    var available = TeacherAvailability.Compute(slots, teacher.OffDays, teacher.Blocked.Select(slot => new BlockedPeriod(slot.Day, slot.Lesson)).ToArray(),
                        null, null, teacher.Released).Available;
                    return new SolverFinding(DiagnosticCodes.TeacherAvailability, entity, related, required, available, ["removeTeacherBlocks", "moveWorkload"], helps);
                }
                var limit = Math.Min(teacher.MaxPerWeek ?? int.MaxValue, teacher.MaxPerDay is { } perDay ? perDay * input.WorkingDays.Count : int.MaxValue);
                return new SolverFinding(DiagnosticCodes.TeacherLimits, entity, related, required, limit == int.MaxValue ? null : limit, ["raiseTeacherLimit", "moveWorkload"], helps);
            }
            case Families.SectionPacking:
            {
                var section = sections[family.Id];
                var teachers = input.Assignments.Where(row => row.SectionId == section.Id).Select(row => row.TeacherId).Distinct()
                    .Select(id => input.Teachers.First(teacher => teacher.Id == id)).Select(teacher => new FindingEntity(FindingEntities.Teacher, teacher.Id, teacher.Name)).ToArray();
                var required = input.Lines.Where(line => line.StageId == section.StageId).Sum(line => line.WeeklyLessons);
                var available = section.AllowedByDay.Where(day => input.WorkingDays.Contains(day.Day)).Sum(day => day.Lessons);
                return new SolverFinding(DiagnosticCodes.SectionPacking, SectionEntity(section), teachers, required, available, ["removeTeacherBlocks", "changeTeacher"], helps);
            }
            case Families.LockedLessons:
            {
                var section = sections[family.Id];
                return new SolverFinding(DiagnosticCodes.LockedLessons, SectionEntity(section), [], null, null, ["unlockEdits"], helps);
            }
            case Families.StageDays:
            {
                var stage = input.Stages?.FirstOrDefault(item => item.Id == family.Id);
                var name = stage?.Name ?? input.Sections.FirstOrDefault(section => section.StageId == family.Id)?.StageName ?? string.Empty;
                return new SolverFinding(DiagnosticCodes.StageDays, new FindingEntity(FindingEntities.Stage, family.Id, name), [], null, null, ["increaseLessons"], helps);
            }
            case Families.SubjectBlocked:
            case Families.SubjectDailyCap:
            case Families.DoublePeriods:
            {
                var subject = input.Subjects.First(item => item.Id == family.Id);
                var entity = new FindingEntity(FindingEntities.Subject, subject.Id, subject.Name);
                return family.Kind switch
                {
                    Families.SubjectBlocked => new SolverFinding(DiagnosticCodes.SubjectBlocked, entity, [], null, subject.Blocked.Count, ["removeSubjectBlocks"], helps),
                    Families.SubjectDailyCap => new SolverFinding(DiagnosticCodes.SubjectDailyCap, entity, [], null, null, ["removeTeacherBlocks", "removeSubjectBlocks"], helps),
                    _ => new SolverFinding(DiagnosticCodes.DoublePeriods, entity, [], null, null, ["turnOffDoublePeriod", "removeSubjectBlocks"], helps),
                };
            }
            default:
            {
                var resource = input.Resources.First(item => item.Id == family.Id);
                var subjects = input.Subjects.Where(subject => subject.RequiredResourceId == resource.Id).ToArray();
                var required = input.Sections.Sum(section => input.Lines.Where(line => line.StageId == section.StageId && subjects.Any(subject => subject.Id == line.SubjectId))
                    .Sum(line => line.WeeklyLessons));
                return new SolverFinding(DiagnosticCodes.ResourceCapacity, new FindingEntity(FindingEntities.Resource, resource.Id, resource.Name),
                    subjects.Select(subject => new FindingEntity(FindingEntities.Subject, subject.Id, subject.Name)).ToArray(), required, resource.Capacity,
                    ["raiseResourceCapacity", "removeSubjectBlocks"], helps);
            }
        }
    }
}
