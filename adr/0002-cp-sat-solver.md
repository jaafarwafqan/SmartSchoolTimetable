# ADR 0002: Use CP-SAT for scheduling

- Status: Accepted
- Date: 2026-10-03

## Context
Scheduling must satisfy hard constraints, scale to school-sized workloads, provide explainable diagnostics, and integrate with the approved .NET backend.

## Decision
Use Google OR-Tools CP-SAT through the C# NuGet package in a separate .NET worker. Keep the `ISolver` contract in Application and its OR-Tools adapter in Infrastructure. The Domain and Application never depend on OR-Tools types. Do not move the solver to a Python worker.

## Consequences
- Clean architecture separation.
- The API and worker share .NET types, validation rules, and deployment tooling; there is no cross-language solver protocol to version or support.
- A separate process/container still isolates CPU- and memory-intensive solving from HTTP requests.
- Python would provide familiar CP-SAT examples and rapid experimentation, but would introduce a second runtime, duplicate model/domain representations, a serialization boundary, and additional deployment and operational support.
- The required .NET 9 and NuGet packages were available and the C# spike compiled and ran, removing the earlier toolchain blocker.

## Phase 0 C# spike

The isolated spike uses Google.OrTools 9.15.6755 and a fixed teacher/subject workload input. It includes:

- Hard constraints: one lesson per section slot, no teacher overlap, exact weekly subject workload, teacher off days and blocked periods, teacher daily and weekly maximums, and shared Laboratory-room capacity of one per period.
- Soft objective terms: teacher gaps (weight 30), repeated same-day lessons to encourage spreading (weight 20), and adjacent same-subject lessons as a preferred double period (weight 10).
- An assumption-core infeasibility case for Ahmed: 28 required lessons versus 25 available slots. CP-SAT returned an infeasibility core naming the demand and availability groups; the diagnostic reports a shortage of 3 and recommends reducing workload or freeing 3 slots.

| Sections / teachers | Workers | Status | Build | Solve | Total | Working-set delta |
|---|---:|---|---:|---:|---:|---:|
| 12 / 20 | 1 | Feasible | 0.164 s | 2.844 s | 3.151 s | 29.1 MB |
| 12 / 20 | 8 | Feasible | 0.155 s | 2.127 s | 2.440 s | 30.9 MB |
| 40 / 20 | 1 | Infeasible | 0.231 s | 0.367 s | 0.836 s | 46.6 MB |
| 40 / 20 | 8 | Infeasible | 0.240 s | 0.258 s | 0.737 s | 46.6 MB |

The 40-section / 20-teacher case is infeasible by the hard workload limits: 40 sections require 1,600 lessons per week, while 20 teachers capped at 30 lessons each can supply at most 600. At least 54 teachers are required by that weekly-cap bound alone; availability and other restrictions can raise the actual minimum. This is a capacity finding, not a solver-performance pass for a feasible 40-section instance. Re-run the large performance case with a feasible teacher roster before accepting its target.

An exploratory 40-section / 54-teacher run (the aggregate weekly-cap lower bound) reached the 30-second limit without a feasible incumbent (`UNKNOWN`). It is not included as a successful performance result; the current fixed teacher/subject workload allocation and soft objective need more work for the large case.

Benchmark runs stop after the first feasible solution, so their timings measure feasibility search and their objective scores are incumbent values, not optimality claims. Working-set deltas are process working-set observations, not peak native-memory profiles. Multi-worker objective values differ; only single-worker mode is intended for reproducibility.
