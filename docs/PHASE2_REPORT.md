# Phase 2 report: core school data

- **Branch:** `phase-2`. Nothing was committed to `master`; the owner merges after acceptance.
- **Scope:** Phase 2 only. Phase 3 was not started.
- **Evidence:** every number below comes from command output in the final session (2026-10-04).

## 1. State found at the start of the resume session
- **Branch and tags:** branch `phase-2`. Last tag `phase-2b` (`1634eb4`), on top of `phase-2a` (`ba8658e`).
- **Owner changes:** 31 files changed or added (as committed in the snapshot): the in-progress 2C stages and sections work across Domain, Application, Api, migration, UI, dashboard and tests, plus rebuilt `wwwroot` assets and `docs/PHASE_2_SPEC.md`.
- **Safety check:** no database files, secrets, `.env` files, uploaded images or real personal data among them.
- **Baseline before any change:**
  - `dotnet build -c Release --no-incremental`: 0 warnings, 0 errors.
  - `dotnet test`: 88 passed.
  - ESLint and Stylelint: clean.
  - Vitest: 41 passed.
  - Playwright: 3 passed, 1 failed (`phase2-school.spec.ts:93`). The dashboard "all steps done" assertion failed because the checklist had grown.

## 2. Owner changes (details in `docs/OWNER_CHANGES_REVIEW.md`)
The owner's edits were committed unchanged first, as `e1e67ec` "Owner manual changes (as received, before review)" (no tag).

**Kept as designed:**
- year-scoped routes `/academic-years/{yearId}/stages/...`;
- entity shapes; archive and restore;
- "a stage with active sections cannot be archived";
- the year-structure copy of stages and sections;
- the dashboard counts and steps; Arabic labels; navigation.

**Fixed** (17 findings, plus 4 in the committed 2B code):

| Area | Problem | Fix |
|---|---|---|
| Validation | Rules were duplicated between the services and the Domain | The Domain is now the single source |
| Save errors | A unique-index violation returned `CONFLICT` | It now returns `DUPLICATE_NAME` |
| Display order | A duplicate order was reported as a duplicate name | Order is a sort key, not unique (migration `Phase2CDisplayOrderIndexes`, decision #10) |
| Section lists | N+1 queries | One query each for the week and the shifts |
| Capacity rule | Lived in Application | Moved to `Section.WeeklyCapacity` |
| Deletes | No hard delete | Added for stages, sections and shifts, with reference checks |
| Archived stages | A section could be restored into, or created in, an archived stage | New code `STAGE_ARCHIVED` |
| Year copy | Matched by name and order with `.Single` (crashes on duplicates) | Source-id map |
| Migration | Generated in a new folder | Moved into `Migrations/`; migration ID unchanged, so already-applied databases are unaffected |
| Undefined CSS variables | Borders and gaps never rendered | Real tokens, plus a new test |
| Screens | Rebuilt for both 2B and 2C (single-line forms, swallowed errors, no edit or delete, no conflict handling, missing row errors) | Rebuilt from design-system components |
| Bell tones | The UI expected `Classic`, the API sent `classic`, so the tone select was empty and "test sound" threw | The UI uses the API values |

**Removed:** the service-level validation helpers (`ValidateStage`, `ValidateSection`, `ValidateName`), because they duplicated Domain rules. Their behaviour is preserved by the Domain.

**Needs your decision:** decisions #10 and #11 in `docs/DECISIONS_PENDING.md`.

## 3. Checkpoints
| Tag | Commit | Built |
|---|---|---|
| `phase-2a` | `ba8658e` | App shell (sidebar, drawer, top bar, breadcrumbs). School profile with authenticated logo/stamp storage (magic bytes, no SVG). Academic years and terms. Dashboard with real counts and a checklist. Formatter. Arabic normalization. `Version` concurrency. Audit. `dotnet-ef` local tool and a pending-model test. |
| `phase-2b` | `1634eb4` | Working days, shifts, lesson periods with a generator, bell settings with Web Audio tones (from the previous session; reviewed and corrected in 2C). |
| (snapshot) | `e1e67ec` | The owner's 2C work, as received. |
| (review) | `96ba605` | Backend fixes for the owner's 2C work and the 2B services. |
| `phase-2c` | `e593d8e` | Stages and sections with weekly capacity and archive rules. Rebuilt 2B and 2C screens. A test for undefined CSS variables. E2E through 2C. |
| `phase-2d` | `9c76bd6` | Subjects (palette colours only, priority, flags, notes). Schedule grid. A keyboard blocked-periods grid. Textarea and colour picker on `/design`. |
| `phase-2e` | `e21769f` | Teachers: off days, blocked periods, full release, limits checked against the grid, bulk add with a preview, archive. |
| `phase-2f` (`958a09c`) / `phase-2-final` | `958a09c` plus housekeeping | Academic calendar (list and month view, out-of-year warning). `--seed-demo-data`. Copy-structure option in the new-year dialog. ADR 0019. Owner test script. This report. |

## 4. Verification (final run)
- **`dotnet build .\SmartSchoolTimetable.sln -c Release --no-incremental`:** 0 warnings, 0 errors (warnings are errors).
- **`dotnet test`:** 108 passed, 0 failed.
  - Consistency tests: error codes have a status and an Arabic text; no raw codes in `src/`; no pending EF model changes.
  - Architecture tests: no DbContext or entities in the Api; feature folders depend only inward.
- **Line coverage (Cobertura):**

  | Assembly | Line | Branch |
  |---|---|---|
  | Domain | 98.3% | 87.1% |
  | Application | 94.8% | 83.8% |
  | Api | 94.0% | 81.5% |
  | Infrastructure | 97.3% | 71.2% |

- **ESLint:** exit 0. **Stylelint:** exit 0.
- **Vitest:** 50 tests in 11 files passed, including the contrast test, the CSS custom-property test, the blocked-grid keyboard test, period rows, the month grid and tone patterns.
- **Playwright:** 4 passed.
  - `phase2-school.spec.ts` covers the whole setup checklist end to end: profile → year/term → shift and generated periods → stages and sections → subjects with the blocked grid → teachers including bulk add → calendar day → year copy. It also covers a stale edit between two pages and the lock screen.
  - Axe found no serious or critical violations on 13 phase 2 screens and dialogs:
    - dashboard, empty and complete;
    - school profile and academic years;
    - timetable structure, and stages and sections;
    - subject dialog and subjects list;
    - teacher dialog, bulk-add preview and teachers list;
    - calendar list and month view.

    `design-quality.spec.ts` covers the 6 account screens.
  - Screenshots at 375/768/1024/1440 px for dashboard, periods and teachers, plus login and settings. There are 20 baselines, with `maxDiffPixelRatio` 0.0002; two consecutive runs were stable.
- **Manual:** `--seed-demo-data` against a scratch path created the database, refused a second run on the same file, and refused a missing path, all with Arabic messages. The app started with `--Database:Path=<demo>` and answered `/api/v1/bootstrap` with `setupRequired: true`. Nothing was run against `%LOCALAPPDATA%`.

## 5. Decisions you need to confirm (`docs/DECISIONS_PENDING.md`)
1. Shifts, periods, stages and sections are year-scoped; subjects, teachers and the profile are global.
2. "Active" = not archived (one flag).
3. ISO weekdays; default Sunday–Thursday.
4. Blocked periods use lesson numbers (breaks excluded).
5. Teacher limits are checked against the current year's largest shift; they are accepted before periods exist.
6. Capacity gaps = sections with zero weekly capacity.
7. A fixed time-zone list.
8. Default Arabic-Indic numerals and the Gregorian calendar.
9. Bell tones are synthesized with Web Audio and play only on an explicit preview (from the 2B session).
10. Display order is not unique.
11. Section and stage archive and delete rules.
12. Blocked periods must lie inside the current grid; none can be blocked before periods exist.
13. Short-name proposals in bulk add.
14. Calendar days are global and the year check is only a warning.
15. The demo command also refuses the configured database path.

## 6. Not done, partial, gaps, risks
- **Native date and time inputs** show the browser's locale (for example "08:00 AM" and Latin digits inside the input). Values shown outside inputs follow the school setting (ADR 0017).
- **Bells:** configuration and preview only. Live ringing is Phase 7, as specified.
- **Stored blocked periods are not re-checked automatically** when working days or periods change later. The editor warns and drops out-of-grid slots on the next save (decision #12).
- **The schedule grid used by the subject and teacher editors** refreshes after structure edits. It does not refresh immediately after another year is made current, only on the next fetch.
- **Deletes of subjects, teachers and sections are allowed** because nothing references them yet. Phase 3 must add workload reference checks (ADR 0019).
- **Year-scoped lists in the UI** (shifts, stages, sections) request up to 100 rows per screen. The API pages beyond that, but these screens do not show pagination.
- **Hijri** is a display option only; all stored dates are Gregorian.
- **Screenshot baselines** are Windows/Chromium-specific (`*-win32.png`).
- **Markdown lint warnings** (MD022/MD032) appear in some docs; they are not part of the build gates.

## 7. Exact commands (PowerShell, repository root)
```powershell
# Build and test
dotnet build .\SmartSchoolTimetable.sln --configuration Release --no-incremental
dotnet test .\SmartSchoolTimetable.sln --configuration Release --no-build
npm.cmd --prefix .\frontend run lint
npm.cmd --prefix .\frontend test

# Run the app (real database in %LOCALAPPDATA%), then open http://127.0.0.1:5080/
dotnet run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj --configuration Release --no-build

# Demo data in a separate file, then run against it
dotnet run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj --configuration Release --no-build -- --seed-demo-data "$env:TEMP\sst-demo\demo.db"
dotnet run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj --configuration Release --no-build -- --Database:Path="$env:TEMP\sst-demo\demo.db"

# Style guide (development only), then open http://127.0.0.1:5173/design
npm.cmd --prefix .\frontend run dev
```

## 8. Files and folders that look unrelated (not touched, not committed)
- `.claude/`: tool settings for the coding assistant.
- `.kilo/`: another assistant's workspace.
- `temp_check/`: a scratch folder.

All three are excluded from every commit, as instructed.

## 9. After the report (2026-10-04)
- **Tag rename:** the final tag `phase-2` had the same name as the branch, so Git called the name "ambiguous". It is renamed to `phase-2-final`; `phase-2` now names only the branch.
- **`.gitignore`:** now ignores `design-system/`, a folder third-party design skills regenerate (it reappeared after Phase 1.4 deleted it), and all `*.db`, `-wal`, `-shm` and `-journal` files.
- **Zip archive:** `..\SmartSchoolTimetable-phase2.zip` was created from commit `958a09c` (448 files; no databases or ignored folders).
- **Not merged:** waiting for the owner's acceptance.

