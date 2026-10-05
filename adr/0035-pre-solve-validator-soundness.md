# ADR 0035: Conservative pre-solve validator

- Status: Accepted for Phase 3D.
- Date: 2026-10-05

## Context
The readiness report runs before Phase 4's CP-SAT solver. A false blocking error would incorrectly tell the owner that no timetable can exist. This is more harmful than a warning that leaves an actual conflict for the solver to discover.

## Decision
- Validate only the pure `SchedulingInput`; results are deterministic and grouped by affected entity.
- Emit an error only when a necessary demand exceeds a proven upper bound (section capacity, teacher slot/load capacity, subject-allowed slots, resource capacity per shift/slot, or consecutive pairs for double lessons). A pair cannot cross a break row. An assignment to an archived or fully released teacher is also an error.
- Emit warnings for uncertain conditions such as partial release, overlap, disabled distribution, orphan blocked periods, an empty stage, and tight-but-possible double periods.
- Use stable codes, severity, entity references, numeric details and fix codes. Arabic wording and links are client dictionary data.
- Do not claim completeness: some jointly interacting constraints can be missed. The validator is not a solver and never places lessons.

## Alternatives
- Treat every aggregate shortage as an error: rejected unless the calculation is a necessary bound for the relevant groups and slot model.
- Attempt a hand-written full constraint solver: rejected; Phase 4 owns CP-SAT feasibility and conflict analysis.
- Warn on every condition: rejected because provable impossibility should be actionable before generation.

## Consequences
- Property tests construct feasible timetables and verify zero errors, then inject a known exact teacher shortage.
- Readiness is a useful conservative preflight; final feasibility remains the solver's responsibility in Phase 4.
