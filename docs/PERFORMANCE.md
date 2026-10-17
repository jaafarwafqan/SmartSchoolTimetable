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

## M3 (2026-10-11): the published program on school-PC limits
The solver's default worker count now follows the computer: all cores but one, at most 4, and at most 2 when the computer has under 6 GB of memory; CP-SAT also gets a memory safety net (a quarter of the memory, 512 MB to 2 GB). Measured with the published executable through the API (`e2e/m3-performance.spec.ts`, on demand), a fictional intermediate school, 60 s limit, machine: i7-7500U 2.7 GHz (2 cores / 4 threads), 15.9 GB, Windows 10 22H2.

| Profile | Sections | Workers | First (s) | Total (s) | Status | Server peak | Whole test (runner + server + browser) peak |
|---|---:|---:|---:|---:|---|---:|---:|
| normal | 24 | 3 | 7.1 | 27.6 | optimal (penalty 0) | 400 MB | 722 MB |
| normal | 40 | 3 | 12.4 | 62.4 | feasible (penalty 25) | 513 MB | 849 MB |
| 2 processors + 512 MB heap limit | 24 | 2 | 7.7 | 29.7 | optimal | 363 MB | 676 MB |
| 2 processors + 512 MB heap limit | 40 | 2 | 11.2 | 62.2 | feasible (penalty 415) | 447 MB | 786 MB |

Cold start (server start to first answer on a new database): 5.0 s; 12.9 s on the first start after a publish. Details, limits and how to repeat: `docs/M3_RELEASE_VALIDATION_AR.md`.


## Phase 5 generation rules (2026-10-16, ADR 0053)
Every new rule on with the default priorities: «أقصى حصص متتالية» = 3 as a priority «مهم جداً» (the owner's final decision), spread with gaps, balanced days and fair last periods. Standard mode, 60 s, **2 workers** (the default of a 4 GB school PC), seed 12345. `PerformanceTests.Phase5RulesKeepTheGate`, Release build, same machine as above.

| School | Sections | Teachers | Workers | Status | First (s) | Total (s) | Objective | Bound | Violations | Memory |
|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---|
| 24/40 s1 P5 | 24 | 40 | 2 | Feasible | 3.50 | 61.89 | 16970 | 6690 | 0 | 67 → 224 MB |
| 24/40 s2 P5 | 24 | 40 | 2 | Feasible | 2.15 | 60.32 | 16080 | 6965 | 0 | 109 → 235 MB |
| 24/40 s3 P5 | 24 | 40 | 2 | Feasible | 2.17 | 60.31 | 15685 | 6530 | 0 | 111 → 241 MB |
| owner-shaped s1 | 12 | 22 | 2 | Feasible | 0.89 | 60.10 | 10780 | 5860 | 0 | 117 → 241 MB |
| owner-shaped s2 | 12 | 22 | 2 | Feasible | 0.97 | 60.09 | 11385 | 6095 | 0 | 114 → 241 MB |
| owner-shaped s3 | 12 | 22 | 2 | Feasible | 0.81 | 60.09 | 10805 | 5925 | 0 | 114 → 241 MB |

- **The gate still holds:** 24/40 found its first timetable in 2.2–3.5 s (target 60 s). The objective has more terms than in Phase 4, so it is not comparable with those numbers.
- **Owner-shaped school** (`SyntheticSchools.OwnerShaped`, fictional; not the owner's data): 12 sections in 3 grades, 30 lessons in 30 slots, 22 teachers, about a third with a day off, up to 2 blocked periods each, and evening sessions Tuesday–Thursday. Readiness was clean on all three seeds.
- Remaining penalties after 60 s (s1 / s2 / s3), measured honestly:

  | Rule | s1 | s2 | s3 |
  |---|---:|---:|---:|
  | runs above the consecutive limit (a priority) | 0 | 3 | 0 |
  | balanced days (sum of excess over max − min ≤ 1) | 3 | 6 | 3 |
  | last periods above the share | 6 | 8 | 8 |
  | most / fewest last periods of one teacher | 4 / 1 | 4 / 1 | 4 / 1 |
  | adjacent lessons of a teacher | 105 | 107 | 111 |

  An earlier run with the limit as a hard rule had 0 runs over the limit on all three seeds. As a priority the solver may trade a few runs for other rules within 60 s (s2: 3), and the result notes name them.
  In full sections, a teacher with 18 lessons in 5 days must have some lessons back to back; the adjacency count shows that limit, not a fault. No run proved optimality in 60 s.
- **Why a priority:** with the limit hard, a fictional primary school with class teachers (24–29 lessons each in their own section; the user guide's school) found no timetable in 60 s, and with the limit off it found one in 0.8 s (ADR 0053 §2).

The Phase 4 gate tests, re-run with the Phase 5 rules (their default profile; no consecutive limit, default workers = 3 on this machine):

| School | Sections | Teachers | Workers | Status | First (s) | Total (s) | Objective | Bound | Violations | Memory |
|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---|
| 12/20 s1 | 12 | 20 | 3 | Feasible | 1.00 | 60.12 | 7750 | 3585 | 0 | 115 → 270 MB |
| 12/20 s2 | 12 | 20 | 3 | Feasible | 0.81 | 60.09 | 8005 | 3585 | 0 | 116 → 270 MB |
| 12/20 s3 | 12 | 20 | 3 | Feasible | 1.11 | 60.10 | 7600 | 3530 | 0 | 119 → 270 MB |
| 24/40 s1 | 24 | 40 | 3 | Feasible | 2.05 | 60.42 | 16670 | 6690 | 0 | 66 → 267 MB |
| 24/40 s2 | 24 | 40 | 3 | Feasible | 2.01 | 60.30 | 16085 | 6965 | 0 | 110 → 270 MB |
| 24/40 s3 | 24 | 40 | 3 | Feasible | 2.00 | 60.32 | 16420 | 6530 | 0 | 112 → 270 MB |

The 4 GB profile was measured on the solver (2 workers, the default below 6 GB). The published-program measurement under a 512 MB heap limit (`m3-performance.spec.ts`) was not repeated for Phase 5.

## Every subject spread across days (2026-10-17, ADR 0055)

`Phase5RulesKeepTheGate` re-run after S1 began to apply to every subject (all subjects spread, weight 40, max 3 in a row as a priority, 2 workers, 60 s):

| School | Sections | Teachers | Workers | Status | First (s) | Total (s) | Objective | Bound | Violations | Memory |
|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---|
| 24/40 s1 P5 | 24 | 40 | 2 | Feasible | 2.60 | 60.51 | 18150 | 7530 | 0 | 68 → 237 MB |
| 24/40 s2 P5 | 24 | 40 | 2 | Feasible | 2.35 | 60.35 | 17255 | 7870 | 0 | 108 → 247 MB |
| 24/40 s3 P5 | 24 | 40 | 2 | Feasible | 3.02 | 60.43 | 17370 | 7270 | 0 | 116 → 248 MB |
| owner-shaped s1 | 12 | 22 | 2 | Feasible | 2.49 | 60.17 | 11755 | 6565 | 0 | 118 → 248 MB |
| owner-shaped s2 | 12 | 22 | 2 | Feasible | 1.77 | 60.10 | 11955 | 6875 | 0 | 111 → 248 MB |
| owner-shaped s3 | 12 | 22 | 2 | Feasible | 0.97 | 60.14 | 11765 | 6525 | 0 | 116 → 248 MB |
| owner-shaped 11 s1 | 11 | 22 | 2 | Feasible | 1.37 | 60.07 | 9780 | 5645 | 0 | 118 → 248 MB |

Same-day repeats on the 11-section owner-shaped school (60 s, 2 workers): 30–37 unavoidable on every run; improvable 31–45 with S1 effectively
off (the bug), 2–7 with the fix, 0–2 with a weight of 120, 0 (in 30 s) with S1 as the only priority. The first timetable still comes in 1–3 s.
