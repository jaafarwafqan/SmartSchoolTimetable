# ADR 0039: Independent timetable verifier

- Status: Accepted (Phase 4)
- Date: 2026-10-09

## Context
A modelling mistake in the solver could produce a timetable that breaks a rule while the solver reports success. Manual edits (Phase 4 M4) also need the same hard-rule check.

## Decision
- `TimetableVerifier` (`Application/Generation`) is pure. It re-checks H1–H10 on any list of lessons using only the raw `SchedulingInput`. It shares no code with the CP-SAT model: real-time overlap, pairing and caps are written again there on purpose.
- Every solver timetable passes it before anything is saved (`GenerationEngine`). A violation is a defect: the run fails with `TIMETABLE_VERIFICATION_FAILED` and nothing is saved.
- Violations carry stable codes (`ViolationCodes`) and numbers. The editor shows them in Arabic.
- `TimetableScorer` computes the S1–S5 breakdown from the lessons using the same definitions as the objective. For a proven-optimal timetable its total equals the solver objective (`SoftRuleTests`).

## Consequences
- Tests:
  - `VerifierTests` is a catalogue of broken timetables, each caught with its exact code.
  - `SolverPropertyTests` checks that every solver timetable on random schools has zero violations.
- Changing a hard rule means changing both the model and the verifier, with a test on each side.
