# Phase 2 progress (resume file)

Updated after every commit. A new session should read this file first.

- **Branch:** `phase-2` (never commit to `master`).
- **Last green tag:** `phase-2d`.
- **Checkpoint in progress:** 2E (teachers).
- **Spec:** `docs/PHASE_2_SPEC.md`. Owner review: `docs/OWNER_CHANGES_REVIEW.md`. Decisions: `docs/DECISIONS_PENDING.md` (#1–#12).

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
| 2D | `phase-2d` | Subjects, schedule grid, blocked-periods grid, colour picker, textarea |

Last verified state:
- .NET: 99 tests passed.
- Vitest: 48 passed.
- Playwright: 4 passed.
- ESLint and Stylelint: clean.
- Build: 0 warnings.
- Line coverage: Domain 98.0%, Application 94.5%.

## Next
1. 2E, teachers (`Domain/Teachers`, `Application/Teachers`):
   - Full name; short name unique after normalization; soft archive (DECISIONS_PENDING #2).
   - Off days (ISO weekdays) and blocked periods: reuse `ScheduleGrid`, `BlockedPeriodsEditor` and `BlockedPeriodsGrid`.
   - Full release: a flag, an optional reason and an optional date range.
   - Limits: max per day ≤ `grid.LessonsPerDay`, max per week ≤ `grid.WeeklyCapacity` (DECISIONS_PENDING #5); notes.
   - Bulk add: paste names one per line, preview, then save, with duplicate detection.
   - Playwright screenshots of the teachers screen at four widths.
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
