# ADR 0044: Timetable lifecycle, comparison, locked regeneration and the audit history

- Status: Accepted (M1, branch `work/phase-5`)
- Date: 2026-10-10

## Context
Phase 4 saved versions and one approved flag. The owner needs to know what changed between versions, go back safely, keep hand edits when regenerating, and see a history of what was done.

## Decisions
1. **Lifecycle.** `TimetableStatus`: Draft → Approved → Archived, or Draft → Archived. One table of valid transitions (`TimetableTransitions`). Approved and Archived versions never change; Archived is final. Approving a version archives the previously approved one in the same transaction. Invalid transitions return `TIMETABLE_INVALID_TRANSITION` (409). `IsApproved` stays as a column (it backs the one-approved-per-year unique index) and a database check keeps it equal to `Status = Approved`.
2. **Rollback is a new version.** `POST /timetables/{id}/rollback` copies the older version's lessons and input snapshot into a new Draft (`source = rolledBack`, parent = the older version). The older version and the approved version are untouched. Nothing is ever deleted.
3. **Comparison.** `GET /timetables/{from}/compare/{to}` (same year only). Lessons are matched per (section, curriculum line): same slot = unchanged or teacher change; leftover slots are paired in day/lesson order as moves; the rest are added or removed. The counts always add up (property-tested). Per-section and per-teacher summaries are computed on the server; names come from the versions' own snapshots.
4. **Keep manual edits.** The manual edits of a version are its lessons that are not in the generated (or restored) version it descends from through edits. A generation may carry `lockFromVersionId`: those lessons become variables fixed to 1 in the CP-SAT model (and one assumption family per section in diagnostics, `CORE_LOCKED_LESSONS`). A lock whose assignment or slot no longer exists is dropped and reported (`LocksDropped`). The locks are not part of the input hash (they are an option of the run, stored on it).
5. **Audit history.** Events are constants (`AuditEvents`), each in one category. A row stores the event code, the target and a small JSON of parameters (numbers and stable codes only). The Arabic sentence is built by the frontend; the English summary column is developer text and is never returned. `GET /audit` is paged, newest first (by id: SQLite cannot order by `DateTimeOffset`), filtered by category or event type. Rows from an older build without parameters, or with an unknown event, still list (under «بيانات المدرسة»). A contract test fails when an event has no category or no Arabic sentence, or a literal event name is written at a call site.
6. **Undo/redo** keys (Ctrl or Cmd + Z, Y, Shift+Z) use `event.code`, so they work on the Arabic keyboard layout; text fields keep their own undo.

## Consequences
- Migration `Phase5Lifecycle` (corrective): `TimetableVersions.Status/ArchivedAt` (existing approved rows become Approved), the source check allows `rolledBack`, `GenerationRuns.LockedFromVersionId/LockedLessons/LocksDropped`, `AuditHistory.ParamsJson` and an index on (EventType, OccurredAt). It reverses.
- New error codes `TIMETABLE_INVALID_TRANSITION`, `TIMETABLE_COMPARE_YEAR_MISMATCH`, `AUDIT_FILTER_INVALID`.
- Versions made before M1 that were once approved and later replaced stay Drafts (the old data did not record that); only new approvals archive.
