# ADR 0034: Scheduling input contract and hash

- Status: Accepted for Phase 3D. Checked against the code in the Phase 3 finish (`SchedulingInput.CurrentFormatVersion` = 1; `SchedulingInputHashTests`).
- Date: 2026-10-05

## Context
Phase 4 needs a stable boundary between school data and the solver. A solver must receive the same inputs that were validated, and a later generated timetable must identify the exact scheduling-relevant input/profile state used.

## Decision
- Define immutable serializable records in the Application layer. `SchedulingInputBuilder` reads the local database with no-tracking queries; the validator and Phase 4 adapter consume the snapshot, not EF entities.
- Include active stages even when they have no sections, section-specific allowed slots, active curriculum and workload, required resources, teacher availability/limits, ordered shift period rows (lessons, breaks, times and bell flags), and `ProfileVersion`.
- Sort collections canonically before serialization. Hash compact JSON with lowercase SHA-256.
- Exclude presentation-only names, labels and assignment row IDs. Include stable IDs and every value that can affect placement, including `FormatVersion` and profile version.
- Bump the format version whenever the contract shape or canonical rules change. Phase 4 stores the hash with the generated timetable version.

## Alternatives
- Let the solver read the database: rejected because validation and solving could observe different data, and solver code would depend on persistence details.
- Hash EF change-tracker state or database bytes: rejected because serialization/order/provider differences would make it unstable.
- Include display names in the hash: rejected because renaming should not invalidate a timetable.

## Consequences
- Equal scheduling inputs produce equal hashes independent of insertion order and machine locale. Scheduling-relevant changes produce different hashes; display-only stage/teacher/subject names remain outside the hash.
- The contract contains no solver package types; CP-SAT placement, queues and progress remain Phase 4.
