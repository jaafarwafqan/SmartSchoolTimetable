# ADR 0037: Google.OrTools dependency and in-process generation

- Status: Accepted (Phase 4)
- Date: 2026-10-09

## Context
Phase 4 generates the timetable with CP-SAT (ADR 0002). The owner's delivery prompt allows exactly one new runtime dependency for the solver. The app is local and has one user (ADR 0008).

## Decision
- Add `Google.OrTools` **9.15.6755**, the version the Phase 0 spike used, to `SmartSchoolTimetable.Infrastructure` only.
  - Purpose: CP-SAT solver.
  - Licence: Apache-2.0.
  - Native binaries: the `google.ortools.runtime.win-x64` package, restored transitively. The other runtime packages restore too but are not loaded on Windows.
  - Maintenance: Google, regular releases.
  - Size: 70.6 MB of native DLLs, measured in the NuGet cache for win-x64. The largest are `ortools.dll` (32.9 MB), `libprotobuf.dll` (12.9 MB), `libscip.dll` (12.2 MB) and `highs.dll` (8.1 MB).
- `ISolver`, `GenerationSettings`, `SolverResult` and the other neutral types live in `Application/Generation`. The OR-Tools adapter (`CpSatSolver`, `CpSatModelBuilder`, `CpSatDiagnostics`) lives in `Infrastructure/Solver`. `ArchitectureTests.OrToolsIsNotUsedByDomainApplicationOrApi` fails the build if any `Google.OrTools` type appears in Domain, Application or Api.
- Generation runs **inside the local app** as a hosted background service, never on the request thread, with **one active generation at a time**. A database row guards this (the active `GenerationRun`), so it is not held only in memory. There is no worker container and no message broker. Progress is polled; there is no SignalR.
- Startup self-check: `OrToolsInfo` loads the native library and reads `OrToolsVersion.VersionString()`. If the library does not load, the generation endpoints return `SOLVER_UNAVAILABLE` (Arabic text on the screen) instead of crashing.

## Alternatives
- A Python worker: rejected in ADR 0002, because it adds a second runtime and a serialization boundary.
- A greedy or heuristic placer: rejected. It cannot prove impossibility or explain it, and the owner's prompt forbids it.
- A newer OR-Tools version: not needed; the spike version is validated on this machine.

## Consequences
- The publish folder is larger because of the native library.
- The solver version is stored with every run (`GenerationRun.SolverVersion`). An upgrade can change timetables for the same input (ADR 0007).
