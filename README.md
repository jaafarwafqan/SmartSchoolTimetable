# SmartSchoolTimetable

Single-user, offline-first school timetable application for one local school and one owner account. The browser UI is served by the ASP.NET Core app, which binds only to `127.0.0.1`. Phase 1.2 is under owner acceptance; do not begin Phase 2 before acceptance.

## Stack and environment
- .NET SDK 9.0.318 and Node.js v24.18.0 are available in the current development environment. Node/npm are required for the React build and frontend tests.
- Frontend: React 19, strict TypeScript, Vite, Tailwind CSS, shadcn-style components, TanStack Query, Zustand, React Router, lucide-react, Vitest, and Playwright.
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

### Database reset and first-run setup
Stop the running app before resetting. From the repository root, run:

```powershell
dotnet run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj --configuration Release --no-build -- --reset-local-database
```

The app displays the resolved database path and asks for the exact confirmation `RESET`; any other input cancels. It removes the database and SQLite sidecar files only. This permanently deletes the local owner account and all local data. Start the app using the run command above to return to first-run setup.

## User-facing text, icons, and RTL
- All user-facing UI, errors, validation, generated files, and offline/network messages are Arabic. The API returns stable error codes and parameters only; the UI maps every code through the Arabic dictionary in `frontend/src/i18n/messages.ts`. English is reserved for developer logs, which must never contain passwords, recovery codes, cookies, session IDs, or launch tokens.
- The frontend document is `lang="ar" dir="rtl"` and styles use logical CSS properties. Do not use native browser validation messages, `window.alert`/`confirm`/`prompt`, or native file-input text.
- Use only `lucide-react` for icons. Every action/navigation control has an icon and an Arabic visible label. The only icon-only actions are clear controls such as show/hide password and close, and they require Arabic `aria-label` and `title`.
- Password visibility buttons sit at the logical end of each password input and expose `aria-pressed`. Keep directional icons mirrored in RTL, consistent icon size/stroke, and WCAG AA contrast.
- ESLint enforces localization and icon/label requirements. Follow the checklist in [CLAUDE.md](./CLAUDE.md) and [TESTING.md](./TESTING.md) for each new screen.

## Security and owner recovery
First run creates one local owner and signs in automatically. Before entering the app, the owner must confirm storing the one-time recovery code. If the page is reloaded before acknowledging it, sign in and generate a replacement code in Settings using the current password. The recovery code is the only password-reset path; losing both it and the password has no recovery route. Password changes are optional and require the current password.

Passwords use PBKDF2-HMAC-SHA-256 with 600,000 iterations. Failed login uses a fixed one-second delay, with no escalating delay or temporary lockout. Sessions use an HttpOnly, SameSite=Strict cookie; inactivity auto-lock supports a configured duration or `Never`. Kestrel validates Host/Origin and a per-launch token on state-changing requests. Details are in [SECURITY.md](./SECURITY.md).

## Phase 0 validation notes
- The Google.OrTools and QuestPDF feasibility spikes are isolated under `spikes/CSharpSpikes/`; the Arabic PDF proof was run with .NET 9, QuestPDF 2026.9.1, and Noto Naskh Arabic. QuestPDF licensing clearance and packaged-runtime visual validation remain pre-Phase-6 gates.
- Phase 4 retains the open risk that the exploratory 40-section/54-teacher input found no feasible solution in 30 seconds. The plan requires an independently verified feasible baseline and measured comparisons before acceptance.
