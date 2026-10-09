# Performance (Phase 4)

Every number below was measured with `PerformanceTests` (`Category=Performance`). Nothing is estimated.

**Machine:**
- CPU: Intel Core i7-7500U @ 2.70 GHz, 2 cores / 4 logical processors.
- RAM: 15.9 GB.
- OS: Windows 10 Pro 10.0.19045.
- .NET SDK 9.0.318, Release build.
- OR-Tools 9.15.6755.

**Schools:** synthetic schools built inside the test project by `SyntheticSchools.Realistic(sections, teachers, seed)`; there is no demo data in the product. Each school has:
- one shift of 6 lessons × 5 days with a break after lesson 3, and 28 lessons a week per section;
- 3 stages per 12 sections and 9 subjects, including heavy and spread subjects, one laboratory subject (capacity sections ÷ 12) and one double-period subject;
- off days for some lightly loaded teachers, 0–2 random blocked periods per teacher, and daily limits.

All hard constraints apply in full.

**Columns:**
- Status: the CP-SAT outcome.
- First: seconds to the first valid timetable.
- Total: wall time of the whole solve (the search runs until the limit to improve the score).
- Objective / bound: penalty of the best timetable / proven lower bound.
- Violations: the independent `TimetableVerifier` on the result.
- Memory: process working set before → peak. This covers the whole test host, not only the solver.

## Gate (PHASE_4_MVP), default settings

Default settings: 2 workers on this machine (logical cores ÷ 2), 60 s limit, standard mode, seed 12345.

| School | Sections | Teachers | Workers | Status | First (s) | Total (s) | Objective | Bound | Violations | Memory |
|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---|
| 12/20 s1 | 12 | 20 | 2 | Feasible | 0.87 | 60.11 | 4065 | 3585 | 0 | 110 → 230 MB |
| 12/20 s2 | 12 | 20 | 2 | Feasible | 1.80 | 60.09 | 4315 | 3585 | 0 | 109 → 230 MB |
| 12/20 s3 | 12 | 20 | 2 | Feasible | 0.79 | 60.09 | 3950 | 3555 | 0 | 112 → 230 MB |
| 24/40 s1 | 24 | 40 | 2 | Feasible | 3.20 | 60.51 | 9000 | 6750 | 0 | 64 → 225 MB |
| 24/40 s2 | 24 | 40 | 2 | Feasible | 1.81 | 60.31 | 8985 | 7010 | 0 | 102 → 230 MB |
| 24/40 s3 | 24 | 40 | 2 | Feasible | 2.01 | 60.29 | 8615 | 6570 | 0 | 108 → 230 MB |

- **12 sections / 20 teachers: the 30 s target is met.** The slowest of the three schools found its first timetable after 1.80 s.
- **24 sections: the 60 s target is met.** The slowest found its first after 3.20 s.

None of these runs proved optimality within 60 s. The gap between the objective and the bound is reported as measured.

## Larger schools (no promise)

| School | Sections | Teachers | Workers | Status | First (s) | Total (s) | Objective | Bound | Violations | Memory |
|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---|
| 40/54 | 40 | 54 | 2 | Feasible | 3.36 | 120.53 | 16235 | 12800 | 0 | 101 → 312 MB |
| 40/60 | 40 | 60 | 2 | Feasible | 3.71 | 120.62 | 14300 | 10160 | 0 | 113 → 321 MB |

The Phase 0 spike risk (ADR 0002: 40/54 with no timetable in 30 s) did not show on these synthetic schools. Real data with tighter teacher availability can be harder. This is one seed per size, so treat it as an observation, not a guarantee.

## «نتيجة قابلة للإعادة» (deterministic mode: 1 worker, deterministic time)

| School | Workers | Limit | Status | First (s) | Total wall (s) | Objective | Bound | Violations |
|---|---:|---|---|---:|---:|---:|---:|---:|
| 12/20 s1 | 1 | 60 deterministic units | Feasible | 34.66 | 150.76 | 8675 | 3585 | 0 |
| 24/40 s1 | 1 | 60 deterministic units | TimedOut (no timetable) | – | 196.04 | – | 6750 | – |

- **The deterministic mode is much slower.** On this machine:
  - 60 deterministic units took 150–196 s of wall time.
  - 12/20 found its first timetable after 34.7 s, which misses 30 s.
  - 24/40 found none within the limit.
- The screen says so in the option's hint. Owners who need reproducibility on a large school should raise the time limit.
- These were measured before the gate change that moved the deterministic runs out of the gate test, with the same model and parameters.

## Strategies

PHASE_4_MVP excludes the two-stage solve and decomposition from this delivery. The model is one weighted CP-SAT solve, in which:
- variables exist only for allowed slots;
- H3 uses a prefix (packing) constraint;
- teacher and resource limits apply per real-time "moment".

If a real school misses a target, try these in this order and measure each:
1. More workers (the advanced option).
2. A feasibility-first stage with its solution as a hint (two-stage).
3. Decomposition by shift or stage groups that share no teacher or resource.
4. Search parameters, for example `interleave_search` for a faster reproducible mode, which would change ADR 0007.

## How to run

```powershell
$env:SST_PERFORMANCE = "1"
dotnet test tests/SmartSchoolTimetable.Tests -c Release --filter "Category=Performance" --logger "console;verbosity=detailed"
```

Each measurement is printed as one table row in the test output. Without `SST_PERFORMANCE=1` these tests are skipped, so the default `dotnet test` run stays fast.
