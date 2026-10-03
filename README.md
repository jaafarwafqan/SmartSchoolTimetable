# SmartSchoolTimetable

Phase 0 architecture and de-risking package for a single-user, local school timetable application.

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
Phase 0 documentation update is committed/tagged as `phase-0.4`. No Phase 1 implementation has started. Phase 1 remains gated on the owner's exact reply `approved`.
