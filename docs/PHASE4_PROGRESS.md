# Phase 4 progress

- Branch: `phase-4`, from `phase-2-5-template-update` (contains `phase-3-final`). Nothing pushed or merged.
- Scope: PHASE_4_MVP_PROMPT, which takes precedence over PHASE_4_PROMPT.
- Baseline before Phase 4: build 0 warnings; 229/229 .NET tests.
- Last green tag: `phase-4e`.

| Milestone | Tag | State |
|---|---|---|
| M1 scheduler | in `phase-4d` | done |
| M2 saving, «التوليد» | in `phase-4d` | done |
| M3 viewer, approval | in `phase-4d` | done |
| M4 manual edit | `phase-4d` | done |
| M5 print, Excel | `phase-4e` | done |
| M6 backup, release folder | `phase-4f` | next |

**Checks at `phase-4d`:**
- `dotnet build -c Release --no-incremental`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 284 passed, 4 skipped (performance, `SST_PERFORMANCE=1`).
- `npm run lint`: clean.
- Vitest: 97 passed.
- Playwright: 23 passed (before the editor scenario was added). The Phase 4 scenario with the editor passed on its own (1/1).

## Known problems
- The deterministic mode is slow: 12/20 found its first timetable after 34.7 s, and 24/40 found none in 60 deterministic units (docs/PERFORMANCE.md).
- A school whose sections all need the same single teacher at lesson 1 is infeasible by H3 (packing). The diagnostics explain it (test `AnImpossibleSchoolEndsInfeasibleWithAnExplanation`).
- `frontend/src/features/generation/GenerationPage.tsx.tmp` is a stray file created by mistake. It is not committed and waits for the owner's confirmation to delete.
- M1–M4 are one commit (`phase-4d`). They were built and verified together, so there are no separate `phase-4a`–`phase-4c` tags.
