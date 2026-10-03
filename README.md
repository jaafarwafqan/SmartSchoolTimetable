# SmartSchoolTimetable

Single-user, local school timetable application. The approved Phase 1 foundation provides a browser-based owner setup/login screen, local SQLite storage, and a loopback-only ASP.NET Core host. Phase 1 acceptance is under owner review; no Phase 2 work has started.

## Included
- Architecture, database, domain, API, security, local/offline operation, testing, diagnostics, and delivery documentation
- ADRs for the local-only architecture, owner authentication, SQLite, optional SQLCipher, CP-SAT, printing, and PDF rendering
- Isolated C# spikes under `spikes/CSharpSpikes/` using Google.OrTools and QuestPDF

## Environment checked for Phase 0
- .NET SDK 9.0.318: installed at `C:\Program Files\dotnet\dotnet.exe`, but **not currently on `PATH`**
- Node.js v24.18.0
- Docker CLI: not installed/on PATH; not required by the approved local-only architecture
- PostgreSQL (`psql`, `postgres`): not installed/on PATH; not required (local SQLite is the baseline)
- Redis (`redis-server`, `redis-cli`): not installed/on PATH; not required

Only the .NET 9 SDK (local API/backend) and Node.js (web UI build) are required for application development/build. Add `C:\Program Files\dotnet` to `PATH` before using the bare `dotnet` command, or use the full executable path. Docker, PostgreSQL, and Redis are not required. SQLite is embedded/local. The owner UI/API must bind only to `127.0.0.1`; there is no external network service.

## Current status
Phase 0 documentation is committed/tagged as `phase-0.4`. Phase 1 foundation and owner authentication are implemented; see the acceptance test mapping in [DELIVERY_PLAN.md](./DELIVERY_PLAN.md). The current local database has one owner account. Phase 1 is not accepted until the owner confirms it.

## Build, test, and run (PowerShell)
Run these commands from the repository root, in order:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build .\SmartSchoolTimetable.sln --configuration Release
& 'C:\Program Files\dotnet\dotnet.exe' test .\SmartSchoolTimetable.sln --configuration Release
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj --configuration Release --no-build
```

While the third command is running, open <http://127.0.0.1:5080/> in the browser. Kestrel binds only to this loopback address.

The local SQLite database is `%LOCALAPPDATA%\SmartSchoolTimetable\timetable.db` (normally `C:\Users\<your-user>\AppData\Local\SmartSchoolTimetable\timetable.db`).

### Reset for first-run setup
Close the application first. From the repository root, run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj --configuration Release --no-build -- --reset-local-database
```

The command displays the database path and deletes only after you type the exact confirmation `RESET`. Any other input cancels without deleting files. This removes the database and its SQLite WAL/shared-memory/journal sidecars; start the app again with the command above to see first-run setup. Resetting permanently deletes the local account and all local data.
