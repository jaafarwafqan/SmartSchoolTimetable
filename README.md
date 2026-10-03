# SmartSchoolTimetable

Phase 0 architecture and de-risking package for a single-user, local school timetable application.

## Included
- Architecture, database, domain, API, security, local/offline operation, testing, diagnostics, and delivery documentation
- ADRs for the local-only architecture, owner authentication, optional SQLCipher, CP-SAT, printing, and PDF rendering
- Isolated C# spikes under `spikes/CSharpSpikes/` using Google.OrTools and QuestPDF

## Environment checked for Phase 0
- .NET SDK 9.0.318: installed at `C:\Program Files\dotnet\dotnet.exe` (directory is not currently on `PATH`)
- Node.js v24.18.0
- Docker CLI: not installed/on PATH; not required by the approved local-only architecture
- PostgreSQL (`psql`, `postgres`): not installed/on PATH; not required (local SQLite is the baseline)
- Redis (`redis-server`, `redis-cli`): not installed/on PATH; not required

Before Phase 1, add `C:\Program Files\dotnet` to `PATH` or invoke the SDK by its full path. No Docker, PostgreSQL, or Redis service is required. The owner UI/API must bind only to `127.0.0.1`; there is no external network service.

## Current status
Phase 0 documentation update is committed/tagged as `phase-0.3`. No Phase 1 implementation has started. Phase 1 remains gated on the owner's exact reply `approved`.
