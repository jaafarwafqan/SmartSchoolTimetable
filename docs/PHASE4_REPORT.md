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

## 5. Decisions (docs/DECISIONS_PENDING.md #69–#77): all approved by the owner on 2026-10-09
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
  - The printed output was checked through print-media emulation, not on paper.
  - The launcher's browser step was not observed on screen. It is the same `start` command at the verified port.
- **Investigated** (section 9):
  - The first `Publish-Release.ps1` failure did not reproduce in 6 attempts. The script now keeps a full log, retries once on download failures, and explains every failure in Arabic.
  - The launcher had a real bug, found by running it. It is fixed and verified on a temporary database.
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

## 8. Files
- **Deleted on the owner's instruction (2026-10-09), and nothing else:**
  - `frontend/src/features/generation/GenerationPage.tsx.tmp`;
  - `artifacts/release/SmartSchoolTimetable-20261009-130155` (it was empty).
- **In `artifacts/release/`** (ignored by git; nothing deleted):
  - `SmartSchoolTimetable-20261009-140917`: **the current folder, with the fixed launcher.** Use this one.
  - `SmartSchoolTimetable-20261009-130332` and `-140428`: older builds whose launcher has the bug from §9. Do not hand them out.
  - `*.publish.log`: the full publish logs.
- **Outside the repository** (all under `%TEMP%\claude\`; nothing deleted):
  - `sst-publish-probe`: the probe publish;
  - `sst-repro-*`: three clean-state reproductions;
  - `sst-inrepo-*`: the in-repository replica logs;
  - `sst-errors-*`: the error-path tests;
  - `sst-launcher-*` and `sst-bat-probe*`: the launcher tests and probes.
- **Untouched:** `design-system/`, `temp_check/`, `.kilo/`, `.claude/`, `spikes/`.

## 9. Release script and launcher investigation (2026-10-09)

**The first publish failure.** The original output is lost, because the command kept only its last 6 lines and the folder `...-130155` was empty. The only distinguishing fact is that the NuGet cache shows the win-x64 runtime packs (`microsoft.netcore.app.runtime.win-x64`, `microsoft.aspnetcore.app.runtime.win-x64` and `microsoft.windowsdesktop.app.runtime.win-x64` 9.0.20) created at 13:01:59, four seconds into that run. It was the first publish on this computer that needed them.

Reproductions (all logs kept):

| Attempts | Conditions | Result |
|---|---|---|
| 3 | Fresh `git archive` extracts, each with a new empty NuGet cache, a non-RID Release build, then the script (`sst-repro-20261009-133632`) | 3 of 3 succeeded |
| 3 | In-repository replicas: `dotnet restore` back to the non-RID state, a new empty NuGet cache, the original `dotnet publish` command, with VS Code's C# Dev Kit open and the owner's server running (`sst-inrepo-*`) | 3 of 3 succeeded, in 66–80 s |

**Conclusion.** The root cause is not determinable from the evidence left. A cold cache, a fresh tree, Dev Kit and a running server do not reproduce it. The most likely cause is a transient failure while downloading the runtime packs for the first time, but that is not proven.

**Changes to `tools/Publish-Release.ps1`:**
- The complete `dotnet publish` output always goes to `<folder>.publish.log`.
- On a NuGet or network download failure the publish is retried once automatically.
- Every failure ends with an Arabic message: the likely cause, the action to take, the log path, the exit code and the first error lines.

  | Cause | Detected by |
  |---|---|
  | locked file | `being used by another process`, `EBUSY`, `MSB3021` |
  | download failure | `NU1301` and others |
  | frontend build failure | `npm ERR!`, `MSB3073` |
  | unknown | anything else |

- Before publishing, the script warns in Arabic when the app is running from the source folder.
- An existing output folder is refused with an Arabic message.

Each error path was exercised in its own temporary extract (`sst-errors-*`):

| Case | Simulated by | Result |
|---|---|---|
| Frontend | a TypeScript error | the frontend hint, with the two `TS` errors including file and line |
| Locked file | `wwwroot\index.html` held open with no sharing | the locked-file hint |
| Network | an unreachable HTTPS package source | the retry notice, then the network hint with `NU1301` |

**Launcher bug, found by running it.** The launcher started `SmartSchoolTimetable.Api.exe` by bare name. With the Windows setting `NoDefaultCurrentDirectoryInExePath=1`, which was set in the session where I ran the tests, cmd does not search the current folder and replied "'SmartSchoolTimetable.Api.exe' is not recognized". Every line before it ran correctly, as traced with `echo on`.

**Fix.** The launcher now:
- calls `"%~dp0SmartSchoolTimetable.Api.exe"` by full path;
- forwards its arguments (`%*`), so the database path or port can be passed;
- takes an optional port `SST_PORT` (default 5080, and the browser opens on the same port);
- skips the browser when `SST_NO_BROWSER` is set, for unattended checks;
- pauses with an Arabic message if the server exits with an error.

**Launcher verification** on folder `-140917`, port 5099, with `--Database:Path=<new temp folder>` on the command line. The test was run with `NoDefaultCurrentDirectoryInExePath=1` and again with it cleared, and both runs gave the same results:
- The server answered `bootstrap` with `setupRequired = true`.
- `/` returned 200 with `lang="ar" dir="rtl"`.
- The temp database was created.
- The server's own command line showed the temp path and port.
- `%LOCALAPPDATA%\SmartSchoolTimetable\timetable.db` was byte-identical before and after (hash and write time).
- Only the test's own server process was stopped.
