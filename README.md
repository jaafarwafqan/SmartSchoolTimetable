# SmartSchoolTimetable

Single-user, offline-first school timetable application for one local school and one owner account. The browser UI is served by the ASP.NET Core app, which binds only to `127.0.0.1`.

**Status:** Phases 0–4 are delivered on `master`; the latest tag is `phase-4i`.
- School setup, curriculum, teachers, workload and readiness.
- CP-SAT timetable generation with an independent verifier.
- Viewer by section, teacher and school; manual editing and approval.
- Printing (A4, PDF through the browser) and Excel export.
- In-app backup and restore.
- 12-hour times, flexible breaks, and the double shift (دوام مزدوج) with a day→session mapping per semester.

The change history is in [CHANGELOG.md](./CHANGELOG.md), and the latest report is [docs/PHASE4G_REPORT.md](./docs/PHASE4G_REPORT.md).

## For the school: the ready-to-run program
1. On the development machine, from the repository root, run `powershell -ExecutionPolicy Bypass -File tools\Publish-Release.ps1`. It writes a new folder `artifacts\release\SmartSchoolTimetable-<date-time>` (self-contained win-x64, about 200 MB). The folder is not stored in git.
2. Copy that folder to the school computer. No .NET installation is needed.
3. Double-click «تشغيل البرنامج.bat». The browser opens `http://127.0.0.1:5080/`; keep the black window open while working.
4. The first run shows the owner setup (account, recovery code), then the wizard. The short Arabic guide is [docs/USER_GUIDE_AR.md](./docs/USER_GUIDE_AR.md).

## Stack and environment
- .NET SDK 9.0.318 and Node.js v24.18.0 are available in the current development environment. Node/npm are required for the React build and frontend tests.
- Frontend: React 19, strict TypeScript, Vite, Tailwind CSS, shadcn-style components, TanStack Query, Zustand, React Router, lucide-react, the bundled Noto Sans Arabic font (offline, [ADR 0014](./adr/0014-bundled-ui-font.md)), Stylelint, axe, Vitest, and Playwright.
- The API serves Vite's built files from `src/SmartSchoolTimetable.Api/wwwroot`. The login/setup UI source is `frontend/src/App.tsx`.
- Docker, PostgreSQL, and Redis are not required. SQLite is embedded/local; the default database is `%LOCALAPPDATA%\SmartSchoolTimetable\timetable.db`.
- Never bind the application to `0.0.0.0`, a LAN address, or a wildcard interface.

## Build, test, and run (PowerShell)
Run from the repository root, in this order. The .NET build restores frontend dependencies when needed and builds the React bundle.

```powershell
dotnet build .\SmartSchoolTimetable.sln --configuration Release
dotnet test .\SmartSchoolTimetable.sln --configuration Release
npm.cmd --prefix .\frontend run lint
npm.cmd --prefix .\frontend exec playwright install chromium
npm.cmd --prefix .\frontend test
dotnet run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj --configuration Release --no-build
```

The frontend test command runs Vitest and Playwright; install Chromium once with the preceding command. While the final command is running, open <http://127.0.0.1:5080/>. The app is available only over loopback HTTP.

### Dependency audit
`npm.cmd --prefix .\frontend run audit:prod` checks only the production dependencies (the code shipped to the browser) and fails on any high-severity finding. `npm.cmd --prefix .\frontend audit` also lists development tools; the current development-only finding (`braces` through stylelint) is explained in `docs/DECISIONS_PENDING.md` #44.

### Owner test scripts
Run them on a separate test database (see below), never on the school's real data. The current one is [docs/OWNER_TEST_SCRIPT_PHASE4.md](./docs/OWNER_TEST_SCRIPT_PHASE4.md) (steps 1–34). It covers setup, generation, the viewer, edits, approval, printing, Excel, backup and restore, the release folder, and the double shift. The earlier scripts are for Phases 2, 2.5 and 3 (`docs/OWNER_TEST_SCRIPT_PHASE*.md`).

### Readiness and generation
- «جاهزية الجدولة» (`/readiness`) checks the current year's real data before generation.
- «الجدول ← التوليد» runs the CP-SAT solver with real progress and can be stopped at any time. An independent verifier checks every timetable.
- «الجدول ← الجداول» shows the saved versions, the views, manual editing, approval, printing and Excel.

Solver design: [docs/SOLVER.md](./docs/SOLVER.md). Measured performance: [docs/PERFORMANCE.md](./docs/PERFORMANCE.md).

### Database reset and first-run setup
Stop the running app before resetting. From the repository root, run:

```powershell
dotnet run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj --configuration Release --no-build -- --reset-local-database
```

The app displays the resolved database path and the prompts in Arabic, and asks for the exact confirmation `RESET` (kept ASCII so it can be typed on any keyboard layout); any other input cancels. It removes the database and SQLite sidecar files only. The console is switched to UTF-8; if Arabic shows as boxes, use Windows Terminal or a console font with Arabic glyphs. This permanently deletes the local owner account and all local data. Start the app using the run command above to return to first-run setup.

### Backup and restore
- In the app: «الإعدادات ← عام ← النسخ الاحتياطي والاستعادة».
- **Backup** writes a new, consistent file (`timetable-backup-<date-time>.db`) to a folder you choose. It never overwrites a file.
- **Restore** needs two confirmations. It first saves an automatic copy of the current data (`backups\pre-restore-<date-time>.db` next to the database). It upgrades an older backup to the current schema, then signs you out.
- No backup is ever deleted by the app. Details: [adr/0042-backup-restore-and-release-folder.md](./adr/0042-backup-restore-and-release-folder.md).
- The school logo and stamp are in the `assets` folder next to the database. Copy that folder too when moving data to another computer.

### A separate test database
The demo data generator (`--seed-demo-data`) was removed (2026-10-09, owner decision). To try the app without touching the real database, run it against a NEW file; the first start shows the first-run setup (owner account, recovery code, then the wizard):

```powershell
dotnet run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj --configuration Release --no-build -- --Database:Path="$env:TEMP\sst-try\try.db"
```

The published launcher accepts the same option. For example, `SmartSchoolTimetable.Api.exe --Database:Path=...` from the release folder.

Automated tests build their own synthetic data inside the test projects and never touch the real database.

### Style guide (`/design`, development only)
```powershell
npm.cmd --prefix .\frontend run dev
```
Then open <http://127.0.0.1:5173/design>. The route exists only in the Vite development server; production builds served by the API do not include it.

## User-facing text, icons, and RTL
- All user-facing UI, errors, validation, generated files, and offline/network messages are Arabic. The API returns stable error codes and parameters only; the UI maps every code through the Arabic dictionary in `frontend/src/i18n/messages.ts`. English is reserved for developer logs, which must never contain passwords, recovery codes, cookies, session IDs, or launch tokens.
- The frontend document is `lang="ar" dir="rtl"` and styles use logical CSS properties. Do not use native browser validation messages, `window.alert`/`confirm`/`prompt`, or native file-input text.
- Use only `lucide-react` for icons. Every action/navigation control has an icon and an Arabic visible label. The only icon-only actions are clear controls such as show/hide password and close, and they require Arabic `aria-label` and `title`.
- Password visibility buttons sit at the logical end of each password input and expose `aria-pressed`. Keep directional icons mirrored in RTL, consistent icon size/stroke, and WCAG AA contrast.
- ESLint enforces localization and icon/label requirements. Follow the checklist in [CLAUDE.md](./CLAUDE.md) and [TESTING.md](./TESTING.md) for each new screen.

## Security and owner recovery
First run creates one local owner and signs in automatically. Before entering the app, the owner must confirm storing the one-time recovery code. If the page is reloaded before acknowledging it, a blocking screen replaces the app until a replacement code is generated with the current password and acknowledged. The recovery code is the only password-reset path; losing both it and the password has no recovery route. Password changes are optional and require the current password.

Passwords must be 8–1024 characters and use PBKDF2-HMAC-SHA-256 with 600,000 iterations. Failed login uses a fixed one-second delay, with no escalating delay or temporary lockout. Sessions use an HttpOnly, SameSite=Strict cookie; inactivity auto-lock supports a configured duration or `Never`. Kestrel validates Host/Origin and a per-launch token on state-changing requests. Details are in [SECURITY.md](./SECURITY.md).

## Phase 0 validation notes
- The Google.OrTools and QuestPDF feasibility spikes are kept under `spikes/CSharpSpikes/` as history.
  - The product uses OR-Tools CP-SAT 9.15 ([ADR 0037](./adr/0037-or-tools-dependency-and-in-process-generation.md)).
  - It does not use QuestPDF: printing and PDF go through the browser's print dialog, and Excel uses ClosedXML ([ADR 0041](./adr/0041-excel-export-closedxml.md)).
- The Phase 0 risk was that a 40-section/54-teacher input found no timetable in 30 s. It did not show in Phase 4: [docs/PERFORMANCE.md](./docs/PERFORMANCE.md) records a first timetable after about 3.4 s on synthetic 40/54 and 40/60 schools. That is one seed per size, and real data with tight availability can be harder.
