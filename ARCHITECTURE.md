# Architecture

## Product boundary
This is a single-user, local school timetable application. One installation stores one school's data on the local machine. One owner account signs in before accessing the application. The browser UI and local ASP.NET Core host communicate only over loopback; the product has no remote service, multi-tenant hosting, or network sync.

## Layering
- Domain: entities, value objects, invariants, and domain events; no infrastructure dependencies.
- Application: use-case services (for example `LocalAuthService`), interfaces, the `ErrorCodes` registry, credential rules, and DTOs.
- Infrastructure: EF Core with local SQLite in WAL mode (per-connection `synchronous=FULL` via an EF connection interceptor), local audit history, and later the Google.OrTools adapter (Phase 4) and local file storage.
- Api: composition root and thin local endpoints. Endpoints bind and validate the request (FluentValidation), call one Application service method directly, and map failures through the central error-code table. MediatR/CQRS is not used (see [ADR 0013](./adr/0013-phase-1-scope-trimming.md)).
- Enforcement: `tests/SmartSchoolTimetable.Tests/ArchitectureTests.cs` and `ArchitectureDependenciesFlowInward` fail the build's test run if Domain/Application reference outer layers, if the Api assembly references Domain or EF Core, if any Api type uses a `DbContext` or Domain entity, or if Google.OrTools appears in Domain/Application. All projects build with nullable enabled, `TreatWarningsAsErrors`, and `AnalysisLevel=latest-recommended` (`Directory.Build.props`, `.editorconfig`).

## Local process flow
1. The local host performs first-run owner setup when no account exists; otherwise it shows the login screen before loading application routes.
2. The web UI is served from the same local host and binds only to `127.0.0.1`.
3. Authenticated requests use a local HttpOnly, SameSite=Strict session cookie. State-changing requests additionally require a random per-launch token.
4. Local state is persisted to SQLite. There is no remote API, tenant context, synchronization service, Redis, or network telemetry.
5. Generation runs as a background job isolated from the HTTP request path. Only one local generation job may be active at a time; no distributed worker fleet or PostgreSQL lock is used.

The CP-SAT adapter uses Google.OrTools for .NET in Infrastructure. Solver types do not cross into Domain or Application.

## Core domains
- School Profile
- Teachers
- Subjects
- Stages and Sections
- Workload and capacity
- Bell System / Shift Model
- Academic Calendar
- Timetable generation and versioning
- Attendance and daily monitoring
- Local owner account and audit history

## Deployment
- One local ASP.NET Core host and web UI
- Local SQLite database and local files
- Repository/unit-of-work abstraction isolates Application and Domain from the SQLite provider; PostgreSQL remains a possible future provider subject to ADR and compatibility work
- Local background generation service
- No Docker, cloud services, remote database, Redis, public listener, or inbound LAN access required
- Optional WebView2 shell may be evaluated later; browser login remains the baseline

## Acceptance criteria for Phase 0
- Architecture documented
- Major decisions captured in ADRs
- Single-user, local-only security model explicitly stated
- Solver and Arabic PDF spikes executed and reported honestly (the spikes were removed after delivery; they remain at tag `phase-0`)
- No application implementation beyond isolated spikes
