# Phase 2 progress (resume file)

Updated after every commit. A new session should read this file first.

- **Branch:** `phase-2` (never commit to `master`).
- **Last green tag:** `phase-2c`.
- **Checkpoint in progress:** 2D (subjects).
- **Spec:** `docs/PHASE_2_SPEC.md`. Owner review: `docs/OWNER_CHANGES_REVIEW.md`. Decisions: `docs/DECISIONS_PENDING.md` (#1–#11).

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
| 2C + quality gate | `phase-2c` | Rebuilt 2B/2C screens, CSS variable test, E2E through stages and sections |

Last verified state:
- .NET: 95 tests passed.
- Vitest: 46 passed.
- Playwright: 4 passed.
- ESLint and Stylelint: clean.
- Build: 0 warnings.
- Line coverage: Domain 97.7%, Application 94.1%.

## Next
1. 2D, subjects:
   - Domain entity in `Domain/SchoolSetup` or `Domain/Subjects`, plus `Application/Subjects`.
   - Colour is subject-1..10 only; priority 1–5; flags (spread across days, heavy, requires double period, distribution enabled); notes.
   - Blocked periods: a set of (ISO day, lesson number), validated against the working days and the largest shift (DECISIONS_PENDING #4 and #5).
   - Soft archive plus hard delete when unreferenced; a keyboard-operable blocked-periods grid (to be reused by 2E).
2. 2E: teachers, including bulk add with a preview.
3. 2F: calendar, `--seed-demo-data`, owner test script, `docs/PHASE2_REPORT.md`, tags `phase-2f` and `phase-2`.

## Patterns to reuse
- **Services:**
  - Use `StoreSaving.TryDomain` / `store.SaveAsync(map, duplicateField, ct)`. Do not duplicate Domain rules.
  - Hard delete requires a reference check, then `RECORD_IN_USE`.
- **UI:**
  - Build from `DataTable`, a `*Dialog` with `useFormFeedback`, `ConfirmDialog` and `RecordActions` / `ArchiveBadge` (in `features/stages-sections`).
  - Never key a component by `version`; use `useSyncedState` instead. Mutation callbacks are dropped on unmount.
  - Sibling keys must be unique (prefix them).
- **Commands:**
  - E2E needs `dotnet build` first, because it rebuilds `wwwroot`.
  - New baselines: `npx playwright test <spec> --update-snapshots`.

## Known problems
- Native date and time inputs show the browser's locale (for example "08:00 AM"). This is a known limitation (ADR 0017).
