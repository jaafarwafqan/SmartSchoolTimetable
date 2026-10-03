using System.Diagnostics;
using Google.OrTools.Sat;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

internal static class Program
{
    private const int Days = 5;
    private const int PeriodsPerDay = 8;
    private const int WeeklyCapacity = Days * PeriodsPerDay;
    private const int TeacherGapWeight = 30;
    private const int SubjectSpreadWeight = 20;
    private const int DoubleLessonWeight = 10;

    private static readonly int[] SubjectWeeklyLoads = [1, 10, 10, 10, 9];
    private static readonly string[] SubjectNames = ["Laboratory", "Mathematics", "Arabic", "Science", "English"];

    public static int Main(string[] args)
    {
        if (args.Contains("--pdf", StringComparer.OrdinalIgnoreCase))
        {
            GenerateArabicPdf();
            return 0;
        }

        if (args.Length == 4 && args[0] == "--benchmark")
        {
            var result = RunBenchmark(int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]));
            PrintBenchmark(result);
            return 0;
        }

        var results = new List<BenchmarkResult>();
        foreach (var (sections, teachers) in new[] { (12, 20), (40, 20) })
        {
            results.Add(RunBenchmark(sections, teachers, 1));
            results.Add(RunBenchmark(sections, teachers, 8));
        }

        Console.WriteLine("CP-SAT scheduling benchmark (wall time includes model build and solve):");
        results.ForEach(PrintBenchmark);
        RunInfeasibilityDiagnostic();
        return 0;
    }

    private static void PrintBenchmark(BenchmarkResult result)
    {
        Console.WriteLine(
            $"{result.Sections} sections / {result.Teachers} teachers, " +
            $"{result.Workers} worker(s): {result.Status}; build={result.BuildSeconds:F3}s; " +
            $"solve={result.SolveSeconds:F3}s; total={result.TotalSeconds:F3}s; " +
            $"working-set delta={result.WorkingSetDeltaMb:F1} MB; " +
            $"objective={result.Objective}; assignments={result.Assignments}");
    }

    private static BenchmarkResult RunBenchmark(int sectionCount, int teacherCount, int workers)
    {
        var process = Process.GetCurrentProcess();
        process.Refresh();
        var initialWorkingSet = process.WorkingSet64;
        var totalTimer = Stopwatch.StartNew();

        var model = BuildSchedulingModel(sectionCount, teacherCount, out var assignmentVariables);
        var buildSeconds = totalTimer.Elapsed.TotalSeconds;
        var modelValidation = model.Validate();
        if (!string.IsNullOrEmpty(modelValidation))
            throw new InvalidOperationException($"CP-SAT model validation failed: {modelValidation}");

        var solver = new CpSolver
        {
            StringParameters = $"max_time_in_seconds:30 num_search_workers:{workers} random_seed:12345 stop_after_first_solution:true"
        };
        var solveTimer = Stopwatch.StartNew();
        var status = solver.Solve(model);
        solveTimer.Stop();
        totalTimer.Stop();
        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
            Console.WriteLine(solver.ResponseStats());

        process.Refresh();
        var workingSetDelta = Math.Max(0, process.WorkingSet64 - initialWorkingSet) / 1024d / 1024d;
        return new BenchmarkResult(
            sectionCount,
            teacherCount,
            workers,
            status.ToString(),
            buildSeconds,
            solveTimer.Elapsed.TotalSeconds,
            totalTimer.Elapsed.TotalSeconds,
            workingSetDelta,
            status is CpSolverStatus.Optimal or CpSolverStatus.Feasible
                ? solver.ObjectiveValue
                : double.NaN,
            status is CpSolverStatus.Optimal or CpSolverStatus.Feasible
                ? assignmentVariables.Count(variable => solver.BooleanValue(variable))
                : 0,
            solver.ResponseStats());
    }

    private static CpModel BuildSchedulingModel(
        int sectionCount,
        int teacherCount,
        out List<BoolVar> allAssignments)
    {
        var model = new CpModel();
        allAssignments = [];
        var subjects = SubjectWeeklyLoads.Length;
        var teacherForSubject = AllocateTeachers(sectionCount, teacherCount);
        var assignments = new BoolVar[sectionCount, subjects, Days, PeriodsPerDay];

        for (var section = 0; section < sectionCount; section++)
        for (var subject = 0; subject < subjects; subject++)
        for (var day = 0; day < Days; day++)
        for (var period = 0; period < PeriodsPerDay; period++)
        {
            var assignment = model.NewBoolVar($"x_s{section}_sub{subject}_d{day}_p{period}");
            assignments[section, subject, day, period] = assignment;
            allAssignments.Add(assignment);

            var teacher = teacherForSubject[section, subject];
            if (day == teacher % Days ||
                (day == (teacher + 1) % Days && period == teacher % PeriodsPerDay))
                model.Add(assignment == 0);
        }

        // Each section has exactly one lesson in every weekly slot.
        for (var section = 0; section < sectionCount; section++)
        for (var day = 0; day < Days; day++)
        for (var period = 0; period < PeriodsPerDay; period++)
        {
            var sectionSlot = new List<LinearExpr>();
            for (var subject = 0; subject < subjects; subject++)
                sectionSlot.Add(assignments[section, subject, day, period]);
            model.Add(LinearExpr.Sum(sectionSlot) == 1);
        }

        // Subject workloads are exact; each section/subject is assigned to one fixed teacher input.
        for (var section = 0; section < sectionCount; section++)
        for (var subject = 0; subject < subjects; subject++)
        {
            var weeklyAssignments = new List<LinearExpr>();
            for (var day = 0; day < Days; day++)
            for (var period = 0; period < PeriodsPerDay; period++)
                weeklyAssignments.Add(assignments[section, subject, day, period]);
            model.Add(LinearExpr.Sum(weeklyAssignments) == SubjectWeeklyLoads[subject]);
        }

        var teacherOccupied = new BoolVar[teacherCount, Days, PeriodsPerDay];
        var teacherWeeklyAssignments = new List<LinearExpr>[teacherCount];
        for (var teacher = 0; teacher < teacherCount; teacher++)
            teacherWeeklyAssignments[teacher] = [];

        for (var teacher = 0; teacher < teacherCount; teacher++)
        for (var day = 0; day < Days; day++)
        for (var period = 0; period < PeriodsPerDay; period++)
        {
            var slotAssignments = new List<LinearExpr>();
            for (var section = 0; section < sectionCount; section++)
            for (var subject = 0; subject < subjects; subject++)
            {
                if (teacherForSubject[section, subject] != teacher)
                    continue;
                slotAssignments.Add(assignments[section, subject, day, period]);
            }

            var occupied = model.NewBoolVar($"occupied_t{teacher}_d{day}_p{period}");
            model.Add(LinearExpr.Sum(slotAssignments) == occupied);
            teacherOccupied[teacher, day, period] = occupied;
            teacherWeeklyAssignments[teacher].Add(occupied);
        }

        // Teacher conflicts and configured maximums are hard constraints.
        for (var teacher = 0; teacher < teacherCount; teacher++)
        {
            model.Add(LinearExpr.Sum(teacherWeeklyAssignments[teacher]) <= 30);
            for (var day = 0; day < Days; day++)
            {
                var dailyAssignments = Enumerable.Range(0, PeriodsPerDay)
                    .Select(period => (LinearExpr)teacherOccupied[teacher, day, period]);
                model.Add(LinearExpr.Sum(dailyAssignments) <= PeriodsPerDay);
            }
        }

        // Laboratory lessons require a single shared room with capacity one per period.
        for (var day = 0; day < Days; day++)
        for (var period = 0; period < PeriodsPerDay; period++)
        {
            var sharedRoomUsage = new List<LinearExpr>();
            for (var section = 0; section < sectionCount; section++)
                sharedRoomUsage.Add(assignments[section, 0, day, period]);
            model.Add(LinearExpr.Sum(sharedRoomUsage) <= 1);
        }

        var objectiveTerms = new List<LinearExpr>();

        // Penalize each empty period between two lessons for the same teacher/day.
        for (var teacher = 0; teacher < teacherCount; teacher++)
        for (var day = 0; day < Days; day++)
        for (var period = 1; period < PeriodsPerDay - 1; period++)
        {
            var hasEarlierLesson = model.NewBoolVar($"before_t{teacher}_d{day}_p{period}");
            var hasLaterLesson = model.NewBoolVar($"after_t{teacher}_d{day}_p{period}");
            model.AddMaxEquality(
                hasEarlierLesson,
                Enumerable.Range(0, period).Select(p => (LinearExpr)teacherOccupied[teacher, day, p]));
            model.AddMaxEquality(
                hasLaterLesson,
                Enumerable.Range(period + 1, PeriodsPerDay - period - 1)
                    .Select(p => (LinearExpr)teacherOccupied[teacher, day, p]));

            var gap = model.NewBoolVar($"gap_t{teacher}_d{day}_p{period}");
            model.AddBoolAnd([
                hasEarlierLesson,
                hasLaterLesson,
                teacherOccupied[teacher, day, period].Not()
            ]).OnlyEnforceIf(gap);
            model.AddBoolOr([
                hasEarlierLesson.Not(),
                hasLaterLesson.Not(),
                teacherOccupied[teacher, day, period]
            ]).OnlyEnforceIf(gap.Not());
            objectiveTerms.Add(gap * TeacherGapWeight);
        }

        // Penalize repeated daily lessons of a subject beyond the first to encourage spreading.
        for (var section = 0; section < sectionCount; section++)
        for (var subject = 0; subject < subjects; subject++)
        for (var day = 0; day < Days; day++)
        {
            var dayAssignments = Enumerable.Range(0, PeriodsPerDay)
                .Select(period => (LinearExpr)assignments[section, subject, day, period])
                .ToList();
            var hasSubjectThatDay = model.NewBoolVar($"spread_s{section}_sub{subject}_d{day}");
            var dailyCount = LinearExpr.Sum(dayAssignments);
            model.Add(dailyCount >= hasSubjectThatDay);
            model.Add(dailyCount <= SubjectWeeklyLoads[subject] * hasSubjectThatDay);
            objectiveTerms.Add((dailyCount - hasSubjectThatDay) * SubjectSpreadWeight);
        }

        // Reward adjacent lessons of the same subject as a preferred double period.
        for (var section = 0; section < sectionCount; section++)
        for (var subject = 0; subject < subjects; subject++)
        for (var day = 0; day < Days; day++)
        for (var period = 0; period < PeriodsPerDay - 1; period++)
        {
            var left = assignments[section, subject, day, period];
            var right = assignments[section, subject, day, period + 1];
            var doubleLesson = model.NewBoolVar($"double_s{section}_sub{subject}_d{day}_p{period}");
            model.AddBoolAnd([left, right]).OnlyEnforceIf(doubleLesson);
            model.AddBoolOr([left.Not(), right.Not()]).OnlyEnforceIf(doubleLesson.Not());
            objectiveTerms.Add(doubleLesson * -DoubleLessonWeight);
        }

        model.Minimize(LinearExpr.Sum(objectiveTerms));
        return model;
    }

    private static int[,] AllocateTeachers(int sectionCount, int teacherCount)
    {
        var teacherForSubject = new int[sectionCount, SubjectWeeklyLoads.Length];
        var teacherLoads = new int[teacherCount];
        var subjectOrder = Enumerable.Range(0, SubjectWeeklyLoads.Length)
            .OrderByDescending(subject => SubjectWeeklyLoads[subject])
            .ToArray();

        var sectionSubjects = Enumerable.Range(0, sectionCount)
            .SelectMany(section => subjectOrder.Select(subject => (Section: section, Subject: subject)))
            .OrderByDescending(item => SubjectWeeklyLoads[item.Subject])
            .ThenBy(item => item.Section)
            .ThenBy(item => item.Subject);
        foreach (var (section, subject) in sectionSubjects)
        {
            var teacher = Enumerable.Range(0, teacherCount)
                .OrderBy(candidate => teacherLoads[candidate])
                .ThenBy(candidate => candidate)
                .First();
            teacherForSubject[section, subject] = teacher;
            teacherLoads[teacher] += SubjectWeeklyLoads[subject];
        }

        return teacherForSubject;
    }

    private static void RunInfeasibilityDiagnostic()
    {
        const int requiredLessons = 28;
        const int availableSlots = 25;
        const string teacherName = "Ahmed";

        var model = new CpModel();
        var lessons = Enumerable.Range(0, WeeklyCapacity)
            .Select(slot => model.NewBoolVar($"teacher_{teacherName}_slot_{slot}"))
            .ToArray();
        var demand = model.NewBoolVar($"required_lessons_{teacherName}_28");
        var availability = model.NewBoolVar($"available_slots_{teacherName}_25");

        model.Add(LinearExpr.Sum(lessons) == requiredLessons).OnlyEnforceIf(demand);
        model.Add(LinearExpr.Sum(lessons.Take(availableSlots)) <= availableSlots).OnlyEnforceIf(availability);
        for (var slot = availableSlots; slot < lessons.Length; slot++)
            model.Add(lessons[slot] == 0).OnlyEnforceIf(availability);
        model.AddAssumption(demand);
        model.AddAssumption(availability);

        var solver = new CpSolver { StringParameters = "max_time_in_seconds:10 num_search_workers:1 random_seed:12345" };
        var status = solver.Solve(model);
        Console.WriteLine();
        Console.WriteLine($"Infeasibility diagnostic: {status}");

        if (status != CpSolverStatus.Infeasible)
            throw new InvalidOperationException("The expected teacher-capacity infeasibility was not demonstrated.");

        var core = solver.SufficientAssumptionsForInfeasibility();
        var coreIndexes = core.Select(literal => literal >= 0 ? literal : -literal - 1).ToHashSet();
        var implicatedDemand = coreIndexes.Contains(demand.Index);
        var implicatedAvailability = coreIndexes.Contains(availability.Index);
        if (!implicatedDemand || !implicatedAvailability)
            throw new InvalidOperationException("The infeasibility core omitted the expected demand or availability group.");

        Console.WriteLine(
            $"Conflict core: {teacherName} weekly demand ({requiredLessons}) and " +
            $"availability ({availableSlots} slots). Shortage: {requiredLessons - availableSlots} lessons.");
        Console.WriteLine(
            $"Suggested fix: reduce {teacherName}'s assigned workload by at least " +
            $"{requiredLessons - availableSlots}, or free up at least that many slots.");
    }

    private static void GenerateArabicPdf()
    {
        var fontPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fonts", "NotoNaskhArabic-Regular.ttf"));
        if (!File.Exists(fontPath))
            throw new FileNotFoundException("Expected the Phase 0 Noto Naskh Arabic font next to the spike sources.", fontPath);

        QuestPDF.Settings.License = LicenseType.Community;
        FontManager.RegisterFontFromFile(fontPath);
        var outputPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "rtl_questpdf_sample.pdf"));

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontFamily("Noto Naskh Arabic").FontSize(14));

                page.Content().ContentFromRightToLeft().Column(column =>
                {
                    column.Spacing(14);
                    column.Item().AlignRight().Text("جدول الحصص - الصف الأول").FontSize(22).Bold();
                    column.Item().AlignRight().Text("المرحلة المتوسطة - الفصل الأول");
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(3);
                        });

                        foreach (var heading in new[] { "اليوم", "الحصة الأولى", "الحصة الثانية", "الحصة الثالثة" })
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(8).AlignRight().Text(heading).Bold();

                        foreach (var row in new[]
                        {
                            new[] { "الاثنين", "اللغة العربية", "الرياضيات", "العلوم" },
                            new[] { "الثلاثاء", "الفيزياء", "اللغة الإنجليزية", "التربية البدنية" },
                            new[] { "الأربعاء", "الكيمياء", "التاريخ", "المكتبة" }
                        })
                        foreach (var cell in row)
                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).AlignRight().Text(cell);
                    });
                });
            });
        }).GeneratePdf(outputPath);

        Console.WriteLine($"QuestPDF sample: {outputPath}");
        Console.WriteLine($"Registered Arabic font: {Path.GetFileName(fontPath)}");
    }

    private sealed record BenchmarkResult(
        int Sections,
        int Teachers,
        int Workers,
        string Status,
        double BuildSeconds,
        double SolveSeconds,
        double TotalSeconds,
        double WorkingSetDeltaMb,
        double Objective,
        int Assignments,
        string SolverStats);
}
