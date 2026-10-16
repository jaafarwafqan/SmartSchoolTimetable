using Microsoft.EntityFrameworkCore;
using SmartSchoolTimetable.Application.Generation;
using SmartSchoolTimetable.Application.Scheduling;
using SmartSchoolTimetable.Infrastructure;
using SmartSchoolTimetable.Infrastructure.Persistence;
using SmartSchoolTimetable.Infrastructure.Solver;
using Xunit.Abstractions;

namespace SmartSchoolTimetable.Tests.Phase5;

/// <summary>Runs only when SST_DIAG_DB names a COPY of a database (never the owner's own file): rebuilds the scheduling input and reports what each rule costs.</summary>
public sealed class SavedSchoolFactAttribute : FactAttribute
{
    public SavedSchoolFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SST_DIAG_DB")))
            Skip = "Diagnostics on a database copy: set SST_DIAG_DB to the copy's path.";
    }
}

public sealed class SavedSchoolDiagnosticsTests(ITestOutputHelper output)
{
    [SavedSchoolFact]
    public async Task SolveACopyWithEachRuleOnAndOff()
    {
        var path = Environment.GetEnvironmentVariable("SST_DIAG_DB")!;
        var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite($"Data Source={path};Mode=ReadOnly;Pooling=False").Options;
        await using var context = new LocalDbContext(options);
        var store = new EfDataStore(context);
        var yearId = await context.Set<Domain.SchoolSetup.AcademicYear>().Select(year => year.Id).FirstAsync();
        var input = (await SchedulingInputBuilder.BuildAsync(store, yearId, CancellationToken.None))!;
        var readiness = PreSolveValidator.Validate(input);
        output.WriteLine($"sections={input.Sections.Count} teachers={input.Assignments.Select(row => row.TeacherId).Distinct().Count()} ready={readiness.Ready} consecutive={input.Profile.DefaultMaxConsecutive}");
        foreach (var info in TeacherDays.Of(input).Values.OrderByDescending(item => item.Load))
            output.WriteLine($"  {info.Teacher.Name}: load {info.Load}, capacity {info.Capacity}, days {string.Join(",", info.Days.Select(day => day.Capacity))}");
        async Task Run(string name, SchedulingInput variant, int seconds)
        {
            var result = await new CpSatSolver().SolveAsync(variant, new GenerationSettings(GenerationModes.Standard, seconds, 1, 2, false), null, CancellationToken.None);
            output.WriteLine($"{name}: {result.Status} first={result.FirstSolutionSeconds} objective={result.Objective} " +
                string.Join(" ", result.Diagnostics?.Findings.Select(finding => $"{finding.Code}:{finding.Entity.Name}") ?? []));
        }
        await Run("as is 10 s", input, 10);
        await Run("as is 60 s", input, 60);
        await Run("no consecutive 10 s", input with { Profile = input.Profile with { DefaultMaxConsecutive = null } }, 10);
        var noSoft = input with { Profile = input.Profile with { Rules = input.Profile.Rules.Select(rule => rule with { Enabled = false }).ToArray() } };
        await Run("no priorities 10 s", noSoft, 10);
    }
}
