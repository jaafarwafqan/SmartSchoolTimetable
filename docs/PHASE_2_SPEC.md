# PHASE 2 - CORE SCHOOL DATA (paste everything below into the coding agent)

You are working in the SmartSchoolTimetable repository. Phase 2 implements the school's master data: school profile, academic year/terms, shifts, lesson periods, bell settings, stages, sections, subjects, teachers (with constraints) and the academic calendar. Nothing about timetable generation is built here.

The owner will be AWAY while you work and will review afterwards. Therefore follow the AUTONOMY PROTOCOL strictly.

------------------------------------------------------------
0. PRECONDITIONS AND AUTONOMY PROTOCOL
------------------------------------------------------------
0.1 Precondition: tag `phase-1.4` exists and the full test suite is green. If not, STOP and report. Do not start Phase 2 on a red baseline.
0.2 Create and work on a Git branch named `phase-2`. Never commit to `master`. The owner merges after acceptance.
0.3 Work in six checkpoints (2A to 2F, section 4). After EACH checkpoint: all tests green (`dotnet build -c Release --no-incremental`, `dotnet test -c Release`, `npm run lint`, `npm test`), docs and CHANGELOG updated, one commit, tag `phase-2a` ... `phase-2f`. Do not start the next checkpoint on a red build.
0.4 If a checkpoint cannot be made green after serious attempts: STOP, commit nothing broken, and write the problem in your final report.
0.5 Ambiguity: do NOT wait for an answer. Choose the safest option that does not change the approved architecture, and append an entry to `docs/DECISIONS_PENDING.md` (date, decision, alternatives, reason, how to change it) then continue.
0.6 Architectural changes (new framework, new database, new runtime dependency of significance, changing auth/storage model): do NOT do them. Write an ADR proposal marked "Proposed", skip that item, continue with the rest.
0.7 New dependencies: allowed only if necessary; each needs a short entry in an ADR (purpose, license, maintenance, alternatives). Prefer none.
0.8 Do not touch `.claude/`, `.kilo/`, `temp_check/`. Do not delete user data. Never run anything against the owner's real database in `%LOCALAPPDATA%`; tests and demo data use temporary paths only.
0.9 No fake functionality: no placeholder screens, no fake statistics, no buttons that do nothing. An unfinished item is listed in the final report, not hidden.

------------------------------------------------------------
1. CONSTRAINTS THAT STILL APPLY (from CLAUDE.md, DESIGN_SYSTEM.md, ADRs)
------------------------------------------------------------
- Single owner, single school, local app, SQLite (WAL), Kestrel on 127.0.0.1 only. No multi-tenancy, no roles, no MediatR (endpoints call Application services directly; endpoints stay thin; no DbContext or Domain entities in endpoints; architecture tests must stay green and be extended).
- Every endpoint (except bootstrap/auth) requires the authenticated session. State-changing requests keep the Origin check and per-launch token.
- API returns stable error codes only. Every new code must be: a constant in the codes file, mapped to an HTTP status, present in the Arabic dictionary, and covered by the existing consistency tests. All user-facing text is Arabic.
- Design: use only tokens and components from DESIGN_SYSTEM.md. Icon + Arabic label on actions, lucide-react only, RTL logical properties, WCAG AA, keyboard operable. Reuse `components/ui`; add primitives to the design system first if missing, and show them on `/design`.
- Frontend: React + TypeScript strict, no `any`, feature folders (`features/<name>`), one component per file, TanStack Query for server state, Zustand only for UI state. No component over ~200 lines; no file over ~300 lines.
- Backend: warnings are errors; feature folders in Application (`SchoolSetup`, `Calendar`, `Teachers`, `Subjects`, `Stages`), endpoint files per feature under 300 lines; business rules live in Domain/Application, never in endpoints.
- Migrations: use `dotnet-ef` as a LOCAL tool (tool manifest `.config/dotnet-tools.json`, document it in an ADR) so migrations are generated with their Designer files. Add a test that fails if the EF model has pending changes versus the latest migration.
- Concurrency: every editable entity has an integer `Version` concurrency token, incremented on save. A stale update returns 409 `CONFLICT` and the UI shows an Arabic message offering to reload the record (two browser tabs can edit the same data).
- Soft archive instead of delete for teachers, subjects, stages, sections (history is needed by later phases). Hard delete only for records with no references, and only after a confirmation dialog.
- Audit: extend the local history to log create/update/archive/delete of these entities (event type, target, short Arabic or neutral summary, no secrets). No before/after JSON.
- Arabic text normalization helper (trim, collapse spaces, remove tatweel and diacritics, unify alef forms and ya/alef-maqsura for comparison) used for uniqueness checks and search. Stored text keeps the user's original spelling.

------------------------------------------------------------
2. DOMAIN SPECIFICATION
------------------------------------------------------------
Iraqi school conventions apply; everything is configurable, defaults are only suggestions. Default timezone `Asia/Baghdad`, default working days Sunday-Thursday.

2.1 SchoolProfile (exactly one row): school name, school type (ابتدائية، متوسطة، إعدادية، ثانوية، أخرى), study type (صباحي، مسائي، مزدوج), principal name, schedule officer name, logo, stamp, timezone, numerals preference (Arabic-Indic or Western), calendar preference (Gregorian or Hijri display). Logo/stamp: PNG/JPEG/WebP only, max 2 MB, validated by magic bytes (SVG NOT allowed), stored as files under the app data folder (not in the repo), served only through an authenticated endpoint with `nosniff`.
2.2 AcademicYear and Term: year label (e.g. 2026-2027), start/end dates, terms (name, start/end), one CURRENT year and one CURRENT term at a time. Terms must not overlap and must lie inside the year. A new year can be created by copying the structure of the previous one (shifts, periods, stages, sections), never the calendar days.
2.3 WorkingDays: set of weekdays (1-7), week start day setting.
2.4 Shift: name (صباحي/مسائي or custom), order. Each shift owns LessonPeriods. A school with one shift has one; dual-shift schools have two.
2.5 LessonPeriod: shift, number (1..12), start time, end time, kind (lesson or break). Rules: ascending, no overlaps, end after start, within one day, at least one lesson per shift. Helper "Generate periods": first start time, lesson length, break length, break after lesson N, number of lessons -> creates an editable list (the owner can still edit each row).
2.6 BellSetting: per period start/end bell enabled flags, break bell, and a tone choice from built-in tones generated with the Web Audio API (no audio files, no licensing issues). Include a "test sound" button. Actual live ringing/status is Phase 7; here only configuration plus preview.
2.7 Stage: name, display order (e.g. الأول المتوسط، الرابع العلمي، السادس الأدبي), active flag.
2.8 Section: stage, section label (A-F or Arabic letters, free text), shift, optional student count, active flag. Computed WEEKLY CAPACITY = number of working days x number of lesson periods in its shift (breaks excluded); shown in lists and used later by Phase 3 validation. Unique per (stage, label).
2.9 Subject: name, colour (ONLY subject-1..subject-10 tokens), priority 1-5 (5 highest), distribution enabled flag, flags: spreadAcrossDays, heavy, requiresDoublePeriod, blocked periods (set of day + period number), notes. A required-resource link is NOT built now (Phase 3).
2.10 Teacher: full name, short name (unique after normalization), active flag, off days (set of weekdays), blocked periods (day + period), full release (flag + optional reason + optional date range), max lessons per day, max lessons per week, notes. Validation: blocked periods must reference existing days/periods; max per day <= periods per day; max per week <= working days x periods per day.
2.11 CalendarDay: date or date range, title, kind (عطلة رسمية، عطلة مدرسية، امتحان، يوم خاص), affects schedule flag. Must lie inside the current year (warn, do not block, outside).
2.12 Settings: inactivity auto-lock (already present, keep).

------------------------------------------------------------
3. API AND FRONTEND SCOPE
------------------------------------------------------------
Backend: REST under `/api/v1` for: school-profile (+ logo/stamp upload/download/remove), academic-years, terms, working-days, shifts, periods (+ generate), bell-settings, stages, sections, subjects, teachers (+ bulk create from pasted list), calendar-days, dashboard-summary. Lists support search (normalized Arabic), sort, pagination and an "include archived" filter. Responses use DTOs, never entities. Add the endpoint table to API.md.

Frontend (RTL, design system): app shell with right-hand sidebar (collapsible, drawer under 768px), top bar with school name + current year/term + user menu (settings, lock, logout), breadcrumbs, and these screens:
1. لوحة التحكم: real counts from the database only (teachers, subjects, stages, sections, capacity gaps) plus a computed "setup checklist" (school profile filled, year/term set, shifts and periods defined, stages and sections added, subjects added, teachers added). Each unfinished step links to its screen. Empty states with a clear primary action.
2. بيانات المدرسة (profile, logo, stamp, timezone, numerals, calendar display).
3. السنة الدراسية والفصول.
4. الدوام والحصص والجرس (working days, shifts, periods table with generate helper, bell settings with test sound).
5. المراحل والشعب (stage list, sections per stage, computed weekly capacity).
6. المواد (list, colour picker limited to the 10 tokens, priority, flags, blocked-periods grid).
7. المعلمون (list with search/filter/archive, add/edit dialog, constraints area with off-days toggles and a day x period blocked-periods grid operable by keyboard, full release, max lessons, bulk add from pasted names one per line with a preview before saving).
8. التقويم الدراسي (list + month view of calendar days).
Numbers and dates go through ONE formatting helper that respects the numerals/calendar settings and timezone.
All forms: inline Arabic validation, focus the first invalid field, loading and disabled states, no native validation tooltips, no alert/confirm.

------------------------------------------------------------
4. CHECKPOINTS
------------------------------------------------------------
2A - Foundation: app shell and navigation, SchoolProfile + assets, academic year/terms, settings (timezone, numerals, calendar), formatting helper, Arabic normalization helper, concurrency token pattern, extended audit, dashboard with real counts and checklist, `dotnet-ef` local tool + pending-model-changes test. Tag `phase-2a`.
2B - Shifts, working days, lesson periods (with generate helper), bell settings with Web Audio tones. Tag `phase-2b`.
2C - Stages and sections with computed weekly capacity and archive rules. Tag `phase-2c`.
2D - Subjects with palette colours, priority, flags and blocked-periods grid. Tag `phase-2d`.
2E - Teachers with constraints grid, full release, limits, bulk add, archive. Tag `phase-2e`.
2F - Academic calendar; demo data command; hardening; documentation; full E2E; owner test script. Tag `phase-2f`, then final tag `phase-2`.

Demo data command (2F): `--seed-demo-data <path-to-new-db-file>` creates a SEPARATE database file at the given path (refuse if the file exists, refuse the default real path) with a realistic Arabic sample school: 20 teachers with varied constraints, 12 sections across 3-4 stages, 10 subjects with all flags exercised, 7 periods plus breaks, one shift (and a dual-shift variant flag), a current year/terms and 8 calendar days. Teachers' weekly workload is NOT created (Phase 3). Document how to run the app against it via configuration `Database:Path`.

------------------------------------------------------------
5. TESTS (all required)
------------------------------------------------------------
- Domain/Application unit tests for every rule in section 2 (overlaps, ordering, capacity computation, normalization uniqueness, term/year ranges, constraint validation, archive rules, concurrency). Coverage target: Domain and Application >= 90% lines.
- API integration tests per endpoint: unauthenticated rejected, validation errors with field codes, success, conflict (stale Version), archive/delete rules, pagination/search, upload validation (wrong type, oversize, SVG rejected, magic-byte mismatch), unified error contract.
- Consistency tests extended: every new error code has an HTTP status and an Arabic message; no raw error strings in src/; no pending EF model changes.
- Architecture tests extended: endpoints do not reference DbContext/entities; feature folders respect dependency direction.
- Frontend: Vitest for forms/validation/formatting helper/grids (keyboard operation); ESLint + Stylelint + contrast test remain green.
- Playwright E2E against the real server and a temp database: complete the setup checklist end to end (profile -> year/term -> shift and generated periods -> stages and sections -> subjects with blocked grid -> teachers incl. bulk add -> calendar day), check the dashboard counts and checklist update, a stale-edit conflict between two pages, and an axe accessibility scan on every new screen. Screenshots at 375/768/1024/1440 px for dashboard, periods, teachers.
- Security: new endpoints cannot be called without the session cookie, without Origin, or without the launch token; logo download is authenticated; uploaded files never execute or render as HTML.

------------------------------------------------------------
6. DOCUMENTATION AND DELIVERABLES
------------------------------------------------------------
Update API.md (endpoint table and error codes), DATABASE.md (real schema from migrations), DOMAIN.md, DELIVERY_PLAN.md, TESTING.md, README.md (how to run, how to run against demo data), CHANGELOG.md (one entry per checkpoint), SECURITY.md (asset handling). Add ADRs only for real decisions (local `dotnet-ef` tool, soft archive, asset storage, Web Audio tones, timezone/numerals handling).

Create `docs/OWNER_TEST_SCRIPT_PHASE2.md` in ARABIC: a step-by-step manual script (about 25 steps) the owner can follow with the demo database and with a fresh database to verify every screen and rule, each step with "المتوقع".

Create `docs/DECISIONS_PENDING.md` (see 0.5).

------------------------------------------------------------
7. FINAL REPORT (write it to `docs/PHASE2_REPORT.md` and also print a short version)
------------------------------------------------------------
1. Per checkpoint: tag, commit, what was built (3-5 lines).
2. Exact test counts and coverage for: dotnet build warnings, dotnet test, eslint, stylelint, vitest, playwright (+axe results).
3. Decisions recorded in DECISIONS_PENDING.md that the owner must confirm.
4. Anything not done, skipped, or partially done, and why. Do not hide gaps.
5. Known issues and risks.
6. Exact commands: run the app, run against demo data, run all tests, open `/design`.
7. Files/folders you noticed that look unrelated to the project (do not touch them).

Rules for the report: only facts backed by command output; no claims without evidence.

Do not start Phase 3 (workload, resources, scheduling profiles, solver). Stop after tag `phase-2` and the report.
