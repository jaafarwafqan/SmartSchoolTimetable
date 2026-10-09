# Phase 4 follow-up report: R1, R2 (tag `phase-4g`) and R3 (tag `phase-4h`)

- Branch: `phase-4g`, from `phase-4` (`3dee04a`). Not pushed, not merged.
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