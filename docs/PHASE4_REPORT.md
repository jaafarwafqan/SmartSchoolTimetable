# Phase 4 report (MVP delivery)

- Branch: `phase-4`, from `phase-2-5-template-update` (which contains `phase-3-final`). Not pushed, not merged.
- Scope: PHASE_4_MVP_PROMPT (takes precedence) together with PHASE_4_PROMPT.

## 1. Milestones

| Tag | Commit | Built |
|---|---|---|
| `phase-4d` | `2b4fcd9` | **M1–M4 together.** CP-SAT scheduler (H1–H10, S1–S5, real progress, cancel, deterministic mode, infeasibility cores). Independent verifier and scorer. Run and version persistence with a background worker. Generation screen. Viewer by section, teacher and master, with approval. Manual edit with immediate checks, undo/redo, and save as a new version. |
| `phase-4e` | `52f7690` | **M5.** Print stylesheet (A4 landscape for the master, portrait for section or teacher, school header). Excel export (ClosedXML). |
| `phase-4f` | `9a07142` | **M6.** Backup and restore in settings. `tools/Publish-Release.ps1` with the launcher and Arabic readme. The published executable was verified. |
| `phase-4-final` | this report's commit | Report. |

There are no separate `phase-4a`, `phase-4b` or `phase-4c` tags. M1–M3 were developed and verified together with M4 on one working tree, and tagging intermediate states that were never built on their own would be misleading.

## 2. Verification

Results from the last full run, before `phase-4f`:

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build -c Release --no-incremental` | 0 warnings, 0 errors |
| .NET tests | `dotnet test -c Release` | 285 passed, 4 skipped (the performance category; it runs only with `SST_PERFORMANCE=1`) |
| ESLint and Stylelint | `npm run lint` | clean |
| Vitest | `npx vitest run` | 97 passed (27 files) |
| Playwright with axe | `npx playwright test` | 23 passed |
| Published executable | Playwright auth and Phase 4 specs with `SST_RELEASE_EXE` set | 3 passed |
| Property tests (FsCheck) | `SolverPropertyTests` | 20 runs each for completeness and soundness, plus 60 generator witnesses checked by the verifier |

## 3. Requirement → test
- H1–H10: `SolverTests.H1…H10`. Each checks a valid timetable passes the verifier and that a variant needing a violation is proven infeasible.
- S1–S5: `SoftRuleTests`. Each reaches zero penalty, and the optimal objective equals the scorer's breakdown.
- Verifier: `VerifierTests`, a catalogue with the exact code for each broken timetable.
- Completeness and soundness: `SolverPropertyTests`.
- Determinism, progress, cancellation within 2 s, timeout that is not "infeasible", engine version: `SolverExecutionTests`.
- Diagnostics:
  - (a) teacher short of slots by packing, (b) two teachers and a laboratory, (c) doubles: `DiagnosticsTests`.
  - (d) timeout: `SolverExecutionTests.ATimeLimitWithoutASolutionIsTimedOutNotInfeasible`.
  - An infeasible school end to end: `GenerationApiTests.AnImpossibleSchoolEndsInfeasibleWithAnExplanation`.
- API: `GenerationApiTests` covers owner-only access, validation codes, the readiness gate, single active run (409), recovery to Interrupted, polling to a verified version, approval and its conflict, Excel content, and an edit refused on a violation and saved when valid. `BackupApiTests` covers backup, refused restores and a restore with sign-out.
- Code contracts: `GenerationCodeContractTests` (diagnostic and violation codes ↔ Arabic texts ↔ TypeScript lists) and `ErrorContractTests` (new HTTP codes).
- Architecture: `ArchitectureTests.OrToolsIsNotUsedByDomainApplicationOrApi`.
- Screens: `e2e/phase4-generation.spec.ts` covers generation, real progress, reload, the three views, keyboard navigation, axe, no horizontal scroll at 375/768/1024/1440, approval, print media, Excel download, and an editor conflict with undo.

## 4. Performance (docs/PERFORMANCE.md)
- Machine: i7-7500U (2 cores / 4 threads), 15.9 GB RAM, Windows 10.
- Default settings (2 workers, 60 s, three synthetic schools per size):

  | Size | Target | First valid timetable | Violations | Result |
  |---|---|---|---|---|
  | 12 sections / 20 teachers | 30 s | 0.79–1.80 s | 0 | **met** |
  | 24 sections / 40 teachers | 60 s | 1.81–3.20 s | 0 | **met** |
  | 40/54 and 40/60 (no promise) | – | 3.36 s and 3.71 s | 0 | one seed each |

- No run proved optimality within its limit.
- The deterministic mode (one worker) is slow: 12/20 found its first timetable after 34.7 s, and 24/40 found none in 60 deterministic units (about 196 s of wall time).

## 5. Decisions to confirm (docs/DECISIONS_PENDING.md #69–#77)
- Lesson time estimate when a shift has no period rows.
- H9 cap max(2, ⌈n ÷ days⌉).
- Pairs never cross a break; S2 is counted per shift.
- Deterministic mode uses deterministic time with a 10× wall-clock safety net.
- «إيقاف» keeps the best timetable.
- `Generation` may use `Scheduling` (architecture edge).
- Diagnostics never relax H1, H2 or H4.
- A sixth sidebar item «الجدول».
- Display-only colour and short name in the input.

## 6. Not done, partial, and known risks
- **Not built, by the MVP prompt:**
  - two-stage solve and decomposition (future proposals in docs/PERFORMANCE.md);
  - demo data;
  - a server-side PDF (owner saves a PDF from the print dialog).
- **Not built, by choice:**
  - Drag and drop is not provided; editing is by choosing and the keyboard, which the prompt allows.
  - Moves are within one section only.
- **Not verified:**
  - `تشغيل البرنامج.bat` itself was not executed, because it uses the real database location. Its content was written by the script, and the executable it starts was verified.
  - The printed output was checked through print-media emulation, not on paper.
  - The first run of `Publish-Release.ps1` failed at the publish step, cause not identified. A direct publish and the second run succeeded.
- **Risks:**
  - Timetable quality and feasibility depend on the owner's data.
  - H3 packing (lessons from lesson 1 with no gaps) makes some small or unusual curricula infeasible. The diagnostics explain those cases.
  - Real schools larger than 24 sections, or with tight availability, are untested.
  - The literary curriculum totals from the owner's source still need the owner's review; the readiness screen reports them.
- **Housekeeping:**
  - The first commit subject (`2b4fcd9`) starts with an invisible BOM character.
  - `phase-4d` was first created by mistake on `36a3fcd` and moved with `git tag -f` to `2b4fcd9` seconds later.

## 7. Commands
- **Run** (separate test database):
  ```powershell
  dotnet run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj -c Release -- --Database:Path="$env:TEMP\sst\test.db"
  ```
  Then open `http://127.0.0.1:5080/`.
- **Tests:**
  ```powershell
  dotnet build -c Release --no-incremental
  dotnet test -c Release
  cd frontend
  npm run lint
  npx vitest run
  npx playwright test
  ```
- **Performance:**
  ```powershell
  $env:SST_PERFORMANCE='1'
  dotnet test -c Release --filter "Category=Performance" --logger "console;verbosity=detailed"
  ```
- **Release folder:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File tools\Publish-Release.ps1
  ```
- **Check the published folder:** set `$env:SST_RELEASE_EXE` to the folder's `SmartSchoolTimetable.Api.exe`, then run `npx playwright test e2e/phase4-generation.spec.ts` in `frontend`.

## 8. Files left for the owner (not touched or not committed)
- `frontend/src/features/generation/GenerationPage.tsx.tmp`: a stray copy I created by mistake. It is not committed and should be deleted once you confirm.
- `artifacts/release/` (ignored by git): the published folder `SmartSchoolTimetable-20261009-130332`, plus a partial folder from the first failed script run.
- `%TEMP%\claude\sst-publish-probe`: a probe publish made outside the repository.
- `design-system/`, `temp_check/`, `.kilo/`, `.claude/`, `spikes/`: untouched.
