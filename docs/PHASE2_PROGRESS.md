# Phase 2 progress (resume file)

Updated after every commit. A new session should read this file first.

- **Branch:** `phase-2` (never commit to `master`).
- **Last green tag:** `phase-2` (also `phase-2f`).
- **Checkpoint in progress:** none. Phase 2 is complete and awaits owner acceptance. Do not start Phase 3.
- **Spec:** `docs/PHASE_2_SPEC.md`. Owner review: `docs/OWNER_CHANGES_REVIEW.md`. Decisions: `docs/DECISIONS_PENDING.md` (#1–#15).

## Baseline at resume (2026-10-04, before any change)
- Build: 0 warnings. `dotnet test`: 88/88.
- Lint: clean. Vitest: 41/41.
- Playwright: 3 passed, 1 failed (the dashboard checklist assertion).

## Done
| Checkpoint | Tag | Notes |
|---|---|---|
| 2A | `phase-2a` (`ba8658e`) | Shell, profile, years and terms, dashboard |
| 2B | `phase-2b` (`1634eb4`) | Working days, shifts, periods, bells |
| Owner snapshot | none (`e1e67ec`) | The owner's 2C work as received |
| Review fixes | none (`96ba605`) | Backend fixes for 2C and the 2B services |
| 2C + quality gate | `phase-2c` (`e593d8e`) | Rebuilt 2B/2C screens, CSS variable test, E2E through stages and sections |
| 2D | `phase-2d` (`9c76bd6`) | Subjects, schedule grid, blocked-periods grid, colour picker, textarea |
| 2E | `phase-2e` (`e21769f`) | Teachers with constraints, bulk add with preview |
| 2F | `phase-2f`, `phase-2` | Calendar, demo data, year copy in UI, ADR 0019, owner test script, report |

Last verified state:
- .NET: 108 tests passed.
- Vitest: 50 passed.
- Playwright: 4 passed.
- ESLint and Stylelint: clean.
- Build: 0 warnings.
- Line coverage: Domain 98.3%, Application 94.8%.

## Next
- Owner review and acceptance (`docs/PHASE2_REPORT.md`, `docs/OWNER_TEST_SCRIPT_PHASE2.md`, `docs/DECISIONS_PENDING.md`). The owner merges `phase-2` into `master`.

## Patterns to reuse
- **Services:**
  - Use `StoreSaving.TryDomain` / `store.SaveAsync(map, duplicateField, ct)`. Do not duplicate Domain rules.
  - Hard delete requires a reference check, then `RECORD_IN_USE`.
- **UI:**
  - Build from `DataTable`, a `*Dialog` with `useFormFeedback`, `ConfirmDialog` and `RecordActions` / `ArchiveBadge` (in `components/`), `BlockedPeriodsEditor` (in `features/timetable-structure`).
  - Never key a component by `version`; use `useSyncedState` instead. Mutation callbacks are dropped on unmount.
  - Sibling keys must be unique (prefix them).
- **Commands:**
  - E2E needs `dotnet build` first, because it rebuilds `wwwroot`.
  - New baselines: `npx playwright test <spec> --update-snapshots`.

## Known problems
- Native date and time inputs show the browser's locale (for example "08:00 AM"). This is a known limitation (ADR 0017).
