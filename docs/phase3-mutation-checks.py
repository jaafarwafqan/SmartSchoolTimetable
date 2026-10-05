"""Mutation sanity checks (Phase 3 finish C1). Each mutant changes one line, runs the relevant tests, and the
original file content is ALWAYS written back (finally). Nothing is committed."""
import json, pathlib, subprocess, sys

ROOT = pathlib.Path(r"C:\Users\jaafar\Desktop\SmartSchoolTimetable")
V = "src/SmartSchoolTimetable.Application/Scheduling/PreSolveValidator.cs"
A = "src/SmartSchoolTimetable.Domain/Scheduling/TeacherAvailability.cs"
H = "src/SmartSchoolTimetable.Application/Scheduling/SchedulingInputHash.cs"
W = "src/SmartSchoolTimetable.Application/Workload/WorkloadService.cs"
M = "src/SmartSchoolTimetable.Infrastructure/Migrations/20261005192744_Phase3EWorkloadWizardStep.cs"
E = "src/SmartSchoolTimetable.Api/Endpoints/Phase3Endpoints.cs"
VAL = "FullyQualifiedName~PreSolveValidator|FullyQualifiedName~ValidatorProperty|FullyQualifiedName~ReadinessTests|FullyQualifiedName~DemoData"

MUTANTS = [
    ("M01 shortage arithmetic", V, 118, "r > a ? r - a : null", "r > a ? r - a + 1 : null", VAL),
    ("M02 section over capacity > to >=", V, 170, "required > available", "required >= available", VAL),
    ("M03 section under capacity < to <=", V, 172, "required < available", "required <= available", VAL),
    ("M04 teacher overload > to >=", V, 207, "required > available", "required >= available", VAL),
    ("M05 subject allowed slots ignore blocks", V, 227, "slots.Count(slot => Context.Allowed(subject, slot))", "slots.Length", VAL),
    ("M06 resource capacity ignored", V, 299, "Math.Min(count, resource.Capacity)", "count", VAL),
    ("M07 double pairs counted twice", V, 349, "pairs++", "pairs += 2", VAL),
    ("M08 unassigned lines never reported", V, 153, "missing.Length == 0", "missing.Length >= 0", VAL),
    ("M09 availability ignores off days", A, 36, "!offDays.Contains(slot.Day) && ", "", VAL + "|FullyQualifiedName~WorkloadTests"),
    ("M10 availability ignores blocked", A, 36, " && !blocked.Contains(new BlockedPeriod(slot.Day, slot.Lesson))", "", VAL + "|FullyQualifiedName~WorkloadTests"),
    ("M11 availability ignores max per day", A, 38, "maxPerDay ?? int.MaxValue", "int.MaxValue", VAL + "|FullyQualifiedName~WorkloadTests"),
    ("M12 availability ignores max per week", A, 39, "Math.Min(byDay, maxPerWeek ?? int.MaxValue)", "byDay", VAL + "|FullyQualifiedName~WorkloadTests"),
    ("M13 hash: teachers not sorted", H, 43, "OrderBy(teacher => teacher.Id)", "AsEnumerable()", "FullyQualifiedName~PreSolveValidator|FullyQualifiedName~SchedulingInputHash|FullyQualifiedName~ReadinessTests"),
    ("M14 hash: NotHashed ignored", H, 63, "member.IsDefined(typeof(NotHashedAttribute), inherit: true)", "false", "FullyQualifiedName~PreSolveValidator|FullyQualifiedName~SchedulingInputHash|FullyQualifiedName~ReadinessTests"),
    ("M15 suggester ignores the teacher limit", W, 193, ">= entry.WeeklyLessons", ">= 0", "FullyQualifiedName~AssignmentSuggester|FullyQualifiedName~DemoData"),
    ("M16 migration mask shift wrong", M, 15, "<< 1", "<< 2", "FullyQualifiedName~Phase3E|FullyQualifiedName~WizardStepMigration|FullyQualifiedName~SetupWizard|FullyQualifiedName~Migration"),
    ("M18 migration Down keeps the workload bit", M, 31, '("CompletedMask" & ~384)', '("CompletedMask" & ~256)', "FullyQualifiedName~WizardStepMigration"),
    ("M17 readiness endpoint without session", E, 60, 'MapOwnerGroup("/academic-years/{yearId:long}/readiness")', 'MapGroup("/api/v1/academic-years/{yearId:long}/readiness")', "FullyQualifiedName~ReadinessTests"),
]

only = set(sys.argv[1:])
results = []
for name, rel, line, old, new, test_filter in MUTANTS:
    if only and name.split()[0] not in only:
        continue
    path = ROOT / rel
    original = path.read_bytes()
    try:
        text = original.decode("utf-8")
        lines = text.split("\n")
        assert lines[line - 1].count(old) == 1, f"{name}: '{old}' not found once on line {line}: {lines[line - 1].strip()}"
        lines[line - 1] = lines[line - 1].replace(old, new)
        path.write_bytes("\n".join(lines).encode("utf-8"))
        run = subprocess.run(["dotnet", "test", str(ROOT / "SmartSchoolTimetable.sln"), "-c", "Debug", "--filter", test_filter],
                             capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT)
        out = run.stdout + run.stderr
        compiled = "error CS" not in out and "error CA" not in out
        summary = [l.strip() for l in out.splitlines() if "Passed!" in l or "Failed!" in l]
        failed_tests = sorted({l.split("[FAIL]")[0].split()[-1] for l in out.splitlines() if "[FAIL]" in l})
        caught = (run.returncode != 0) if compiled else None
        results.append({"mutant": name, "caught": caught, "compiled": compiled, "summary": summary[-1:] , "failing": failed_tests[:6]})
        print(json.dumps(results[-1], ensure_ascii=False), flush=True)
    finally:
        path.write_bytes(original)

(pathlib.Path(__file__).parent / ("mutants-rerun.json" if only else "mutants.json")).write_text(json.dumps(results, ensure_ascii=False, indent=1), encoding="utf-8")
