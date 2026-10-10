# Phase 4 follow-up report: R1, R2 (tag `phase-4g`) and R3 (tag `phase-4h`)

- Branch: `phase-4-followup` (renamed from `phase-4g`, because a branch and a tag with the same name made `git` refs ambiguous), from `phase-4` (`3dee04a`). Not pushed, not merged.
- Nothing was run against the owner's real database: every test, check and published-executable run used temporary databases.

## R1: 12-hour time (Iraqi convention) everywhere

**What changed:**
- `frontend/src/lib/time.ts` is the single formatter: «٨:٠٠ ص», «١:٣٠ م», noon «١٢:٠٠ م», midnight «١٢:٠٠ ص».
  - `format.time` now calls it, so every screen that showed a time follows.
  - The API and the database keep 24-hour `HH:mm`.
- `TimeField` was rebuilt from the existing `Select`:
  - hour 1–12, minute in 5-minute steps (the current minute is always kept), and ص/م;
  - native selects, so the keyboard and screen readers work as usual;
  - Arabic accessible names, for example «بداية أول حصة - الساعة».
- Excel writes 12-hour lesson times in the lesson headers (`Infrastructure/Export/Clock12`, the same rule as the frontend).

**Every place a time is shown or entered** (search for `format.time`, `TimeField`, `type="time"`, `hourCycle`, `toLocaleTimeString`, `getHours` in `frontend/src`; and `TimeOnly`/minutes formatting in exports):

| Where | Kind | File |
|---|---|---|
| Readiness: the time of the check | display | `features/readiness/ReadinessPage.tsx` (`format.time`) |
| Wizard timing step: preview «من / إلى» | display | `features/setup-wizard/TimingStep.tsx` (`format.time`) |
| Wizard timing step: first lesson start | input | `TimingStep.tsx` (`TimeField`) |
| Generate-periods dialog: first lesson start | input | `features/timetable-structure/GeneratePeriodsDialog.tsx` (`TimeField`) |
| Periods editor: start and end of every row | input | `features/timetable-structure/PeriodRow.tsx` (`TimeField` compact) |
| Breaks editor: each break's clock time (new, R2) | display | `features/timetable-structure/BreaksEditor.tsx` (`format.time`) |
| Timetable viewers (section, teacher, master) and print | display | `features/timetable/TimetableGrids.tsx` (`format.time`) |
| Style guide sample | input | `features/design-guide/PatternsSection.tsx` (`TimeField`) |
| Excel export: lesson headers | generated document | `Infrastructure/Export/ExcelTimetableExporter.cs` (`Clock12`) |

- There were no native `type="time"` inputs.
- The generation screen shows durations in seconds, not clock times. Every other screen shows dates only.

**Tests:**
- `lib/time.test.ts`: 00:00, 00:05, 11:59, 12:00, 12:30, 13:00, 23:59 and 08:05; a round trip of all 1,440 minutes; no output on a 24-hour clock.
- `format.test.ts`.
- `DateTimeFields.test.tsx` (TimeField).
- `Clock12Tests` with the same cases.
- `GenerationApiTests`: the Excel header «الحصة ١ / ٨:٠٠ ص – ٨:٤٠ ص», and no 24-hour time anywhere in the workbook.
- Playwright `expectNo24HourTimes` checks the wizard timing step (its preview runs past noon) and the section and master viewers. It converts Arabic-Indic digits before applying `/\b(1[3-9]|2[0-3]):\d\d\b/`.

**Screenshots viewed:**
- Wizard step 3 at 375 px: the time field reads «٨ : ٠٠ ص», and the preview shows «١:٣٠ م».
- The periods editor rows at 375 px.

The baselines `periods-*` and `wizard-step-3-*` changed only in the time fields and times.

## R2: flexible breaks

**What changed:**
- **Any number of breaks.** A break may follow any lesson 1..N−1, at most one per gap, or there may be none.
- **Duration control.** Each break's duration is chosen with a 1–60 minute stepper plus quick picks of 5/10/15/20/30 (`ChipGroup`, with `aria-pressed` and a check icon). Nothing is typed.
- **Rows.** Each row is edited in place (no drawer) and shows its 12-hour clock time and a delete action.
- **Arabic validation.** A break after the last lesson or a duplicate gap shows an Arabic message on its row and blocks «التالي» and «إنشاء القائمة».
- **Suggestion.** The suggested length for the school type is shown as a suggestion in the legend, and the owner can change or remove it.

**Caps and whitelists found and removed:**

| Cap | Where | Now |
|---|---|---|
| `PeriodGenerator.MaxBreaks = 3` and its `Breaks` count check | Domain | removed |
| `PeriodGenerator.MinBreakMinutes = 5` | Domain | 1 (the 120 maximum and all other validation stay) |
| `Shift.MaxRows = 20` | Domain | `2 × MaxLessons − 1` = 23 (12 lessons and 11 breaks) |
| `maxBreaks = 3` and `validBreaks(...).slice(0, maxBreaks)` | `BreaksEditor.tsx` | removed |
| `breakMinuteChoices = [5, 10, 15, 20, 25, 30, 40, 45, 60]` (duration whitelist) | `BreaksEditor.tsx` | replaced by the stepper and quick picks |
| «ثلاث استراحات كحد أقصى» text | dictionary | removed |
| "up to 3 breaks" | API.md, ADR 0026 | amended |

There was no database constraint on the row count, so there is no migration.

**Tests:**
- `FlexibleBreaksTests`:
  - a break after lesson 1 and after N−1;
  - a break in every gap of a 12-lesson day (23 rows saved);
  - 1-minute and 60-minute breaks with exact start and end times;
  - a duplicate gap, a break after the last lesson and a 0-minute break refused;
  - old plans and presets giving exactly the same rows as before;
  - the generator endpoint with six breaks.
- `StageLessonsAndBreaksTests` amended (four breaks are now accepted).
- `breaks.test.ts`: issues, clock times and gaps.
- Playwright `phase25-model`:
  - a quick pick;
  - a third break moved after lesson 1 and shortened to one minute (clock «من ٨:٤٥ ص إلى ٨:٤٦ ص»);
  - lowering the lessons to 4, which shows «لا يمكن وضع استراحة بعد آخر حصة…» and refuses «التالي»;
  - three breaks saved with exact times.
- `phase2-school`: the generate-periods dialog still fits 1280×720 without scrolling. Its check now reports the measured heights.

**Screenshots viewed:**
- The generate-periods dialog at 1280×720 (it first overflowed by 83 px and was compacted).
- The breaks editor at 375 px and 1440 px.

## Gates at `phase-4g` (R1 + R2)

| Gate | Result |
|---|---|
| `dotnet build -c Release` | 0 warnings, 0 errors |
| `dotnet test -c Release` | 298 passed, 4 skipped (the performance category) |
| ESLint and Stylelint (`npm run lint`) | clean |
| Vitest | 107 passed (28 files) |
| Playwright with axe | 23 passed (full suite) |
| Published executable (`SmartSchoolTimetable-20261009-234213`) | auth flow, `phase25-model` (12-hour timing step, breaks) and `phase4-generation` (viewers with the 12-hour check): 4 passed |
| Launcher, on a temporary database | the server answered, first-run setup shown, page right-to-left, temp database created, real database unchanged |
## R3: double shift (دوام مزدوج) with daily sessions

**What changed** (ADR 0043; DECISIONS_PENDING #81–#86):
- **Model.** `SessionPlan` per academic year:
  - `System`: `OneSession` (the default; no row means one session), `TwoSessions`, or `ThreeSessions`. Three sessions are allowed by the model; the UI shows «الدوام الثلاثي … قريباً».
  - The timing of each session other than morning, as lesson and break rows.
  - The day mapping `(semester 1|2, day) → session`.

  Morning is the shift's own periods, so a single-session school sees no change and has no extra step.
- **Same lessons per day in every session.** This is enforced in the domain and the API with the Arabic message `SESSION_LESSON_COUNT_MISMATCH`. Three guards keep it true later:
  - changing the shift's lesson count is refused while sessions are on;
  - adding a second shift is refused (`SESSIONS_NEED_ONE_SHIFT`);
  - applying the old two-shift mode is refused (`SESSIONS_NEED_ONE_SHIFT`).
- **Uniform period count investigated** (#85):
  - The solver, `TimetableVerifier` and `SchedulingInput` already support different counts per day (`AllowedByDay`/`LessonsByDay`; the H3 test with [4,4,4,4,2]).
  - Those per-day counts apply to every session alike. No per-session-per-day counts were added.
- **One grid.** Generation, verification and approval are unchanged. A double lesson must be adjacent in **every** session, so a break in the evening splits (n, n+1). All four adjacency checks now share one rule, `ShiftInput.Adjacent`: the validator, the CP-SAT model builder, the verifier and the scorer.
- **InputHash** (#81):
  - Only the lesson gaps where another session has a break, and the shift does not, are hashed (`SessionBreaksAfter`).
  - Evening clock times and the day mapping are not hashed: they change no lesson.
  - A null value is left out of the JSON, so single-session hashes are byte-for-byte the same as before.
- **UI** (`/school/timing`, card «نظام الدوام اليومي»; no drawers, nothing typed):
  - choice cards «دوام واحد / دوام مزدوج»;
  - a read-only morning summary;
  - the evening timing: 12-hour `TimeField`, lesson length stepper, the R2 breaks editor, and a live 12-hour preview;
  - per semester, day chips «اختر أيام الدوام الصباحي» (the other days are listed as evening) and «اعكس للفصل الثاني».
- **Viewer, print and Excel:**
  - The timetable page has a semester switch «الفصل الدراسي الأول / الثاني» (chips with `aria-pressed`).
  - Section and teacher grids show the lesson numbers, then one clock row per session («الدوام الصباحي», «الدوام المسائي»). Each day's row header names its session in the chosen semester.
  - The master grid shows «الأحد — صباحي» and that session's clock under each lesson number.
  - Print adds «أوقات الفصل الدراسي الثاني».
  - Excel takes `?term=1|2`. It names the semester in each sheet header and in the file name (`timetable-vN-term2.xlsx`), adds the same per-session time rows and day labels, and has no 24-hour time.
- **Old «مزدوج»** (two shifts, each with its own sections) is kept and relabelled «ورديتان بشعب مختلفة» (#82).
- **Teacher availability** stays by (day, lesson). Availability by session (for example, no evenings) is a future option in #83.
- **Migration** `20261009205646_Phase4SessionPlans` is corrective: it only adds tables, and no old migration was edited. Old data reads as one session, with the same times in both semesters.

**Files:**
- **Domain:** `SchoolSetup/SessionPlan.cs`, `DomainErrorCode.SessionLessonCountMismatch`.
- **Application:**
  - `SchoolSetup/SessionPlanService.cs`;
  - guards in `TimetableStructureService` and `ShiftModeService`;
  - `ShiftInput.SessionBreaksAfter` and `Adjacent` in `Scheduling/SchedulingInput.cs`, plus `SchedulingInputHash` and `SchedulingInputBuilder`;
  - `PreSolveValidator`, `TimetableVerifier`, `TimetableScorer`;
  - `GridSessions` and `SessionsAsync` in `TimetableService`;
  - the semester in `TimetableExport`;
  - `ErrorCodes`.
- **Infrastructure:** `CpSatModelBuilder`, `ExcelTimetableExporter`, `SessionPlanConfiguration`, the migration.
- **Api:**
  - `GET/PUT /api/v1/session-plan`;
  - `?term=` on `export.xlsx`;
  - `ApiErrorCodes`.
- **Frontend:**
  - `features/timetable-structure/SessionsCard.tsx`, `sessionPlan.ts`, `sessionPlanApi.ts`;
  - `components/ui/chip-group.tsx` (`ToggleChipGroup`);
  - `components/ui/timetable-grid.tsx` (`headerRowLabels`);
  - `features/timetable/TimetableGrids.tsx`, `TimetablePage.tsx`, `TimetableEditor.tsx`, `timetableApi.ts`;
  - `lib/time.ts` (`minutesOf`);
  - the dictionary (`structure.ts`, `school.ts`, `wizard.ts`, `phase4.ts`, `errors.ts`);
  - styles.
- **Docs:** ADR 0043, `API.md`, DECISIONS_PENDING #81–#86, `USER_GUIDE_AR.md`, `OWNER_TEST_SCRIPT_PHASE4.md` (steps 26–32).

**Tests added:**
- `SessionPlanTests` (domain and scheduler):
  - the owner's mapping and its reverse;
  - unequal lesson counts refused (`SessionLessonCountMismatch`);
  - each working day mapped exactly once per semester (missing, duplicate, wrong session, non-working day), and overlapping evening rows refused;
  - three sessions in the model, and one session clearing the plan;
  - a single session keeps the pre-R3 hash, and an old snapshot reads back the same;
  - an evening break splits a double lesson in the solver (infeasible in «دروس مزدوجة», valid in the standard mode), the scorer (S5 penalty), the verifier (`DOUBLE_PERIOD_BROKEN`) and the validator (`DOUBLE_PERIOD_IMPOSSIBLE`).
- `SessionPlanApiTests`:
  - the full scenario on a temporary database: the one-session default; Arabic-coded refusals; the shift-count and second-shift guards; generate once; API times for semester 1 and 2;
  - Excel for semester 1 and 2: header, session rows «٨:٠٠ ص – ٨:٤٠ ص» / «١:٠٠ م – ١:٤٠ م», «الأحد\nصباحي» in S1 against «الأحد\nمسائي» in S2, identical lessons, no 24-hour time, `term=3` refused;
  - a new mapping keeps the version current, and a new evening break makes it out of date;
  - back to one session: the single-shift regression, with no semester in Excel;
  - only a new break gap changes the hash (`OnlyANewBreakGapChangesTheInputHash`);
  - sessions need exactly one shift.
- `BackupApiTests.ABackupFromThePreviousSchemaIsRestoredAsASingleSessionSchool`: a backup is migrated down to `Phase4Generation` (the previous schema), restored, upgraded, and reads as one session with its data.
- Vitest `sessionPlan.test.ts`: evening rows, form round trip, mapping, reverse.
- Playwright `phase4-sessions.spec.ts`:
  - the card: double, evening 1:00 pm with a break, semester 1 Sunday+Monday, reverse;
  - axe and no horizontal scroll at 375 and 1280 px;
  - generate once;
  - the viewer in S1 and S2: same lessons, Sunday morning then evening, 12-hour check, axe;
  - the master in S2, the print header, and the Excel file name.

**Screenshots viewed** (from the Playwright run, opened and checked):
- `r3-sessions-card.png`: the card with «دوام مزدوج», the morning summary «من ٨:٠٠ ص إلى ١:٣٠ م، ٧ حصص», the evening editor with a break «من ٣:١٥ م إلى ٣:٣٠ م», the 12-hour preview, and the chips (S1: الأحد، الاثنين; S2: الثلاثاء، الأربعاء، الخميس). The element screenshot shows the sticky top bar over its middle; this is a capture effect and does not appear on the page.
- `r3-viewer-term1.png` and `r3-viewer-term2.png`: the section grid with the rows «الدوام الصباحي ٨:٠٠ ص – ٨:٤٥ ص …» and «الدوام المسائي ١:٠٠ م – ١:٤٥ م …». Sunday and Monday show «صباحي» in S1 and «مسائي» in S2; the lessons are identical.
- The full Playwright run also produced two screenshot differences, both reviewed and intended:
  - `periods-*`: the new «نظام الدوام اليومي» card on the structure page;
  - `wizard-step-1-*`: «ورديتان بشعب مختلفة» replaces «مزدوج» on the old two-shift card.

  Reviewing them found a real bug, now fixed: after a shift was created on the same page, the card still said "needs one shift". Shift and shift-mode changes now refresh the session plan. The baselines of these two specs only were updated, after the images were opened (375 and 1440 px).

## Gates at `phase-4h` (R3)

| Gate | Result |
|---|---|
| `dotnet build -c Release` | 0 warnings, 0 errors |
| `dotnet test -c Release` | 310 passed, 4 skipped (the performance category) |
| ESLint and Stylelint (`npm run lint`) | clean |
| Vitest | 112 passed (29 files) |
| Playwright with axe | 24 passed (full suite, including `phase4-sessions`) |
| Published executable (`SmartSchoolTimetable-20261010-004627`) | `phase4-sessions` on a temporary database (set double shift, map the days, generate once, view S1 and S2, master, print header, Excel term 2) and `phase4-generation` (single-shift regression): 2 passed |
| Owner's real database | not used; `%LOCALAPPDATA%\SmartSchoolTimetable\timetable.db` last written 2026-10-09 14:40, before R3 |
