# SmartSchoolTimetable

Phase 0 architecture and de-risking package for the enterprise timetable platform.

## Included
- Architecture, database, domain, API, security, offline sync, testing, observability, and delivery documentation
- ADRs for tenancy, solver language and architecture, offline printing, generation worker locking, deterministic solver mode, and PDF rendering
- Isolated C# spikes under `spikes/CSharpSpikes/` using Google.OrTools and QuestPDF

## Environment checked for Phase 0
- .NET SDK 9.0.318: installed at `C:\Program Files\dotnet\dotnet.exe` for the C# spikes (the directory is not currently on `PATH`)
- Node.js v24.18.0: available
- Docker CLI: not installed/on PATH
- PostgreSQL (`psql`, `postgres`): not installed/on PATH
- Redis (`redis-server`, `redis-cli`): not installed/on PATH

Before Phase 1, add `C:\Program Files\dotnet` to `PATH` (or invoke the SDK by its full path), and install Docker Desktop with its Compose-capable engine. PostgreSQL and Redis must be available as services, preferably through the project's Compose configuration, or as compatible local installations. The Phase 0 standalone .NET spike does not require Docker, PostgreSQL, or Redis.

## Status
Phase 0 is committed and tagged `phase-0`. No Phase 1 implementation will begin until the owner replies exactly `approved`.
