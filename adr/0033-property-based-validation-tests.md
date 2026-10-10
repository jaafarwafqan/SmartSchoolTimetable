# ADR 0033: Property-based validation tests

- Status: Accepted for Phase 3D tests only; FsCheck does not enter the shipped application. Checked against the code in the Phase 3 finish (300 runs per property, `ValidatorPropertyTests.Runs`).
- Date: 2026-10-05

## Context
The pre-solve validator must be conservative: a valid timetable must not produce a blocking finding. Example-based tests alone cover known cases but do not explore varied combinations of sections, shifts, days, loads, blocked periods and resources.

## Decision
- Use `FsCheck.Xunit` in the test project to generate small valid weekly placements first, then derive curriculum, assignments, availability and resource constraints from those placements.
- Assert that each derived feasible input has zero errors. In a second property, reduce one teacher's weekly limit below the generated load and require exactly that teacher and exact shortage to be reported.
- Keep the generator deterministic per seed and bounded to 300 runs per property in CI.
- Package: FsCheck.Xunit 3.4.0; license: MIT; maintained open-source project. It is referenced only by `SmartSchoolTimetable.Tests`.

## Alternatives
- Hand-authored cases only: rejected because they do not exercise the combinatorial space that threatens soundness.
- A custom randomized loop: rejected because shrinking and repeatable property reporting would need to be recreated.

## Consequences
- Property tests are deterministic, run in CI, and do not introduce a production dependency or solver.
- The generator itself has a guard test proving it emits varied schools and actual placements.
