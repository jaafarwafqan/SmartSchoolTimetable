# ADR 0038: CP-SAT model, single-stage solve and the measurement gate

- Status: Accepted (Phase 4 MVP delivery)
- Date: 2026-10-09

## Context
PHASE_4_PROMPT asks for a two-stage solve (feasibility, then optimisation with hints) and decomposition. PHASE_4_MVP_PROMPT takes precedence and changes three things:
- No two-stage solve and no decomposition in this delivery; both are recorded as future proposals.
- The gate is 12 sections / 20 teachers to a valid timetable within 30 s, and 24 sections within 60 s.
- Larger sizes are measured and reported, with no promise.

## Decision
- One CP-SAT model and one solve with the weighted objective. The best timetable found is always returned, and cancelling keeps it.
- Boolean `x[line, day, lesson]` exists only for allowed slots: the section's stage lesson count on that day, and not on the teacher's off days or blocked periods or the subject's blocked periods. H1–H10 are hard (docs/SOLVER.md §2). S1–S5 form the objective, using the profile's enabled flags and weights (§3).
- Real time across shifts: a teacher (H4) and a resource (H8) are limited per "moment". For every lesson start time, the moment covers all slots whose clock interval contains it, which covers every overlap of intervals on a line.
- Parameters:
  - Time limit: 60 s by default, 10–600 s allowed.
  - Workers: max(1, cores ÷ 2) by default.
  - Seed: stored.
  - Deterministic mode: one worker, the fixed seed, and `max_deterministic_time` as the limit (ADR 0007).
- Measurement: `PerformanceTests` (`Category=Performance`, run only with `SST_PERFORMANCE=1`) measure the gate on synthetic schools built inside the test project. docs/PERFORMANCE.md records the real numbers.

## Alternatives
- Two-stage with hints, and decomposition by shift or stage group: deferred by the MVP prompt. They are the first things to try if larger schools miss a target (docs/PERFORMANCE.md).
- Softening a hard rule to reach a time target: never.

## Consequences
- The 40-section case is measured and reported honestly with no promise (docs/PERFORMANCE.md).
