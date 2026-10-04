# PHASE 2.5 - SIMPLER SETUP (owner's specification, as received 2026-10-04)

Phase 2 (school master data) is built. Owner testing showed that the data entry is too heavy: the owner types what should be CHOSEN, adds records through a narrow side panel, and some Arabic/RTL details are wrong. Phase 2.5 fixes this BEFORE Phase 3 (workload and solver), because Phase 3 will multiply the amount of data entry. Follow the AUTONOMY PROTOCOL strictly. Do not start Phase 3.

## 0. Preconditions and autonomy protocol
- 0.1 Preconditions: tag `phase-2-final` exists (or Phase 2 is merged into master) and the full test suite is green; otherwise STOP and report.
- 0.2 Branch `phase-2-5` from the Phase 2 result. Never commit to `master`.
- 0.3 Five checkpoints (section 8). After EACH: `dotnet build -c Release --no-incremental`, `dotnet test -c Release`, `npm run lint`, `npm test` (and Playwright) green; docs + CHANGELOG updated; one commit; tag `phase-2-5a` ... `phase-2-5e`; update `docs/PHASE25_PROGRESS.md` (branch, last green tag, in progress, next, known problems). Never start the next checkpoint on a red build.
- 0.4 Token discipline: short tool output, no full logs or files, commit small and often. If running low, stop at a clean green commit and update the progress file.
- 0.5 Ambiguity: do NOT wait. Choose the safest option, log it in `docs/DECISIONS_PENDING.md` (date, decision, alternatives, reason, how to change) and continue.
- 0.6 Architectural changes (new framework, database, auth/storage model): "Proposed" ADR and skip. New runtime dependencies only if necessary, each with an ADR entry.
- 0.7 Never run anything against the owner's real database in `%LOCALAPPDATA%`; tests and demos use temporary paths. Do not touch `.claude/`, `.kilo/`, `temp_check/`. Never recreate `design-system/`.
- 0.8 No fake functionality: a feature works end to end (backend, UI, validation, tests) or is listed as not done.
- 0.9 A reference program inspired this phase. Take ONLY ideas (presets, quick actions, simple flows). Do NOT copy its texts, layout, icons, colours or name. DESIGN_SYSTEM.md remains the only design authority.

## 1. What the owner decided
1. School type is CHOSEN at the start, with the school name and shift mode. Iraqi structure:
   - ابتدائية: 6 grades (الأول الابتدائي … السادس الابتدائي)
   - متوسطة: 3 grades (الأول المتوسط … الثالث المتوسط)
   - إعدادية: 3 grades (الرابع الإعدادي، الخامس الإعدادي، السادس الإعدادي) with branches (علمي، أدبي) chosen per grade; a stage is grade+branch such as "الرابع العلمي" or "السادس الأدبي"
   - ثانوية: متوسطة + إعدادية together (6 grades)
   Each grade has a number of sections (labels أ، ب، ج، د، هـ، و، …).
2. Shift mode chosen at the start: صباحي فقط، مسائي فقط، مزدوج (morning + evening).
3. Records are added by choosing and by quick add, not by long forms. NO side drawers anywhere.
4. A CURRICULUM TABLE defines, per stage (grade), the subjects taught and lessons per week. The SAME subject may appear MORE THAN ONCE in the same stage (e.g. "قواعد" and "أدب"); (stage, subject) is NOT unique.
5. Number of lessons may differ between days (e.g. Sunday 7, Thursday 6).

## 2. Fixes for defects seen in the screenshots (2.5A)
- 2.1 RTL bug: the academic year label renders reversed ("2027-2026"). Every Latin/numeric range or code (years, times, dates, numbers with dashes or slashes, file names) renders in correct logical order with `dir="ltr"` + `unicode-bidi: isolate` (or `<bdi>`). Shared `LtrText` component used everywhere; a test fails if the year is displayed reversed.
- 2.2 Dates/times show browser-native English formats. Replace every native `<input type="date|time">` with design-system `DateField` and `TimeField`: Arabic labels, 24-hour time (HH:mm), day/month/year order, numerals following the school setting, typing and keyboard picking, clear Arabic errors, ISO values to the API. Show both on `/design`. Lint rule forbidding native date/time inputs in `features/**`.
- 2.3 English labels (e.g. "Admin" in the user menu): all fixed UI text Arabic. Show the username as data and the fixed label "المالك" where a role label is needed. Re-scan the UI for Latin letters in visible text (excluding usernames, codes, file names).
- 2.4 Replace all side panels/drawers with the add patterns and delete the drawer/sheet component. Lint rule or test fails if a drawer/sheet is introduced.
- 2.5 ADD PATTERNS (new DESIGN_SYSTEM.md section "Add and edit patterns", enforced):
  - 1–2 fields → INLINE ROW in the list (type, Enter): section count, quick add subject, quick add teacher, curriculum cells, calendar quick add title.
  - 3–6 fields → CENTERED DIALOG (32–40rem, focus trap, Esc closes, no inner scrolling at 1280×720): academic year, term, generate periods, calendar day, shift.
  - Long or optional detail → QUICK ADD BY NAME ONLY, details edited IN PLACE on an expandable row/card (teacher constraints, subject advanced options). Never a long modal form.
  - Bulk → a dedicated panel inside the page with preview.
  - Never anchor a dialog to a screen edge. `/design` shows each pattern.
- 2.6 Navigation: 5 sidebar items with tabs: لوحة التحكم / المدرسة (بيانات المدرسة، السنة والفصول، الدوام والحصص والجرس، التقويم الدراسي) / الصفوف والمنهج (المراحل والشعب، المواد، المنهج الدراسي) / المعلمون / الإعدادات. Old URLs redirect. Breadcrumbs, tests, screenshots updated.

## 3. Model changes (2.5B)
- 3.1 PER-DAY LESSON COUNT per shift and working day (default = the shift's lesson count; the first N lessons are used). Section weekly capacity = sum over working days of that day's count (breaks excluded). Update capacity everywhere, blocked-period validation (lesson must exist that day), teacher limits (max per day ≤ most lessons of any day; max per week ≤ total capacity of the largest shift), blocked grids (cells for missing lessons disabled and hatched), and a per-day stepper row on the periods tab. Migration + tests; existing data keeps working.
- 3.2 SHIFT MODE = the school study type (صباحي، مسائي، مزدوج). Choosing it creates the needed shifts for the current year. Changing later allowed only when no active section would be left without a shift (Arabic explanation + affected sections). Evening default first lesson = morning end + 30 minutes (editable).
- 3.3 CURRICULUM ENTRY: stage, subject, weekly lessons (1–15), optional label, optional "needs double period" (default from subject), notes, soft archive, `Version`, audit. NO uniqueness on (stage, subject). Live totals per stage vs weekly capacity of its sections (per shift when both). Status colour + icon + text: under (warning, missing count), equal (success), over (error, excess). Deleting/archiving a subject or stage with curriculum entries blocked or requires archiving the entries (Arabic message). Phase 3 turns each entry into one workload line per section (document in DOMAIN.md, DELIVERY_PLAN.md).
- 3.4 SECTION LABELS: generator (أ، ب، ج، د، هـ، و، ز، ح، ط، ي … then أ1 …), options numbers (1,2,3) and Latin (A,B,C). Stepper per stage; removing only the LAST section when unreferenced; otherwise Arabic error explains why.
- 3.5 SETUP PROGRESS: single-row record with completed wizard steps and choices (school type, shift mode); resumable; `Version`, audit.
All new/changed endpoints authenticated, Origin/token protected, stable codes with Arabic text, DTOs, pagination for lists, API.md updated. Migrations via local `dotnet-ef`; "no pending model changes" test stays green.

## 4. Templates (2.5C)
- 4.1 Data-driven JSON templates (resources, validated by tests): stage templates per school type (branches علمي، أدبي editable); starter subject NAME lists per type and stage (NAMES ONLY, no invented official weekly counts; UI marks "مقترحة، يمكن تعديلها"); period presets (lesson length, first start, lesson count, break patterns e.g. "استراحة واحدة بعد الحصة الثالثة", "فرصة قصيرة بعد كل حصتين"); working-day presets (الأحد-الخميس default, السبت-الخميس, custom).
- 4.2 Curriculum helpers: copy a stage's curriculum to another stage; same weekly lessons for a subject across selected stages; "apply template" with a PREVIEW (created / already exists, skipped / not applicable). IDEMPOTENT, never deletes or overwrites.
- 4.3 Subject colours auto-assigned from the 10 tokens (next unused, then cycle), changeable on the row. Default priority 3; advanced flags collapsed under "خيارات متقدمة".

## 5. Setup wizard (2.5D)
After the owner account and recovery code, before the dashboard. Linear steps, progress bar, back/next, autosave per step, resumable, skippable where sensible, reachable from the dashboard checklist ("استكمال الإعداد") and Settings. Choices by selection controls (cards, toggles, steppers), typing only names.
1. المدرسة: name, type (4 cards), shift mode (3 cards), principal optional.
2. السنة الدراسية: year proposed from today (selectable list), two terms with editable proposed dates (shown as suggestions).
3. الدوام: working-day preset or custom; lessons per day, lesson length, first start, break pattern presets, per-day counts (collapsed "تعديل عدد الحصص لكل يوم"); dual: a second block for the evening shift. Live preview table of periods.
4. الصفوف والشعب: grades from the template as checkable cards (branches per grade for إعدادية/ثانوية), section stepper per grade, label style, dual: per-grade (or per-section) shift selector with "كل شعب هذا الصف في الدوام …". Preview.
5. المواد والمنهج: suggested subjects checklist (+ quick add), then the curriculum table (rows subjects, columns stages, cells weekly lessons; keyboard navigable), "إضافة تكرار لهذه المادة"; live totals vs capacity with status. Cells may stay empty.
6. المعلمون (optional): paste names with preview; skip allowed.
7. المراجعة: counts, warnings (stages under capacity, empty curriculum), "إنهاء" → dashboard.
Wizard actions use the same services and validations (no shortcuts), one transaction per step, audit entries, Arabic errors next to the step.

## 6. Screen changes outside the wizard
- المراحل والشعب: per stage a card with a sections stepper, section chips with shift badge (dual), capacity per day/week.
- المواد: list with quick-add row (name + Enter), auto colour, expandable advanced options.
- المنهج الدراسي: curriculum table (same component as step 5) with totals, repeats, copy actions.
- المعلمون: quick-add row (name + Enter, short name proposed), expandable row/card for constraints; keep bulk add.
- التقويم: inline quick add (title + date), details by expanding.
- Dashboard: real counts and checklist, curriculum totals status per stage, "استكمال الإعداد" button.

## 7. Tests and quality
- Unit: template JSON validation, label generator, stage/section generation per type and branch, per-day capacity, curriculum totals (under/equal/over, per shift), repeated entries allowed, wizard idempotency, shift mode change rules, blocked periods vs per-day lessons, teacher limits, archive/delete protection with curriculum entries.
- API integration: every new endpoint (unauthenticated, validation codes, `Version` conflict, pagination), template preview/apply, wizard step persistence and resume.
- Vitest: DateField/TimeField (24-hour, numerals, keyboard), LtrText (year order), curriculum table (keyboard, totals), wizard step logic.
- Playwright on a temp DB: (a) fresh start → owner → recovery code → wizard for a morning-only intermediate school with 3 grades; check stages, sections, periods, curriculum totals; (b) dual-shift secondary (ثانوية) with branches; both shifts, per-section shift, per-day counts, capacities; (c) template re-run idempotent; (d) year label order; (e) no drawers; axe on every new/changed screen and dialog; screenshots at 375/768/1024/1440 for wizard steps and curriculum.
- Consistency: error codes (status + Arabic), no raw error strings, no pending EF changes, architecture, contrast, lint (incl. no native date/time inputs, no drawers).
- Coverage: Domain and Application ≥ 90% lines.
- Quality: files ≤ ~300 lines, components ≤ ~200, no `any`, no TODO/placeholder, warnings as errors, `AsNoTracking` on read queries added in this phase.

## 8. Checkpoints
- 2.5A Foundation fixes: LtrText, DateField/TimeField, Arabic labels, drawers removed, add patterns (DESIGN_SYSTEM.md + `/design`), navigation regrouped, inline quick-add for subjects/teachers/calendar. Tag `phase-2-5a`.
- 2.5B Model: per-day lesson counts, capacity/validation updates, shift mode rules, setup progress record, migrations and tests. Tag `phase-2-5b`.
- 2.5C Templates and curriculum: JSON templates, stage/section generator and stepper, curriculum entries (repeats allowed) with totals, copy helpers, curriculum screen, subject auto colours. Tag `phase-2-5c`.
- 2.5D Wizard: all 7 steps, resume, idempotent apply, dashboard integration. Tag `phase-2-5d`.
- 2.5E Hardening and delivery: E2E scenarios, axe and screenshots, demo data updated (Iraqi school, morning-only and dual-shift variants, curriculum with SAMPLE numbers clearly marked as demo), docs, Arabic owner test script, final report. Tag `phase-2-5e`, then `phase-2-5-final`.

## 9. Documentation and deliverables
Update DOMAIN.md, DATABASE.md, API.md, DESIGN_SYSTEM.md (add/edit patterns, DateField/TimeField, LtrText), DELIVERY_PLAN.md (insert Phase 2.5; Phase 3 becomes: assign teachers to curriculum entries per section, resources, scheduling profiles, pre-solve validation, using the curriculum table as the source of weekly workload), TESTING.md, README.md, CHANGELOG.md (one entry per checkpoint), SECURITY.md if relevant. ADRs: per-day lesson counts, curriculum repetition, idempotent templates, wizard design, removal of drawers. `docs/OWNER_TEST_SCRIPT_PHASE25.md` in ARABIC (~25 steps, each «المتوقع»). `docs/DECISIONS_PENDING.md`, `docs/PHASE25_PROGRESS.md`.

## 10. Final report (`docs/PHASE25_REPORT.md` + short printed summary)
Per checkpoint tag/commit/what; exact results (build warnings, dotnet test, eslint, stylelint, vitest, playwright + axe, coverage); decisions to confirm; not done/partial with reasons, risks; exact commands (run, demo variants, tests, `/design`); unrelated files/folders. Only facts backed by command output. Do not start Phase 3.
