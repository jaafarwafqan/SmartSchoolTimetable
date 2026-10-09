# Changelog

## [Unreleased]
### Phase 2.5 - template update with the owner's decisions (branch `phase-2-5-template-update`)
- **Official template updated by the owner (#66–#68 decided):** الرابع الابتدائي totals 30 (الاجتماعيات 2, with a note); منهج جرائم حزب البعث moved from the fourth grades to الخامس العلمي/الخامس الأدبي (optional, one lesson, on top). Every stage now matches its printed total and no stage carries a review note; `needsReview`/`verificationNote` support stays.
- **Demo data generator removed (owner decision):** `Infrastructure/DemoData/` (DemoCatalog, DemoDataSeeder, DemoSchool) and the `--seed-demo-data`/`--dual-shift`/`--with-problems` command are gone; its three tests were removed and `Phase2/CalendarAndDemoDataTests.cs` became `CalendarTests.cs` (calendar tests only). Tests build synthetic data inside the test projects. README, DOMAIN, DATABASE and the owner test scripts say so.
- **Period presets are labelled suggestions:** «قالب مقترح للحصص» with a hint that break lengths and lessons per day are the owner's choice.

### Phase 2.5 - school-type stage template guard
- The saved school profile is now the authority for stage templates. Preview and apply reject a mismatched school type, an out-of-type grade, or an invalid/missing branch with `STAGE_NOT_IN_SCHOOL_TYPE`.
- Changing school type from the stage-template panel saves the profile; existing stages are retained and active template stages outside the new type are listed for review.
- Added API coverage for all four Iraqi school structures, invalid stage requests, profile type changes, and retained stages.

### Phase 2.5 - official study plan 2026-2027 (branch `phase-2-5-official-curriculum`)
- **The curriculum template is now the Ministry's official plan 2026-2027** (`iraq-curriculum.official-2026-2027.json`, template version 2; ADR 0036). It replaces the unverified `iraq-curriculum.suggested.json`.
- **Optional subjects** start unticked and are never created unless ticked: اللغة الكردية (counted in the official total), اللغة الفرنسية, الحاسوب and منهج جرائم حزب البعث (added on top).
- **Preview** shows «المجموع الرسمي» and «المحسوب للمواد المفعّلة» for each stage, the source's review note (الرابع الابتدائي 31 against 30; الرابع العلمي and حزب البعث), and the fixed source line. None of these block applying.
- **Aliases** fold «اللغة العربية (قراءتي)», «التربية الفنية», «مبادئ الاقتصاد», … into one subject. Subjects are created under the canonical name, so a K-12 school gets one «التربية الفنية والنشيد», not two.
- **API:** the plan gains `templateVersion`, and `provenance` is an object. Subject lines gain `inStatedTotal` and `note`, stage lines gain `officialTotal` and `verificationNote`, and entry lines gain `inStatedTotal` and `note`.
- **Tests:** template totals against the printed plan, Kurdish on/off, apply twice, unticked optional not created; Playwright: preview → French and computing ticked → apply → rows in the curriculum tab.
- Test data follows the heavier official loads: `phase3-scenarios` (h) ticks الحاسوب, uses «التربية الفنية والنشيد» and adds a third الاجتماعيات teacher (4 lessons per intermediate grade). The curriculum, wizard step 5 and suggester screenshots were re-baselined after viewing them.
- Open questions: DECISIONS_PENDING #66–#68.

### Phase 3E - suggester, wizard step, demo data, scenarios (tags `phase-3e`, `phase-3-final`)
- **«اقتراح توزيع الأنصبة»:** a deterministic suggester for unassigned lines only (specialists, the lowest load share, never above the limit). Preview first, then a centred confirmation; the applied result equals the preview and existing assignments never change.
- **Wizard step 7 «الأنصبة»** (eight steps). Data migration `Phase3EWorkloadWizardStep` moves a saved review step; `Down` restores it.
- **Demo data:** about 20 sample teachers with specializations and constraints, a sports field (capacity 2) and a computer lab (capacity 1), assignments made by the suggester, so every checklist item is done and the school is ready. `--with-problems` leaves an overloaded teacher, physics with too few allowed slots and a field shortage.
- **Readiness screen:** a group's lesson shortage now has a label («النقص: …»). **Bulk actions** offer only the chosen stage's sections and lines while another stage loads. The suggester heading is an `h3` inside the wizard.
- **Playwright scenarios (a)–(h)** on temporary databases (`phase3-scenarios.spec.ts`, plus `phase3-readiness` and `phase3-workload`), with axe, no horizontal scroll and no text overlap at four widths; screenshots viewed before acceptance; the check time and hash are masked.
- **UX metric** (scenario a, 12-section primary school): 0 typed, 26 chosen, 37 commands to assign every line.
- `/design` scheduling components rendered by a Vitest test. Docs: DOMAIN, DATABASE, API, DESIGN_SYSTEM, DELIVERY_PLAN, TESTING, README, `docs/OWNER_TEST_SCRIPT_PHASE3.md`, `docs/PHASE3_REPORT.md`.

### Phase 3D - scheduling input and pre-solve readiness (tag `phase-3d`)
- **Phase 3 finish fixes:** finding codes declared once in `FindingCodes` with a contract test (B1); exact wizard locators (B3); the Phase 2 checklist test completes the workload step (B4); the workload matrix screenshot re-baselined after viewing it (B5).
- **Double-period severity** follows the generation mode: a warning by default, an error with `?doublePeriods=true` (DECISIONS_PENDING #65).
- **Verification:** 18 mutation checks, all caught; hash, migration and readiness security tests; the 40-section check in about 40 ms.
- Added the serializable `SchedulingInput`, canonical SHA-256 `InputHash`, pure conservative pre-solve validator and authenticated readiness API.
- Added «جاهزية الجدولة» with grouped findings, numeric Arabic messages, actionable links, refresh, hash/time and the live dashboard readiness card.
- Added the dashboard checklist step «تعيين المعلمين على المنهج», computed from active curriculum lines and assignments.
- Added FsCheck soundness properties, 40-section performance coverage, readiness API/query-count tests, Vitest presentation tests and a Playwright/axe/responsive scenario.
- Documented ADRs 0033–0035 and the Phase 4 input boundary. No solver or generation functionality was added.

### Phase 3C - workload assignments (tag `phase-3c`)
- **«الأنصبة»** tab next to «المعلمون» (ADR 0032, #59).
  - **«حسب الشعبة»:** a matrix per stage (sections × curriculum lines) with a teacher chooser in each cell. The chooser lists the subject's specialists, or everyone with «عرض الجميع», and shows each teacher's load.
    - Empty cells say «غير معيّن»; each section shows «x من y».
    - «خارج التخصص» comes with «إضافة المادة لتخصصاته».
    - Keyboard: left/right between cells; a choice is saved on Enter or on leaving the cell (#60).
  - **«حسب المعلم»:** load bars (assigned / limit) with «ضمن الحد، قريب، تجاوز», the numbers (assigned, max per week, available) and the assignment list.
  - **Quick warnings:** «المعلم …: المسند …، المتاح …، يزيد …».
  - **Bulk actions,** each previewed then confirmed, never overwriting unless chosen: across a stage, class teacher of a section, transfer, remove.
- **Teacher rows** show «النصاب: x من y» and the status when not within.
- **Protection** (`WORKLOAD_IN_USE`): teachers and sections with assignments cannot be archived or deleted, and the stepper never removes them.
  - Clearing or archiving a curriculum line with assignments opens a dialog listing them. Confirming archives them with the line; the undo restores both.
- **Domain:** `WorkloadAssignment` and `TeacherAvailability` (#57, #61, #62). Migration `Phase3CWorkload`.
- **Tests:** .NET 173, Vitest 83, Playwright 15. The Phase 2 teachers screenshots were re-baselined after viewing them (new tabs and load badges); teacher, subject, curriculum, stage and timing changes now refresh the workload views.

### Phase 3B - resources, specializations, scheduling profile (tag `phase-3b`)
- **«الموارد»** tab under «الصفوف والمنهج» (ADR 0031):
  - Quick add in one row: name, kind (مختبر، ساحة، قاعة، أخرى) and a capacity stepper. Rows are edited in place; archive and delete are protected.
  - A subject's row offers «المورد المطلوب» and shows «يتطلب …». A resource required by subjects cannot be archived or deleted (`RESOURCE_IN_USE`); the dialog names the subjects.
- **Teacher specializations:** subject checkboxes in the teacher editor and a summary badge on the row.
  - `POST /teachers/{id}/specializations/{subjectId}` adds one subject (used by the 3C warning action).
  - Deleting a subject removes it from specializations (#53).
- **«ملف الجدولة»** in a new Settings tab: five soft rules with on/off and a weight (0–100, steps of five), the profile version, and «استعادة الإعدادات الافتراضية» with confirmation. Settings now has the tabs «عام» and «ملف الجدولة» (#55).
- **Editors** for teachers and subjects say how many blocked periods outside the grid will be removed on save (#56).
- **Migration** `Phase3BResourcesProfile`; new code `RESOURCE_IN_USE`.
- **Tests:** .NET 165, Vitest 78, Playwright 14 (the settings screenshots were re-baselined for the new tabs).

### Phase 3A - hardening (tag `phase-3a`)
- **Build output is no longer tracked:** `src/SmartSchoolTimetable.Api/wwwroot/` is ignored and removed from the index (the build regenerates it). A new `.gitattributes` normalizes line endings and marks binary files.
- **Reference protection** (DECISIONS_PENDING #48):
  - One `ReferenceGuard` answers what depends on a stage, section, subject, teacher, shift, resource or curriculum line. Every delete and archive path uses it.
  - `GET /references/{kind}/{id}` previews the dependents.
  - The delete dialogs list them in Arabic and keep «حذف» disabled while anything depends on the record. A refused archive opens a dialog that lists the active dependents.
- **Clearing a curriculum cell is a soft delete** with «تراجع عن الإفراغ» (#49, replaces #25).
- **Orphan blocked periods** (#50): after the working days or lessons per day shrink, the timing, teachers and subjects screens show how many blocked periods fall outside the grid. «مراجعة الحصص المحجوبة» lists them per teacher and subject and removes them only after confirmation.
- **Reads:**
  - list and report queries run without change tracking (#52);
  - a query-count test proves ten list endpoints run a constant number of SQL commands as the data grows;
  - record lists stay paged (#51).
- **README** documents `npm run audit:prod`.
- Cherry-picked the npm audit decision (#44) onto `phase-3`.
- **Tests:** .NET 158, Vitest 76, Playwright 13; Release build 0 warnings.
### Phase 2.5 suggested Iraqi curriculum (tag `phase-2-5-curriculum`)
- **«تعبئة المنهج المقترح»** in the curriculum tab and wizard step 5 (ADR 0028, 0029).
  - Uses the owner's suggested weekly lessons for primary, intermediate and preparatory stages; unverified, labelled «مقترح» with a provenance banner.
  - Preview first, then an idempotent, non-destructive apply. Existing subjects are matched through aliases. Optional Kurdish and French start unchecked.
  - The three literary stages carry a review warning.
  - Suggested cells show «مقترح» until edited; «إعادة المقترح لهذه المرحلة» restores values after a before/after confirmation.
- **«اقتراح توزيع الحصص اليومية»** on the stages screen, the curriculum tab and wizard step 5 (ADR 0030).
  - An even split of each stage's curriculum total, with the earlier days longer, never above the shift.
  - Manual counts are never overwritten, and a notice appears when the total changes.
- **Migration** `Phase25SuggestedCurriculum`; new code `DAILY_TOTAL_ABOVE_SHIFT`.
- **Tests:** .NET 150, Vitest 73, Playwright 13.

### Phase 2.5E - scenarios, demo data, report (tags `phase-2-5e`, `phase-2-5-final`)
- **E2E scenarios (a)–(e):**
  - (b) is a dual-shift ثانوية with branches set up through the wizard.
  - Screenshots of every wizard step and the curriculum at 375, 768, 1024 and 1440px.
  - Typed-versus-chosen UX metric: (a) 2 typed, 3 chosen; (b) 2 typed, 9 chosen.
- **Failure-injection tests** for the wizard's one-transaction steps.
- **Tests** for a single section's shift change and for archiving a middle section.
- **Curriculum totals row** stays visible at the bottom of the scrolling table.
- **Demo data:** an Iraqi secondary school (morning-only and dual-shift), created through the templates.
  - The first grade has 6 lessons a day.
  - The sample curriculum includes a repeated subject; every line is marked as a demo number.
- **Docs:** `docs/OWNER_TEST_SCRIPT_PHASE25.md` (Arabic, 28 steps) and `docs/PHASE25_REPORT.md`.
- **Tests:** .NET 138, Vitest 73, Playwright 9; coverage Domain 98.7%, Application 95.6%.

### Phase 2.5 fixes 2 - owner model changes M1–M3 (tag `phase-2-5-fix2`)
- **M1 editable breaks (ADR 0026):** up to three breaks per shift, each with its own position and duration, plus an optional gap between lessons.
  - Presets only fill the values; the suggested length per school type is 15 minutes, clearly marked as a suggestion.
  - Wizard step 3 and the periods generator share `BreaksEditor`; the morning and evening shifts are independent.
- **M2 lessons per day per stage (ADR 0027):** «عدد الحصص اليومية» with «تعديل لكل يوم» on each stage card (wizard step 4 and the stages screen).
  - Capacity of sections, stage cards, curriculum totals, the dashboard and the review use the stage's own counts.
  - Shortening a shift below a stage is previewed and confirmed.
  - Migration `Phase25FixStageDayLessons`; new code `STAGE_LESSONS_ABOVE_SHIFT`.
- **M3:** each stage's weekly capacity under its name in the curriculum header (done in fix 1).
- **Stale data fix:** mutation hooks cancel in-flight reads before refreshing them.
- **Tests:** .NET 133, Vitest 73, Playwright 8.

### Phase 2.5 fixes 1 - owner findings B1–B8 (tag `phase-2-5-fix1`)
- **B1:** curriculum totals are one compact `<tfoot>` cell per stage column, with planned out of capacity and a status chip. The old cells were turned into grids and stacked into one column.
- **B2:** curriculum header: full stage names that wrap, the weekly capacity under each name (M3), sticky header and subject column with opaque backgrounds, compact 3rem inputs. A shared rule had made every `th` stick to the top.
- **B3:** no horizontal page scroll. Grid and flex children shrink (`min-inline-size: 0`); only table containers scroll.
- **B4:** `expectNoTextOverlap` and `expectNoPageScrollX` Playwright helpers. Stacked Arabic lines got room (curriculum header, dashboard counts).
- **B5:** step labels and order covered by a test (not reproduced; decision #33).
- **B6:** the school's subjects are one chip list.
  - Added subjects show as checked chips «مضافة».
  - Removing a chip archives the subject, with «تراجع».
  - Quick add is in the same panel, and the curriculum table refreshes immediately.
- **B7:** `arabicCount` and `format.count()` for every counted noun. All hand-built count strings were replaced, and a test forbids new ones.
- **B8:** Iraqi month names for Gregorian dates (decision #31).
- **Tests:** .NET 129, Vitest 70, Playwright 7.

### Default owner account withdrawn (owner instruction)
- The default account added in `f827c5a` was removed. An empty database shows first-run setup again (create owner, recovery code, wizard).
- `FirstRunTests`: an empty database reports setup required with zero users, and no default credential may appear in the source.
- CLAUDE.md now forbids deletions outside the repository and test temporary folders, and any destructive command without an explicitly confirmed path.

### Phase 2.5D - setup wizard (tag `phase-2-5d`)
- **Setup wizard** at `/setup` (ADR 0023), seven steps: المدرسة، السنة الدراسية، الدوام، الصفوف والشعب، المواد والمنهج، المعلمون، المراجعة.
  - Choice cards, proposed year and terms, working-day and period presets with a live preview, per-day counts.
  - Template and curriculum steps reuse the screens' components.
  - Optional teachers step; review with real counts and warnings.
- **Each server step is one transaction** through the normal services. Nested service transactions now join the step's transaction.
- **Resumable:** opens once after a new account's recovery code; then from the dashboard («استكمال الإعداد») or Settings.
- **Dashboard:** curriculum status per stage and shift.
- **Tests:** .NET 127, Vitest 66, Playwright 5; line coverage Domain 98.7%, Application 95.6%.

### Phase 2.5C - templates, stage cards, curriculum (tag `phase-2-5c`)
- **Curriculum table** (المنهج الدراسي, a new tab under الصفوف والمنهج; ADR 0021):
  - Weekly lessons per subject and stage, edited in place with arrow-key navigation (`EditGrid`).
  - A subject may repeat in a stage with a label («إضافة تكرار لهذه المادة»).
  - Live totals per stage and shift against capacity, marked ناقص / مطابق / زائد with an icon and text.
- **Typing savers** with preview: copy a stage's curriculum to other stages; set the same lessons for one subject across stages (ADR 0022).
- **Stage cards:** a section stepper per stage. New sections get the next label (Arabic letters, numbers or Latin letters); removing the last one is confirmed.
- **Templates** (JSON, marked «مقترحة، يمكن تعديلها»):
  - Iraqi stages by school type, with branches for the preparatory grades.
  - Suggested subject names, with no invented lesson counts.
  - Period presets in the generator, including several breaks.
  - Applying is previewed first, runs in one transaction, and is idempotent.
- **Year copy** also copies the active curriculum lines.
- **New error code:** `CURRICULUM_IN_USE` (a stage or subject is still in the curriculum).
- **Migration:** `Phase25CCurriculumTemplates`.
- **Tests:** .NET 126, Vitest 62, Playwright 4; line coverage Domain 98.7%, Application 95.4%.

### Phase 2.5B - per-day lessons, shift mode, setup progress (tag `phase-2-5b`)
- **Per-day lesson counts** (ADR 0020): each shift teaches the first N lessons on each working day.
  - Capacity, the schedule grid, blocked-period checks and teacher limits use the per-day counts.
  - A stepper row on the periods tab edits them.
  - Grid cells for lessons that do not exist on a day are hatched and disabled.
- **Shift mode** (صباحي فقط، مسائي فقط، مزدوج) on the timing tab, as choice cards.
  - A preview shows what will be created or removed, and which sections block the change (`SHIFT_MODE_IN_USE`).
  - The evening generator proposes the morning end + 30 minutes.
- **Setup progress record** for the resumable wizard (2.5D).
- **New error codes:** `NO_CURRENT_YEAR`, `SHIFT_MODE_IN_USE`.
- **Migration:** `Phase25BDayLessonsShiftModeSetup`.
- **New primitives:** `Stepper`, `ChoiceCards`.
- **Tests:** .NET 115, Vitest 60, Playwright 4.

### Phase 2.5A - foundation fixes (tag `phase-2-5a`, branch `phase-2-5`)
- **Side panel cause fixed:** the CSS reset removed the dialog's `margin: auto`, so dialogs stuck to a screen edge. Dialogs are now centred, and an E2E check covers it.
- **Navigation:**
  - Five sidebar items: لوحة التحكم، المدرسة، الصفوف والمنهج، المعلمون، الإعدادات.
  - Group screens are tabs, and breadcrumbs show the group.
  - Old URLs redirect.
- **Phone navigation:** opens in the page flow; `MobileDrawer` was deleted (ADR 0024).
- **Dates and times:** new `DateField` and `TimeField`: day/month/year and 24-hour HH:mm, the school's numerals, typing in either digit set, arrow keys, ISO values. All 11 native date and time inputs were replaced; native inputs are now forbidden by lint.
- **Bidi:** `LtrText` and `ltrRuns()`. `isolate()` now also isolates numeric ranges, so "2026 - 2027" is no longer shown reversed.
- **Arabic text:**
  - The user menu shows «المالك» with the username as data.
  - "Enter" was removed from the hints.
  - A dictionary test and an E2E check reject Latin text.
- **Add patterns** (DESIGN_SYSTEM.md 14 and 15, `/design`):
  - Subjects and teachers: quick add by name (Enter), details edited in place.
  - Teacher bulk add: a panel inside the page.
  - Calendar: a quick-add row.
  - The subject and teacher dialogs were removed.
- **Server defaults for quick add:** the next free subject colour, priority 3, and a proposed teacher short name.
- **Tests:** .NET 110, Vitest 58, Playwright 4.

### Phase 2F - academic calendar, demo data, final docs (tags `phase-2f`, `phase-2-final`)
- **Academic calendar** (التقويم الدراسي):
  - One day or a date range, a title, a kind and an "affects schedule" flag.
  - A list view and a month view (weeks start on the school's week start day).
  - Entries outside the current year are saved with a warning.
- **Demo data:**
  - `--seed-demo-data <new-db-path> [--dual-shift]` creates a separate database with a fictional school.
  - It refuses an existing file, the default database and the configured database.
  - Every record goes through the Application services, so all rules and audit events apply.
- **New year from the previous one:** the new-year dialog has an optional "copy structure from" choice (shifts, periods, stages and sections; never calendar days), covered by E2E.
- **Messages:** `RECORD_IN_USE` now reads correctly for both delete and archive.
- **ADR 0019** (soft archive and hard delete).
- **Docs:** `docs/OWNER_TEST_SCRIPT_PHASE2.md` (Arabic manual script) and `docs/PHASE2_REPORT.md`.
- **Screenshots:** the comparison tolerance is tightened to 0.0002, and all baselines were regenerated.
- **Tests:** .NET 108, Vitest 50, Playwright 4.

### Phase 2E - teachers (tag `phase-2e`, branch `phase-2`)
- **Teachers** (المعلمون):
  - Full and short names; the short name is unique after Arabic normalization.
  - Off days, chosen with day toggles.
  - Blocked periods on the shared keyboard grid.
  - Full release, with a reason and dates.
  - Maximum lessons per day and per week, validated against the schedule grid; notes.
- **List:** search by full or short name, a "released only" filter, an archive filter and paging. Archive, restore and confirmed delete.
- **Bulk add:** paste names one per line; the preview shows each line's status and proposed short name; only ready lines are saved, in one batch (DECISIONS_PENDING #13).
- **Dashboard:** a teachers count and the step "إضافة المعلمين".
- **Tests:** .NET 104, Vitest 48, Playwright 4 (with teachers screenshots). Line coverage: Domain 98.3%, Application 94.9%.

### Phase 2D - subjects (tag `phase-2d`, branch `phase-2`)
- **Subjects** (المواد):
  - Name, a colour from the ten palette tokens only, and priority 1–5.
  - Flags: distribution enabled, spread across days, heavy, requires double period.
  - Notes, and blocked periods.
  - Search, archive filter and paging; archive and restore; confirmed delete.
- **Blocked periods:**
  - `GET /schedule-grid` exposes working days × the most lessons per day of the current year.
  - The server rejects slots outside it (`BLOCKED_PERIOD_INVALID`, DECISIONS_PENDING #12).
- **New primitives, shown on `/design`:**
  - `Textarea` and `SubjectColorPicker` (a native radio group with a check icon).
  - `BlockedPeriodsGrid`: hatch pattern and Ban icon, one tab stop, arrow keys mirrored for RTL, Space/Enter to toggle.
- **Dashboard:** new counts for subjects and capacity gaps; new checklist step "إضافة المواد".
- **Shared UI:** `RecordActions` and `ArchiveBadge` moved to `components/`; archive and status strings moved to `common`.
- **Tests:**
  - Screenshot baselines regenerated; the tolerance is tightened to 0.002.
  - Totals: .NET 99, Vitest 48, Playwright 4. Line coverage: Domain 98.0%, Application 94.5%.

### Phase 2C - stages and sections, owner-change review, quality gate (tag `phase-2c`, branch `phase-2`)
- **Owner changes kept and corrected** (details in `docs/OWNER_CHANGES_REVIEW.md`):
  - Year-scoped stages and sections with soft archive, as the owner designed them.
  - Validation now lives only in the Domain; duplicate-name races return `DUPLICATE_NAME`.
  - Weekly capacity is computed by `Section.WeeklyCapacity`, with no N+1 queries.
  - Hard delete is allowed for unreferenced stages, sections and shifts.
  - New error code `STAGE_ARCHIVED`.
  - The year copy links sections to the copies of their own stage and shift.
- **Screens rebuilt on the design system:**
  - الدوام والحصص والجرس: working days with a week-start setting, a shifts table with edit and delete, a period editor (add, remove and re-kind rows, errors shown on each row), a generator dialog, and bell settings with a test sound.
  - المراحل والشعب: stage and section tables, add/edit dialogs, archive and restore, confirmed delete, and the computed capacity.
- **Fixes:**
  - Bell tones used `Classic` in the UI while the API sends `classic`, so the select and the test sound failed.
  - Undefined CSS variables; a new test now blocks them.
  - Duplicate React keys made the shifts card render twice.
  - "Saved" messages were lost when components remounted.
- **Data:**
  - The 2C migration moved into `Migrations/`, with its ID unchanged.
  - Display-order indexes are no longer unique (`Phase2CDisplayOrderIndexes`).
- **Tests:** .NET 95, Vitest 46, Playwright 4. Line coverage: Domain 97.7%, Application 94.1%.

### Phase 2B - الدوام والحصص والجرس (tag `phase-2b`, branch `phase-2`)
- Added ISO weekday configuration with Sunday–Thursday defaults, version checks and audit entries.
- Added year-scoped shifts and editable lesson/break periods, validated and copied when a new year copies its structure.
- Added period generation preview and per-period start/end bell flags, plus built-in Web Audio tone previews.
- Added authenticated REST routes, the Arabic schedule setup screen, local EF migration, and domain/API tests.

### Phase 2A - shell, school profile, academic years (tag `phase-2a`, branch `phase-2`)
- **App shell:**
  - A right-hand sidebar (collapsible; a drawer below 768 px) and a top bar showing the school name and the current year and term.
  - A user menu (settings, lock, log out), breadcrumbs and an Arabic 404 page.
  - Routes: dashboard, school profile, academic years, settings.
- **School profile:** name, school and study type, principal and schedule officer, time zone (default Asia/Baghdad), numeral system and calendar display.
  - Logo and stamp upload: PNG, JPEG or WebP up to 2 MB, checked by magic bytes; no SVG.
  - Files are stored under generated names in the app data `assets` folder and served only to the owner, with `nosniff` and a sandbox CSP (ADR 0016).
- **Academic years and terms:**
  - A list with search, sort and paging.
  - Create, edit and make current. Delete is refused while the year is in use, or while it is the current year and other years exist.
  - Terms must fall inside the year without overlapping; one term can be marked current.
  - A partial unique index enforces a single current year.
- **Dashboard:** real counts and a setup checklist computed from stored data, with a link to each step.
- **Formatter:** `createFormatter`/`useFormatter` in `lib/format.ts` follow the school's numeral system, calendar and time zone (ADR 0017). User values inside Arabic sentences are bidi-isolated.
- **Foundations:**
  - An integer `Version` concurrency token on every editable entity; a stale edit returns 409 `CONFLICT` and the UI offers an Arabic reload prompt.
  - Arabic normalization for uniqueness and search.
  - Audit events for every change.
  - The `IDataStore` port, `OperationResult`, list queries (search, sort, page, include archived) and domain-error mapping.
- **Errors:** 25 new codes, each with a status and an Arabic message. The Arabic dictionary is split into `i18n/ar/*` files. Request-size failures now return 413 `PAYLOAD_TOO_LARGE` instead of 500.
- **Tooling:** migration `20261003183255_Phase2ASchoolProfileAndAcademicYears`, generated with the local `dotnet-ef` tool (ADR 0015), and a feature-folder dependency test.
- **Tests:** .NET 79, Vitest 40, Playwright 4. Line coverage: Domain 98.9%, Application 97.4%.
- Decisions taken without the owner are listed in `docs/DECISIONS_PENDING.md`.

### Phase 1.4 - design system adoption and housekeeping (tag `phase-1.4`)
No Phase 2 work.
- **Design system:**
  - Added `DESIGN_SYSTEM.md` and `frontend/src/styles/tokens.css`, exactly as provided by the owner; they are now the only design authority (recorded in CLAUDE.md).
  - Deleted the untracked, generated `design-system/smartschooltimetable/` folder; it recommended Google Fonts and a landing-page pattern.
- **UI font:** Noto Sans Arabic, bundled locally (ADR 0014). The Cairo font from the uncommitted Phase 1.3 polish was removed.
- **Styles:**
  - `styles.css` now imports `tokens.css`, then `styles/base.css`, `components.css`, `layout.css` and `design-guide.css`.
  - No hex colours, font families, raw radii or shadows remain outside `tokens.css`. The primary colour follows the token (#1758A6).
- **Components (DESIGN_SYSTEM.md 6):**
  - New `components/ui` primitives: `Alert`, `Field` (label, "(مطلوب)" marker, hint and error), `Dialog`, `ConfirmDialog`, `DataTable`, `Badge`, `Spinner`, `IconButton`, `Select`, `Checkbox`, `TimetableCell`.
  - `Button` gained sizes sm/md/lg (36/40/48px).
  - Inputs are 40px; touch screens get 44px targets.
  - Forms move focus to the first invalid field.
  - The old `AlertMessage`, `StatusMessage` and `FieldMessages` components were replaced by `Alert` and `Field`.
  - Feature code no longer uses raw form elements; the recovery-code checkbox became `Checkbox`.
- **Numbers:** one formatting helper (`lib/format.ts`, Arabic-Indic digits until the Phase 2 school setting exists). Dictionary strings no longer hard-code digits, and minutes use correct Arabic grammatical number.
- **Style guide:** the `/design` route (development only, excluded from the production bundle) shows:
  - every colour token and type style;
  - every button variant and state, and field states;
  - alerts and badges;
  - a table (hover, selected, empty, loading) and a confirmation dialog;
  - every timetable cell state.
- **Enforcement:**
  - Stylelint is added to `npm run lint` (ADR 0015).
  - New ESLint rules: no arbitrary colour classes or inline style colours, no raw `<button>/<input>/<select>/<textarea>/<table>` in `features/**`, lucide-react icons only.
  - A Vitest contrast test checks every token pair documented in DESIGN_SYSTEM.md.
  - A Playwright design-quality spec runs axe on the setup, recovery code, recovery pending, settings, login and recovery form screens (no serious or critical violations). It also checks login and settings screenshots at 375/768/1024/1440 px with no horizontal scroll.
- **4.1 (added):** the inactivity auto-lock can now be changed in Settings (5, 15, 30, 60 minutes or never).
  - It is saved per owner in `Users.HasCustomInactivityTimeout` and `Users.InactivityTimeoutMinutes` (migration `20261003175642_AddOwnerInactivityTimeoutPreference`, generated by the local `dotnet-ef` tool).
  - It applies immediately without a restart; the appsettings value remains the default.
  - New endpoint `PUT /api/v1/settings/inactivity-timeout`, new code `INVALID_INACTIVITY_TIMEOUT` (422), and the change is audited (`InactivityTimeoutChanged`).
- **4.2:** the test host logs `Microsoft.EntityFrameworkCore.Migrations` and `Database.Command` at Warning.
- **4.3:** the README backup command now refuses to run while the app is running. It copies `timetable.db` with any `-wal`/`-shm` sidecars. Both cases were verified against a real running and a stopped app.
- **Housekeeping:**
  - ADR 0013 accepted.
  - Frontend version is 1.4.0.
  - Test host and helpers moved to `tests/.../TestSupport.cs`.
  - New test `EfModelHasNoPendingChangesVersusTheLatestMigration`.
- **Tests:**
  - .NET: 45 (was 41).
  - Vitest: 32 (was 7; 25 are contrast checks).
  - Playwright: 3 (was 2).
- **DB changes:** the two `Users` columns above.
- **API changes:** the endpoint above; `GET /bootstrap` now returns the effective `inactivityTimeoutMinutes` and `inactivityTimeoutChoices`.
- **Also in this release** (the Phase 1.3 UI polish done on owner feedback, never tagged separately):
  - **Fixed:** password fields had no visible box and the eye button floated outside them. `Input` merged its `ui-input` class before spreading props, so the `className` passed by `PasswordField` replaced it. The eye control now sits inside the field at its logical end (left in RTL).
  - **Changed:** redesigned the stylesheet (later moved onto the owner's tokens; the Cairo font was replaced by Noto Sans Arabic).
    - Clear hover, focus, pressed, disabled and loading states (buttons show a spinner while a request runs).
    - Reduced-motion support.
  - **Changed:** validation errors appear under each field (`aria-invalid` and `aria-describedby`) and clear as soon as the field is edited. A summary alert remains for server validation failures.
  - **Changed:** Settings is split into three titled cards (Session, Recovery code, Password), each with a description and a distinct action label. This removes the repeated "إنشاء رمز استرداد جديد" heading/button text.
  - **Changed:** copy and save confirmations are shown as success messages instead of in the error box; the copy button briefly shows "تم النسخ".
    - The recovery code is shown as a 2×2 grid of groups.
    - After a password change, the login screen shows a confirmation.
    - The header has active navigation states and a user chip.
    - The home page has a greeting and a quick-action card (no placeholder statistics).
  - **Tests:** Playwright adapted to the new UI. Empty login fields are now validated in the browser; the real-server 422 check uses the recovery form's password length. Counts are unchanged: .NET 41, Vitest 7, Playwright 2.

### Phase 1.3 - audit fixes (tag `phase-1.3`)
Fixes only; no Phase 2 work. Source: `docs/AUDIT_REPORT.md` §11.
- **Fixed (D1):** an error code that the error middleware did not recognise was returned with HTTP 200. `Response.Clear()` had reset the status before the fallback read it. The middleware now captures the status first. Unregistered codes keep the original error status, or become 500 `INTERNAL_ERROR` when that status is below 400. Change-password with an expired or bogus session now returns 401 `UNAUTHENTICATED` (previously `200 {"code":"unauthenticated"}`), and the UI no longer reports "password changed" in that case. The client also treats any 2xx body containing `code` as an error.
- **Changed:** all error codes are constants in `Application/ErrorCodes.cs`, with statuses in `Api/ApiErrorCodes.StatusByCode`. Endpoints take the status from that table. Removed the unreachable `INVALID_SETUP`, `RECOVERY_CODE_REGENERATION_FAILED` and `PASSWORD_CHANGE_FAILED` fallbacks and the unused `CLIPBOARD_FAILED` UI code.
- **Fixed (D2):** a reload before acknowledging the recovery code no longer opens the application. A blocking screen requires generating a new code with the current password (or logging out). SECURITY.md, ADR 0009 and DELIVERY_PLAN were updated, and the Playwright test now asserts the blocking behaviour.
- **Changed:** the minimum password length is 8 (was 12) in the validators, service, UI and docs.
- **Fixed:** after a completed recovery, logging out showed the recovery form instead of the login screen. The recovery-form flag now resets.
- **Changed (frontend):** `App.tsx` was split into `features/auth`, `features/home`, `features/settings`, `layout/` and `lib/`, one component per file.
  - Removed the `RecoveryGate` pass-through and the nested `role="alert"`.
  - Password-toggle labels and the download filename moved to the dictionary.
  - `Object.hasOwn` is used for code lookup.
  - Bootstrap/session server state now lives only in TanStack Query; Zustand holds only the in-memory recovery code and a UI flag.
- **Changed (backend):**
  - Deduplicated security headers (`LocalSecurityHeaders`), cookie options (`SessionCookie`), token generation (`SecureToken`) and password/username rules (`CredentialRules` plus FluentValidation rule extensions).
  - Removed dead `context.Items` writes and the template `SmartSchoolTimetable.Api.http`.
  - Split the request/response records and validators into their own files.
- **Changed (build):** `Directory.Build.props` enables nullable, `TreatWarningsAsErrors`, .NET analyzers (`AnalysisLevel=latest-recommended`) and `EnforceCodeStyleInBuild`, and `.editorconfig` was added. The resulting CA1848 findings were fixed with source-generated `LoggerMessage` methods (`LocalLog`), and CA1305 in tests with a fixed `DateTimeOffset`. `spikes/` is excluded.
- **Fixed (D3):** `PRAGMA synchronous=FULL` is now applied on every EF Core connection (`SqlitePragmaInterceptor`); previously only the startup connection had it.
- **Fixed (D4):** the fixed one-second failed-login delay now runs after the operation lock is released.
- **Changed:** `--reset-local-database` console prompts are Arabic, and the console uses UTF-8.
- **Docs:**
  - ADR 0013 records not adopting MediatR/CQRS and deferring Serilog, health endpoints, CI and a generated OpenAPI client.
  - ARCHITECTURE.md now describes direct application services.
  - API.md has an endpoint table and the real status codes (422, not 400; no 423).
  - DATABASE.md describes the real columns and pragmas.
  - SECURITY.md says the iteration count is stored and the algorithm is fixed in code (no hash-version column was added).
  - README has interim backup instructions.
  - `docs/AUDIT_REPORT.md` §11 shows the DONE/NOT DONE status of each item.
- **Tests:**
  - .NET: 41 (was 22). New: ErrorContractTests, ArchitectureTests, LocalAuthServiceTests, change-password 401, 8-character minimum, per-connection pragma, Arabic reset prompts.
  - Vitest: 7 (was 4).
  - Playwright: 2, now against real server responses except the 500 path.
- **DB changes:** none (no migration).
- **API changes:** change-password with an invalid session now returns 401 instead of 200, and the minimum password length is 8.
- **Breaking changes:** none for stored data.

### Phase 1.2 - React owner UI and Arabic error contract (tag `phase-1.2`)
- Replaced the static owner pages with a React 19 / strict TypeScript / Vite / Tailwind UI served from the API `wwwroot` (ADR 0012). It uses TanStack Query, Zustand, React Router and lucide-react.
- Added the recovery-code confirmation screen (copy/print/save, acknowledgement gate), Settings with current-password regeneration and optional password change, client inactivity auto-lock, and accessible password visibility toggles.
- Added the unified API error envelope `{ code, correlationId, errors }` for framework, validation, security and unhandled failures; FluentValidation field/code pairs; and an Arabic dictionary covering every API code.
- Added the ESLint localization/icon rules, Vitest tests and two Playwright end-to-end tests.
- DB: migration `20261003152200_RecoveryCodeAcknowledgement` adds `Users.RecoveryCodeAcknowledged` (existing rows default to acknowledged).

### Phase 1.1 - authentication hardening (tag `phase-1.1`)
- Specified the credential parameters (PBKDF2-HMAC-SHA-256, 600,000 iterations, 16-byte salt, 32-byte key) and the recovery code (128-bit, 4×8 hex, salted SHA-256).
- EF Core command logging is `Warning` by default and `Information` only in Development; sensitive-data logging is explicitly off.
- Added tests that passwords and recovery codes never reach logs, and that the session cookie has `HttpOnly`, `SameSite=Strict` and `Path=/` without `Secure` on loopback HTTP.
- Added an accessible password visibility toggle to the login page.
- DB: migration `20261003160000_RemoveEscalatingLoginLockoutFields` drops the escalating-lockout columns; failed logins use only a fixed one-second delay.

### Phase 1 - local owner foundation (tag `phase-1`)
- Added the ASP.NET Core 9 local host with the Domain/Application/Infrastructure/Api layering, bound only to `127.0.0.1` with startup and live-binding checks.
- Added first-run owner setup, login, logout, recovery-code reset and regeneration, password change, and the configurable inactivity timeout (1–1440 minutes or `Never`) with an in-memory server session store.
- Added localhost protections: exact Host/Origin validation, SameSite=Strict HttpOnly cookie, no CORS, a per-launch token on state-changing requests, and security headers.
- Added EF Core SQLite persistence in WAL mode (migration `20261003105756_InitialLocalSchema`: `Users`, `AuditHistory`), local audit events, and the confirmed `--reset-local-database` command.
- Added xUnit integration tests for the Phase 1 acceptance criteria.

### Phase 0.4 - storage, acceptance, and risk criteria
- Added the SQLite WAL/safe-backup/provider-abstraction ADR and detailed its backup integrity requirements.
- Reworked the delivery plan into eight implementation phases with acceptance criteria for every phase.
- Recorded the 40-section / 54-teacher 30-second no-solution risk and required measured investigation of two-stage solving, decomposition, and solver hints.
- Added a multi-constraint infeasibility-diagnostic acceptance case and a QuestPDF licensing gate before Phase 6.
- Clarified that .NET 9 and Node are the only required build toolchains; Docker, PostgreSQL, and Redis are not required. .NET 9 is installed but currently not on PATH.
- Documentation only; Phase 1 had not started at this point.

### Phase 0.3 - owner scope clarification
- Replaced the multi-tenant/multi-user deployment assumptions with a single-user, local-only application architecture.
- Documented one-time owner setup/recovery, password/session/lockout requirements, loopback and localhost-attack protections, and local audit history.
- Updated the delivery and test criteria for Phases 1 and 8.
- Added ADRs for local deployment, owner authentication, and the SQLCipher final-phase go/no-go; marked tenant sync and distributed worker ADRs superseded.
- No application code was added; Phase 1 remains gated on explicit owner approval.

### Phase 0
- Added architecture, database, domain, API, sync, security, testing, observability, and delivery-plan documentation.
- Added ADRs for tenancy, C# CP-SAT, offline sync/printing, PostgreSQL worker locking, deterministic solving, and Arabic PDF rendering.
- Ran the .NET 9 / Google.OrTools C# spike with hard constraints, weighted soft preferences, shared-resource capacity, single/multi-worker timings, and assumption-core infeasibility diagnostics.
- Ran the QuestPDF/Noto Naskh Arabic RTL spike, generated a PDF, and visually inspected its PNG render.
- Recorded the 40-section / 20-teacher workload-cap infeasibility and local environment prerequisites.
