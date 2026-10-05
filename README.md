# SmartSchoolTimetable

Single-user, offline-first school timetable application for one local school and one owner account. The browser UI is served by the ASP.NET Core app, which binds only to `127.0.0.1`. Phase 1.4 (design system adoption, tag `phase-1.4`) is complete. Phase 2 checkpoints 2A–2C are implemented on `phase-2`; checkpoints 2D–2F remain (see [DELIVERY_PLAN.md](./DELIVERY_PLAN.md)). The owner merges after acceptance.

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

### Phase 2.5 owner test
Follow `docs/OWNER_TEST_SCRIPT_PHASE25.md` on a separate test database (`$env:Database__Path`). The results are in `docs/PHASE25_REPORT.md`.

### Database reset and first-run setup
Stop the running app before resetting. From the repository root, run:

```powershell
dotnet run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj --configuration Release --no-build -- --reset-local-database
```

The app displays the resolved database path and the prompts in Arabic, and asks for the exact confirmation `RESET` (kept ASCII so it can be typed on any keyboard layout); any other input cancels. It removes the database and SQLite sidecar files only. The console is switched to UTF-8; if Arabic shows as boxes, use Windows Terminal or a console font with Arabic glyphs. This permanently deletes the local owner account and all local data. Start the app using the run command above to return to first-run setup.

### Interim database backup (until Phase 6)
There is no in-app backup yet. **Stop the app first** (close the console or press Ctrl+C), then run this from the folder where the backup should be created:

```powershell
if (Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'SmartSchoolTimetable.Api.exe' -or ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -like '*SmartSchoolTimetable.Api.dll*') }) { Write-Error 'التطبيق يعمل. أوقفه أولاً ثم أعد تنفيذ النسخ الاحتياطي.' } else { $db = "$env:LOCALAPPDATA\SmartSchoolTimetable\timetable.db"; $dest = ".\timetable-backup-$(Get-Date -Format yyyyMMdd-HHmmss)"; New-Item -ItemType Directory $dest | Out-Null; Copy-Item "$db*" $dest; Get-ChildItem $dest }
```

The command **refuses to run while the app is running**: it checks for the app process, because the app does not keep the database file locked between requests, so a file-lock check cannot detect it. Once the app is stopped it copies `timetable.db` together with `timetable.db-wal` and `timetable.db-shm` (present after an unclean shutdown) into a timestamped folder, and lists what was copied. Both cases were verified in Phase 1.4. Paste the command into PowerShell directly; script files may be blocked by the execution policy.
- Restore: stop the app and copy all files from a backup folder back into `%LOCALAPPDATA%\SmartSchoolTimetable\`.
- If `Database:Path` is configured, use that path instead.
- From Phase 2, the school logo and stamp are stored in the `assets` folder next to the database. Copy that folder as well, if it exists: `Copy-Item "$env:LOCALAPPDATA\SmartSchoolTimetable\assets" $dest -Recurse`.
- Never copy the database while the app is running. The Phase 6 online backup (SQLite Online Backup API, integrity-checked) will replace this procedure.

### Demo data (separate database)
Create a fictional sample school in a NEW file. The command refuses an existing file, the default `%LOCALAPPDATA%` database and the configured `Database:Path`:

```powershell
dotnet run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj --configuration Release --no-build -- --seed-demo-data "$env:TEMP\sst-demo\demo.db"
# optional: add --dual-shift for a morning and an evening shift
```

Then run the app against it. The first start asks you to create the owner account for the demo database:

```powershell
dotnet run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj --configuration Release --no-build -- --Database:Path="$env:TEMP\sst-demo\demo.db"
```

The sample contains:
- 20 teachers with varied constraints (2 fully released);
- 10 subjects that exercise every flag;
- 4 stages with 12 sections;
- one shift of 7 lessons plus a break (two shifts with `--dual-shift`);
- the 2026-2027 year with two terms;
- 8 calendar days.

Teacher workload is Phase 3. A manual test script in Arabic is in [docs/OWNER_TEST_SCRIPT_PHASE2.md](./docs/OWNER_TEST_SCRIPT_PHASE2.md).

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
- The Google.OrTools and QuestPDF feasibility spikes are isolated under `spikes/CSharpSpikes/`; the Arabic PDF proof was run with .NET 9, QuestPDF 2026.9.1, and Noto Naskh Arabic. QuestPDF licensing clearance and packaged-runtime visual validation remain pre-Phase-6 gates.
- Phase 4 retains the open risk that the exploratory 40-section/54-teacher input found no feasible solution in 30 seconds. The plan requires an independently verified feasible baseline and measured comparisons before acceptance.
