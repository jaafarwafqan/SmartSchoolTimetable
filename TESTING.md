# Testing Strategy

## Test layers
- .NET xUnit unit/integration tests cover credentials, auth lifecycle, recovery, session expiry, SQLite migrations/WAL, audit history, unified API errors, request security, loopback binding, and architecture.
- Vitest/Testing Library tests cover Arabic error mapping and accessible password visibility.
- Playwright tests exercise the real built React app and API in Chromium, including setup/recovery confirmation, automatic sign-in, logout/login, wrong password, validation, not-found, offline server, and unexpected 500 paths.
- Scheduling tests in future phases cover domain rules, property-based hard-constraint checks, and measured solver behavior.

## Required security and authentication scenarios
- Setup creates exactly one owner, signs them in automatically, and displays a one-time recovery code with clear store/print guidance. Acknowledgment gates entry.
- Reload before acknowledgment must neither strand the owner nor let them in: the signed-in owner sees a blocking screen (Home and `/settings` unreachable) that permits recovery-code regeneration only with the current password; the former code is invalidated and the new one is shown once and must be acknowledged.
- Password and recovery code are never stored in clear text or written to logs. PBKDF2-HMAC-SHA-256 uses at least 600,000 iterations, and comparisons use `FixedTimeEquals`.
- Recovery code resets the password once, is consumed, and is replaced with a one-time code. No other password-recovery route exists.
- Failed login has a fixed one-second delay only. The delay is injected in tests; no test waits a real second. Do not implement escalating delays or temporary lockout.
- Password change requires the current password and revokes the session. Logout revokes the session. Inactivity timeout locks the authenticated session; `Never` disables inactivity expiry.
- Cookie test asserts HttpOnly, SameSite=Strict, Path=/, and the intentional lack of Secure on loopback HTTP.
- Kestrel startup test inspects actual bound addresses and fails unless exactly `127.0.0.1`. Include rejection of wildcard, LAN, and non-canonical loopback binding configurations.
- Host/Origin protections reject malicious or missing Origin on state-changing requests, DNS-rebinding-style Host changes, absent/stale per-launch tokens, and cross-origin attempts. No wildcard CORS is allowed.
- EF Core database-command logs are Warning by default and Information only in Development; sensitive-data logging is off.

## Unified API errors and localization
- Backend test enumerates all `ApiErrorCodes` and fails if the Arabic dictionary is missing any entry.
- Framework-generated failures, model binding, validation, unsupported methods/media, Origin/token failures, and unhandled exceptions must return `{ code, correlationId, errors }`, with no default ProblemDetails title/detail or user-readable server text.
- FluentValidation errors use field/code pairs only. The UI maps every API code to Arabic, handles unknown/missing codes with a generic Arabic fallback, and localizes offline/network/timeout errors.
- Playwright verifies wrong password, validation, 404 route, stopped/unavailable server, and unexpected 500 alerts contain no Latin letters. Validation (real 422 from the login endpoint), the unknown API route (real 404 JSON), the unknown app route, and the stopped server use the real API process; only the unexpected 500 is mocked with `page.route()` because the real server cannot produce it on demand.
- Error codes: `ErrorContractTests` verifies every `ErrorCodes` constant is registered with an HTTP error status and an Arabic message, scans `src/` so codes are only emitted through those constants, and proves the middleware never returns 2xx/3xx for an error.
- Set `<html lang="ar" dir="rtl">`. Forms use `noValidate`; never use browser-native validation popups, `window.alert`, `window.confirm`, or `window.prompt`.
- Generated PDF/Excel, backup, and import-report user-facing text must be Arabic. Numbers and dates follow tenant preferences once implemented.

## Frontend design enforcement
- The only icon library is `lucide-react`. Every action/navigation item has an icon and an Arabic visible label. Password visibility/close controls are the only permitted icon-only actions and require Arabic `aria-label` and `title`.
- Password eye controls are inside the field at its logical end, use `aria-pressed`, and toggle visibility accessibly.
- ESLint rejects hard-coded JSX text/labels, requires icon plus label on shared `Button` components, and requires an accessible label on native buttons. Review new screens for RTL directional-icon mirroring, logical CSS properties, consistent stroke/size, and WCAG AA contrast.
- Review native browser APIs, form validation, and all translated setup/login/recovery/password/settings messages whenever a screen changes.

## Phase 1.4 test inventory
- .NET (45):
  - `LocalApiTests.cs` (25)
  - `ErrorContractTests.cs` (3 facts + 9 theory rows)
  - `ArchitectureTests.cs` (3)
  - `LocalAuthServiceTests.cs` (1)
  - `InactivityTimeoutSettingTests.cs` (4): runtime apply without restart, invalid/missing/unauthenticated changes, domain choices, and no pending EF model changes
- Vitest (32):
  - `i18n/errors.test.ts` (3)
  - `components/PasswordField.test.tsx` (1)
  - `api.test.ts` (3)
  - `styles/tokens.contrast.test.ts` (25): every pair documented in DESIGN_SYSTEM.md, plus `line-strong` on `surface` at 3:1
- Playwright (3):
  - `auth-flow.spec.ts` (2), which now also changes the inactivity auto-lock in Settings.
  - `design-quality.spec.ts` (1): axe (no serious or critical violations) on setup, recovery code, recovery pending, settings, login and the recovery form; screenshots of login and settings at 375/768/1024/1440 px with no horizontal scroll. Baselines are in `frontend/e2e/design-quality.spec.ts-snapshots/` (Windows/Chromium). Update them with `npx playwright test design-quality --update-snapshots` after an approved design change.
- Lint: `npm run lint` runs ESLint (localization, icon + label, design-system rules) and Stylelint (`color-no-hex`, logical properties, no `font-family` outside `tokens.css`, no `px` font sizes).

## Phase 2 test inventory (updated per checkpoint)
Checkpoint 2A:
- .NET (79): the Phase 1.4 suite plus:
  - `Phase2/SchoolSetupDomainTests.cs`: Arabic normalization, profile and year/term invariants, image signatures, domain-error mapping and list queries.
  - `Phase2/SchoolSetupServiceTests.cs`: Application services against an in-memory `FakeDataStore` (not found, stale versions, record in use, save conflicts, asset clean-up).
  - `Phase2/SchoolProfileApiTests.cs` and `Phase2/AcademicYearApiTests.cs`: every endpoint over the real HTTP pipeline and a temporary SQLite database. They cover the session requirement, validation, 409 conflicts, and uploads including SVG, type mismatch and 413.
  - `ArchitectureTests.FeatureFoldersRespectDependencyDirection`: a feature folder may depend only on shared folders.
- Coverage (Cobertura, `dotnet test --collect:"XPlat Code Coverage"`): Domain 98.9% and Application 97.4% of lines (target ≥ 90%).
- Vitest (40): adds `lib/format.test.ts` (numerals, dates, times, ranges) and `components/ui/menu.test.tsx`.
- Playwright (4): adds `phase2-school.spec.ts`, which covers:
  - profile validation and focus on the first invalid field;
  - logo upload and SVG rejection;
  - a stale edit between two pages (Arabic conflict message and reload);
  - creating a year and a term, the dashboard checklist and the lock screen;
  - axe on the dashboard, profile and years screens, and dashboard screenshots at 375/768/1024/1440 px.

Checkpoint 2B:
- .NET (85): adds domain rule coverage for ISO working days, period generation, ascending/nonoverlapping lesson and break rows, and bell options; API coverage for unauthenticated access, working-week validation, conflict versions, bell settings, shift periods, generated drafts and year structure copying.
- Vitest (41): adds Web Audio tone-preview cleanup and synthesized oscillator checks.
- Playwright (4): all Phase 1/2A end-to-end tests pass; no Phase 2B browser setup journey or axe screen coverage has been added yet.
- `dotnet ef migrations has-pending-model-changes` reports no pending changes after `20261003200446_Phase2BTimetableStructure`.

Checkpoint 2C (after the owner-change review):
- .NET (95): adds `Phase2/StagesSectionsTests.cs` (weekly capacity rule, archive/restore/copy, service rules for duplicates, archive, delete, `STAGE_ARCHIVED`, save-failure mapping, paging and search, year copy linking sections to their copied stage and shift, shift delete in use, protected routes without session, Origin or launch token).
- Coverage: Domain 97.7% and Application 94.1% of lines.
- Vitest (46): adds `periodRows.test.ts` (row-level API errors, lesson numbering, new rows), tone names matching the API, and `styles/customProperties.test.ts`, which fails when CSS uses an undefined `var(--x)`.
- Playwright (4): `phase2-school.spec.ts` now also creates a shift, checks generator validation, shows a row-level overlap error, saves generated periods, adds a stage and a section (capacity 30 = 5 days × 6 lessons), runs axe on both screens, and records period screenshots at 375/768/1024/1440.

Checkpoint 2D:
- .NET (99): adds `Phase2/SubjectsTests.cs`:
  - the grid membership rule and the subject field rules;
  - service duplicates, versions, archive and delete;
  - dashboard counts;
  - routes on real SQLite, including replacing the owned blocked-period rows and the 422 before periods exist.
- Coverage: Domain 98.0% and Application 94.5% of lines.
- Vitest (48): adds `components/ui/blocked-periods-grid.test.tsx` (a single tab stop, arrow/Home/End navigation, toggling, the empty state).
- Playwright (4): the phase 2 flow adds a subject (palette swatch, priority, a blocked cell toggled with the keyboard) and runs axe on the subject dialog and list.
- All screenshot baselines were regenerated. `maxDiffPixelRatio` is now 0.002, because 0.01 let stale baselines pass despite new tiles and new sidebar items.

Checkpoint 2E:
- .NET (104): adds `Phase2/TeachersTests.cs`:
  - every teacher rule, including limits before and after periods exist;
  - short-name proposals;
  - service CRUD, the release-date parsing, filters and sorting;
  - bulk preview statuses and refusal of a whole batch;
  - routes for the limit codes, release dates, bulk add, the `released` filter and the missing-token check.
- Coverage: Domain 98.3% and Application 94.9% of lines.
- Playwright (4): the phase 2 flow adds a teacher (an off day, a limit error with focus moved to the field, the release toggle) and a bulk add with a preview (one line flagged "already exists"). It runs axe on the teacher dialog, the bulk preview and the list, and records teachers screenshots at 375/768/1024/1440.

Checkpoint 2F:
- .NET (108): adds `Phase2/CalendarAndDemoDataTests.cs`:
  - calendar rules;
  - routes: the outside-year warning, the month window, invalid filters, conflict and delete;
  - the demo target check: a missing path, a protected path (compared without touching the file), an existing file;
  - a complete dual-shift demo database in a temporary folder: 20 teachers including 2 released, 10 subjects, 4 stages, 12 sections, 2 shifts of 7 lessons plus a break, 8 calendar days, every checklist step done, no capacity gaps.
- Vitest (50): adds `features/calendar/monthGrid.test.ts` (ISO weekdays, month bounds, weeks starting on the school's week start day).
- Playwright (4): the phase 2 flow ends with the calendar (an out-of-year warning, kind selection, the month view, axe on both views). Baselines were regenerated with `maxDiffPixelRatio` 0.0002, so a new sidebar item or tile fails the comparison; two consecutive runs were stable.
- Manual check: `--seed-demo-data` was run against a scratch path. It created the file, then refused the same path on a second run, and printed an Arabic message when the path was missing.

## Phase 2.5 test inventory (updated per checkpoint)
Checkpoint 2.5A:
- .NET (110): adds `Phase25/QuickAddTests.cs` (automatic subject colours and cycling, default priority, proposed teacher short names and the fallback when none is free).
- Vitest (58):
  - `components/DateTimeFields.test.tsx`: ISO composition, day/month/year order, Arabic-Indic digits, typing either digit set, arrow keys, 24-hour wrap, minutes step 5, LTR time group.
  - `LtrText`: year order and `ltrRuns`.
  - `i18n/noLatinText.test.ts`: no Latin letters anywhere in the dictionary.
  - `styles/noDrawers.test.ts`.
- ESLint adds `design-system/no-native-date-time` and `design-system/no-drawers`. A probe file proved that both fire.
- Playwright (4):
  - The flow uses the new navigation (sidebar group, then tab), segmented date and time fields, and quick add with in-place details for subjects and teachers. The bulk panel replaces the bulk dialog, and the calendar has a quick-add row.
  - Every 3–6 field dialog is checked to be centred, 32–40rem wide and without inner scrolling at 1280×720.
  - Five screens are checked for Latin text.
  - A spaced year label ("2027 - 2028") is checked for display order.
  - The phone menu is checked to open in the page flow (no dialog, `position: static`) and to return focus on Esc.
  - All screenshot baselines were regenerated.

Checkpoint 2.5B:
- .NET (115): adds `Phase25/DayLessonsAndShiftModeTests.cs`:
  - per-day storage and its validation, capping after periods shrink, capacity sums;
  - the grid per day and its weekly bound, teacher limits against it;
  - setup progress masks;
  - shift mode: no current year, adopt by name, create, remove, blocked by sections with the impact list, stale version;
  - routes for day lessons, the grid, shift mode and setup progress.
- Vitest (60): adds `components/ui/stepper.test.tsx` (buttons, keys and bounds; grid cells for lessons that do not exist on a day are aria-disabled and never toggle).
- Playwright: the flow lowers Thursday to 5 lessons with the stepper, and section capacity becomes 29 (also in the copied year).

Checkpoint 2.5C:
- .NET (126): adds `Phase25/CurriculumDomainTests.cs` and `Phase25/CurriculumServiceTests.cs`:
  - section labels, curriculum entry validation and labels, totals under/equal/over per shift;
  - the template JSON files (keys, school types, branch stages, presets generate, no lesson counts);
  - the stepper (next labels, removing the last, errors); repeated subjects and totals in the table, conflicts, clearing, archive, delete;
  - copy and set-across previews, idempotence, `ambiguous`, never overwriting; protections (`CURRICULUM_IN_USE`);
  - templates create only what is missing and never lower sections; the year copy carries curriculum lines;
  - routes, including a failing stage template rolled back on the real SQLite database, and multi-break generation.
- Vitest (62): `components/ui/edit-grid.test.tsx` (arrow keys, RTL direction, edges) and `features/curriculum/curriculumGrid.test.ts` (cell parsing, Arabic-Indic digits).
- Playwright, on the copied year:
  - applies the preparatory stage template (preview, apply, then preview again shows no change);
  - raises and lowers sections with the stepper (removal confirmed);
  - fills a curriculum cell (under by 23) and cancels an invalid value with Escape;
  - adds a repeated line «هندسة» (under by 21);
  - runs axe and the Latin-text check.

Checkpoint 2.5D:
- .NET (127): `Phase25/SetupWizardTests.cs` on the real SQLite database:
  - steps 1–3 save through the services and can run again without changes (one year, two terms, two shifts);
  - a failing timing step rolls back the working week too;
  - duplicate shift kinds, an invalid school type, no current year;
  - step 1 refused with `SHIFT_MODE_IN_USE` and rolled back;
  - the review counts and warnings; the dashboard curriculum status and `setupFinished`.
- Vitest (66): `features/setup-wizard/wizardLogic.test.ts` (the proposed year from today, term dates, weekly lessons with per-day exceptions, reading a saved shift back for resume).
- Playwright (5): `phase25-wizard.spec.ts`, scenario (a):
  - a fresh owner lands in the wizard and sets up a morning-only intermediate school through all seven steps;
  - covers a required-name error, a per-day exception (34 a week), leaving and resuming at step 4 from the dashboard, the template re-applied without changes, a curriculum cell, the optional step skipped, the review, finishing, the dashboard status and the Settings entry;
  - axe and the Latin-text check on the steps.
  - `setupOwner` now postpones the wizard; dashboard and settings baselines were regenerated (new cards).

Fixes `phase-2-5-fix1` (owner findings B1–B8):
- Vitest (70): `lib/arabicCount.test.ts`:
  - every noun at 1, 2, 3, 11 and 100, plus the boundaries 10, 99, 103 and 111, in both numeral systems;
  - the Iraqi month names for all twelve months;
  - a guard that fails on any `${number} noun` string in `src` (shown to catch a planted violation).
- Playwright (7): `phase25-fixes.spec.ts`, on a secondary school with nine stage columns:
  - B5: step labels and their order;
  - B3: no horizontal page scroll on every wizard step and every main screen at 375, 768, 1024 and 1440px;
  - B4: no overlapping text (line boxes) on the periods preview, stage cards, curriculum table, dashboard and settings;
  - B6: a typed subject appears as a chip «مضافة» and as a table row, is removed and restored with «تراجع»;
  - B7: the count message for the added suggestions;
  - B1/B2/M3: one header and one totals cell per stage, aligned, headers not clipped, compact inputs below the header, capacity in the header;
  - B8: Iraqi month names on the year step and the years list;
  - a self-test showing the overlap guard rejects the old stacked-totals markup.

Fixes `phase-2-5-fix2` (M1, M2):
- .NET (133): `Phase25/StageLessonsAndBreaksTests.cs`:
  - break durations and the gap (exact times), at most three breaks, gap range, suggested defaults;
  - stage lessons: inheritance, capping by the shift, validation codes, clamping, copy, reset;
  - routes: stage lessons per day (wrong token, out of range, version conflict, not found), section and curriculum capacity per stage, shift-shortening impact, refusal without confirmation and lowering with it;
  - the migration upgrading an existing database from `Phase25CCurriculumTemplates`.
- Vitest (73): `features/timetable-structure/breaks.test.ts` (positions, at most three, durations and the gap read back per shift).
- Playwright (8): `phase25-model.spec.ts`:
  - the breaks editor changes the live preview (duration, an added break, the gap), and the saved breaks have the exact times;
  - a stage with fewer daily lessons gets a smaller capacity, while the other stage keeps the shift's;
  - per-day editing on the card; curriculum headers and totals per stage;
  - the shorten-shift confirmation with the affected stage;
  - axe and the overlap guard.
- Flake fixed: adding a subject while the curriculum's first load was in flight left the table stale; mutations now cancel then refresh (decision #38), and the spec passed 8 runs in a row.

Checkpoint 2.5E:
- .NET (138):
  - `Phase25/WizardFailureInjectionTests.cs`: a `FailingDataStore` wraps the real store in `TestHost`. A save that throws in the middle of a wizard step (inside a nested transaction, on a later save, or on the progress record) rolls back the whole step on real SQLite.
  - `Phase25/SectionEditsTests.cs`: one section moved to the other shift splits the totals; archiving a middle section keeps its label reserved, and the stepper removes only the last active section.
  - `CalendarAndDemoDataTests`: both demo variants, sample curriculum lines marked as demo, per-stage capacity.
- Playwright (9):
  - `phase25-scenarios.spec.ts`: scenario (b) through the wizard, plus (c), (d) and (e), and screenshots of wizard steps 1–7 and the curriculum at four widths (page clock fixed to 2026-10-05).
  - `phase25-wizard.spec.ts`: scenario (a).
  - Both print the typed-versus-chosen UX metric (`e2e/support/ux.ts`).
- `expectNoTextOverlap` ignores text hidden under an opaque layer (for example rows scrolling under the sticky totals row) and still rejects transparent text drawn over text.

Suggested curriculum (`phase-2-5-curriculum`):
- .NET (150): `Phase25/SuggestedCurriculumTests.cs`:
  - the JSON against the owner's totals (all and without optional), lessons 1–15, stage names = stage-template names, exactly three `needsReview` stages, aliases;
  - the distribution examples 28/27/29/30/31/33, six working days, a short Thursday, above capacity, below the number of days, no curriculum;
  - flags cleared by owner edits;
  - primary apply: preview saves nothing; an alias is matched, not duplicated; totals 28/28/27/29/30/30; apply twice = 0 changes; an edited value survives; the reset needs `confirm` and shows before/after;
  - the daily suggestion makes every stage "equal", manual counts are skipped, a later change shows the notice, above capacity is refused, unknown stage ids give 422, and the request without a token gets 403 and without a session 401;
  - intermediate without French 30/30/31 and with French 33/33/34; Kurdish unchecked; the literary review flag; the dual-shift bound (30);
  - the migration upgrading an existing database.
- Playwright (13): `phase25-curriculum.spec.ts`:
  - (a) primary six grades, then the daily suggestion → six "مطابق";
  - (b) intermediate 30/30/31, then with French 33/33/34;
  - (c) preparatory review warnings in the panel and the header;
  - (d) a dual-shift ثانوية (السادس العلمي above the evening capacity, with the reason; eight stages "مطابق");
  - (e) the second apply changes nothing;
  - (f) an edited value stays, and the reset restores it after a before/after confirmation;
  - (g) axe, no horizontal page scroll and no overlapping text at 375, 768, 1024 and 1440px.

Official study plan 2026-2027 (`phase-2-5-official-curriculum`, ADR 0036) replaces the numbers above:
- .NET `Phase25/SuggestedCurriculumTests.cs`:
  - `TheOfficialTemplateTotalsMatchThePrintedPlan`: version 2, `status: official`; for every stage the mandatory rows plus Kurdish equal the printed total, except الرابع الابتدائي (31 against 30, `needsReview`, a verification note); exactly الرابع الابتدائي and الرابع العلمي flagged; Kurdish counted, French/computing/حزب البعث on top; the aliases.
  - primary apply: totals 30/30/30/31/30/31; one «اللغة العربية» for «(قراءتي)» and the plain spelling; nine subjects; apply twice = 0 changes.
  - `OptionalSubjectsAndReviewWarningsFollowTheSchool`: default 30/30/30/28/28, an unticked optional subject is never created; Kurdish and French ticked 32 each.
  - `KurdishIsOptionalButCountsInTheOfficialTotal`: fourth/fifth grades 28/28/29/30 unticked, exactly the printed 30/30/30/31 ticked; one Kurdish subject; a third apply changes nothing; unticking never deletes.
- Playwright `phase25-curriculum.spec.ts`: (a) الرابع الابتدائي review note and totals; (b) French and computing ticked → 34/34/32 and both rows in the curriculum tab, with axe; (c) the الرابع العلمي question, Kurdish ticked → 30; (d) seven stages "مطابق" (السادس العلمي 33 and السادس الأدبي 31 above the evening 30).

## Phase 3 test inventory (updated per checkpoint)
### 3A - hardening
- .NET (`Phase3/`):
  - `ReferenceProtectionTests`: the stage report (2 sections and 1 line, names, blocking codes); stage archive/delete refused with `RECORD_IN_USE`; subject archive/delete refused with `CURRICULUM_IN_USE`; after the line is archived the subject can be archived but still not deleted; a shift used by sections cannot be deleted; unreferenced kinds report nothing and delete normally; an unknown kind is 404; the preview needs the owner session.
  - `CurriculumClearUndoTests`: clearing a cell archives the line and returns it; the subject's report shows it as archived history; restoring brings the value back.
  - `OrphanBlockedPeriodsTests`: lowering lessons per day from 6 to 4 reports exactly the teacher's 2 and the subject's 1 orphan slots, removes nothing by itself, refuses a stale version, cleans only the confirmed teacher (version + 1) and leaves the subject's orphan.
  - `QueryCountTests`: ten list and report endpoints run the same number of SQL commands (counted by `QueryCounter`, a `DbCommandInterceptor` in `TestHost`) for a school with 1 stage and one with 3 stages, 18 sections, 9 subjects, 13 teachers and 25 lines.
- Vitest: `components/References.test.tsx` (dependent wording with `arabicCount`: delete counts archived ones, archive names only active ones, hidden names are counted).
### 3B - resources, specializations, profile
- .NET `Phase3/ResourcesAndProfileTests`:
  - resource validation (name, kind, capacity 1–20) and versions;
  - the profile's default weights 20/30/15/25/10; `ProfileVersion` + 1 per real change; same rules in another order change nothing; missing, unknown, duplicate and out-of-range rules are rejected; restore defaults;
  - specializations: kept when not sent, added once, at most 30;
  - API: resources (duplicate name, kind, capacity, paging, sort, search, conflict, 401, 403); a subject requiring the field blocks archive and delete with `RESOURCE_IN_USE` and the report names it; an archived resource cannot be newly chosen; clearing the requirement frees it;
  - specializations through the API, including the one-click add and removal with a deleted subject;
  - the profile API (versions, conflict, `Rules` validation, restore needs `Confirm`);
  - migration `Phase3BResourcesProfile` on a database at `Phase25SuggestedCurriculum` with an existing subject.
- Vitest: `profileApi.test.ts` (weight choices in steps of five, keeping a saved odd value).
- Playwright `phase3-resources-profile.spec.ts`: quick add of two resources (kind, capacity stepper); the subject's required-resource chooser and row badge; the delete dialog naming the subject with «حذف» disabled; the profile's weight change (version 2) and restore defaults (version 3); axe, no horizontal scroll, no overlapping text and screenshots at 375/768/1024/1440.
### 3C - workload
- .NET `Phase3/WorkloadTests`:
  - domain: reassign, archive, restore;
  - availability: shared slots counted once, two shifts separately; off day, blocked lesson, 5 a day and 20 a week give 23 / 20 / 20; released gives 0; a blocked lesson number blocks both shifts;
  - API cells: assign, conflict for an empty cell assigned meanwhile, stale version, reassign outside the specialization (flagged), clear, invalid line/section, 401, 403;
  - loads: 10 of 12 «ضمن الحد», 11 of 12 «قريب», 17 of 12 «تجاوز»;
  - bulk: across the stage without overwrite (create + skip), the applied plan equals the preview, applying again changes nothing, overwrite replaces and moves Ali's load 5 → 0; class teacher; transfer (Ahmed 11 → 0, Ali 0 → 11); remove;
  - protection: teacher and section archive/delete and the stepper refused with `WORKLOAD_IN_USE`, the report names «الأول المتوسط / ب: الرياضيات — أحمد علي حسن»; clearing a line needs `confirmWorkload`, archives its assignment, and the undo restores it; archiving a line through its endpoint follows the same rule;
  - the filtered unique index: one active row per (section, line), archived rows allowed.
- `QueryCountTests` now also covers resources, the workload matrix and the loads.
- Vitest `features/workload/workload.test.tsx`: the teacher chooser (specialists, current teacher, «عرض الجميع»), shortage order and message, load bar width and meter values, the bulk plan view, counting «نصاب».
- Playwright `phase3-workload.spec.ts`: assign in the matrix, «خارج التخصص» and «إضافة المادة لتخصصاته», completion «٢ من ٢», the shortage warning «المسند ١١، المتاح ١٠، يزيد ١», bulk class teacher with preview and confirmation, load bars by teacher, the teacher delete dialog naming «نصابان», clearing an assigned curriculum line with confirmation and undo; axe, no scroll, no overlap and screenshots at 375/768/1024/1440.
### 3D - scheduling input and readiness
- .NET `Phase3/PreSolveValidatorTests`: exact 28/25/3 teacher and 7/5/2 physics bounds; unassigned grouping; section stage-day capacity; off days, blocked periods, release and daily/weekly limits; assignment intersections; per-shift resource capacity; double periods; consistency warnings; empty-stage reporting; deterministic hash ordering and scheduling-relevant changes.
- .NET `Phase3/ValidatorPropertyTests`: 300 generated feasible schools must have zero errors; 300 known teacher-limit reductions must identify only that teacher with the exact shortage. Seeds are deterministic.
- .NET `Phase3/ReadinessTests`: unauthenticated API rejection; a persisted unassigned workload appears in the report and becomes ready after class-teacher assignment; checklist completion is based on active assignments; a 40-section × 9-line snapshot/hash/validate benchmark must finish below one second.
- `QueryCountTests` includes readiness and compares the number of SQL commands for small and larger schools.
- Vitest `features/readiness/readinessPresentation.test.ts`: exact teacher/physics Arabic numbers, grouping by entity and no double-counting overload shortage. `lib/arabicCount.test.ts` covers error, warning and pair forms and guards against manual counted-noun strings.
- Playwright `phase3-readiness.spec.ts`: real dashboard readiness card and unassigned-line finding; exact section-capacity warning; axe; no horizontal scroll and text overlap at 375/768/1024/1440; reference screenshots viewed before acceptance.
- `SchedulingSection` adds the matrix, load bar/status, and readiness finding to the development-only `/design` guide.
### Phase 3 finish (`phase-3-finish`: 3D fixes and verification, 3E)
- .NET `Phase3/FindingCodeContractTests`: every `FindingCodes` constant is in `All` exactly once; every code has Arabic text and a TypeScript union member in the frontend; no finding-code literal outside `FindingCodes.cs`. `ErrorContractTests` is unchanged and treats `FindingCodes.cs` as a definition site.
- .NET `Phase3/PreSolveValidatorTests`: double periods in both modes (warning by default, error with `DoublePeriodsRequired`); the readiness API with `doublePeriods=true`.
- .NET `Phase3/ValidatorPropertyTests`: also the double-lesson mode on valid timetables (zero errors) and per-stage day counts that differ between days.
- .NET `Phase3/SchedulingInputHashTests`: reading order and names never change the hash on generated schools; lesson counts, blocked periods, teacher limits, resource capacity, profile weights and assignments do.
- .NET `Phase3/WizardStepMigrationTests`: finished wizard, wizard in the middle, no progress row, `Down` restores the seven-step masks and keeps the review undone.
- .NET `Phase3/AssignmentSuggesterTests`: the preview is deterministic, balanced, within limits and equal to the applied result; existing assignments never change; no teacher above the limit (reason `capacity`).
- .NET `Phase3/ReadinessTests`: foreign Origin 403, foreign Host 400, no session 401, suggestion apply without the launch token 403; the 40-section test prints its timing.
- .NET `CalendarAndDemoDataTests`: the default demo completes every checklist item and is ready; `--with-problems` shows the three readiness errors.
- Mutation checks: `docs/phase3-mutation-checks.py` applies 18 mutants one at a time to the working copy, runs the tests and always restores the file (never committed). All 18 are caught.
- Vitest: `features/readiness/readinessPresentation.test.ts` (every code has Arabic text, the unknown-code fallback, the double-period note), `features/design-guide/SchedulingSection.test.tsx` (the style-guide matrix, load bar and finding render).
- Playwright `phase3-scenarios.spec.ts`: (a) a 12-section primary school assigned with the class-teacher bulk action, then ready; (h) a 12-section intermediate school assigned by the suggester (preview = applied, existing kept); (c) a resource shortage fixed by raising the capacity; (d) protection of subject, section and stage with the dependents listed; (f) orphan blocked periods previewed, then removed. (b) 28/25 and (g) readiness + dashboard are in `phase3-readiness.spec.ts`, (e) undo in `phase3-workload.spec.ts`. Wizard step 7 «الأنصبة» has screenshots in `phase25-scenarios.spec.ts`. The report's check time and hash are masked in screenshots.
- Final Phase 3 run: Release build 0 warnings; .NET 214; Vitest 91; Playwright 21; coverage Domain 97.3%, Application 95.4% of lines.

## Later-phase acceptance suites
- Phase 4 infeasibility test must construct a conflict involving two teachers, a shared lab, and a blocked period; the diagnostic must identify the conflict groups and actionable correction, not merely report infeasible.
- Large solver-risk comparison uses the same independently verified feasible 40-section/54-teacher workload and records status, first-solution/total time, objective/bound, memory method, and independently checked hard constraints for baseline, two-stage, decomposition, and hints.
- Phase 6 SQLite backup tests verify online backup is a self-contained, integrity-checked single file in WAL mode. A copy-based backup must checkpoint/quiesce/close before copying; directly copying an active main `.db` is unsupported.
- Phase 8 reruns setup/login/recovery/auto-lock, loopback listener and localhost-attack tests against the packaged build, with external networking disabled.

## Quality expectations and scope
- Domain and scheduling logic: 90%+ coverage target.
- Do not add acceptance tests for multi-tenancy, RBAC, refresh-token rotation, remote sync, or multi-user concurrency; these are out of scope.
- Phase 0 validation is complete documentation and isolated spikes; current application tests are Phase 1/1.2 implementation validation, not Phase 2 work.
