# ADR 0040: Infeasibility diagnostics with assumption cores

- Status: Accepted (Phase 4)
- Date: 2026-10-09

## Context
The answer "لا يوجد حل" alone is useless to the owner. The pre-solve validator (ADR 0035) only reports proven single-cause shortages. Interactions between several rules reach the solver.

## Decision
- The diagnostics run only after the main solve proves INFEASIBLE. They build a separate diagnostic model:
  - Variables exist for every lesson of the shift.
  - Each restriction family is enforced only under its own assumption literal:
    - a teacher's availability (off days, blocked periods, release);
    - a teacher's limits;
    - a section's packing;
    - a stage's day counts;
    - a subject's blocked periods;
    - a subject's daily cap;
    - a resource's capacity;
    - a subject's double periods.
  - H1 (workload), H2 (section clash) and H4 (teacher clash) are always enforced, because they define what a timetable is.
- Core: CP-SAT returns a sufficient set of families (`SufficientAssumptionsForInfeasibility`). The set is shrunk by deletion: drop a family when the rest is still impossible. The budget is 25% of the time limit. When the budget ends first, the best core is returned with `Minimal = false`.
- Relaxation hints: each family of the core is tested alone, with everything else enforced. "Relaxing this makes the timetable possible" is true, false, or untested.
- Findings carry:
  - a stable code (`DiagnosticCodes`);
  - the entity and related entities;
  - numbers where computable (lessons, available slots, capacity);
  - fix codes shared with the readiness screen, so the frontend shows Arabic text and links.
- A timeout without any timetable is `TimedOut` with the advice `TIMEOUT_NO_SOLUTION`. It is never reported as "infeasible".

## Consequences
- Diagnostics add up to 25% of the time limit after an infeasible answer.
- Scenarios (a) to (d) are tests: `DiagnosticsTests` and `SolverExecutionTests.ATimeLimitWithoutASolutionIsTimedOutNotInfeasible`.
