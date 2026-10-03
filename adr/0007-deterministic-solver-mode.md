# ADR 0007: Deterministic solver mode

- Status: Accepted
- Date: 2026-10-03

## Context
Solver results must be reproducible enough to investigate scheduling outcomes and compare model changes.

## Decision
Persist the complete input hash, scheduling-profile version, solver version, parameters, and random seed for every run. Deterministic mode uses one CP-SAT worker and a fixed persisted seed. Multi-worker mode is available for throughput but is not promised to reproduce the same schedule or objective.

## Consequences
- Reproduction is scoped to the same solver version, model/input, parameters, and compatible runtime environment; upgrades can change results.
- Multi-worker results and timing may differ between executions.
- Tests should assert hard-constraint validity and status, not exact output equality, except for deterministic-mode tests under a pinned environment.
- The seed is metadata for reproducibility; it must not be represented as a guarantee across solver or platform changes.
- The Phase 0 C# spike pins seed `12345`; its benchmark runs use one or eight workers to compare deterministic and throughput-oriented modes.
- Two separate 12-section, 20-teacher, one-worker runs with the pinned seed both returned the same 5,600 incumbent objective and 480 assignments. Their solve times differed (2.796 s and 1.521 s), so deterministic output does not imply deterministic wall-clock timing.
