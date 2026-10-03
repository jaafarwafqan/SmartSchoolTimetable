# Changelog

## [Unreleased]
### Phase 0.4 - storage, acceptance, and risk criteria
- Added the SQLite WAL/safe-backup/provider-abstraction ADR and detailed its backup integrity requirements.
- Reworked the delivery plan into eight implementation phases with acceptance criteria for every phase.
- Recorded the 40-section / 54-teacher 30-second no-solution risk and required measured investigation of two-stage solving, decomposition, and solver hints.
- Added a multi-constraint infeasibility-diagnostic acceptance case and a QuestPDF licensing gate before Phase 6.
- Clarified that .NET 9 and Node are the only required build toolchains; Docker, PostgreSQL, and Redis are not required. .NET 9 is installed but currently not on PATH.
- Documentation only; Phase 1 has not started.

### Phase 0.3 - owner scope clarification
- Replaced the multi-tenant/multi-user deployment assumptions with a single-user, local-only application architecture.
- Documented one-time owner setup/recovery, password/session/lockout requirements, loopback and localhost-attack protections, and local audit history.
- Updated the delivery and test criteria for Phases 1 and 8.
- Added ADRs for local deployment, owner authentication, and the SQLCipher final-phase go/no-go; marked tenant sync and distributed worker ADRs superseded.
- No application code was added; Phase 1 remains gated on explicit owner approval.

### Phase 0
- Added architecture, database, domain, API, sync, security, testing, observability, and delivery-plan documentation.
- Added ADRs for tenancy, C# CP-SAT, offline sync/printing, PostgreSQL worker locking, deterministic solving, and Arabic PDF rendering.
- Ran the .NET 9 / Google.OrTools C# spike with hard constraints, weighted soft preferences, shared-resource capacity, single/multi-worker timings, and assumption-core infeasibility diagnostics.
- Ran the QuestPDF/Noto Naskh Arabic RTL spike, generated a PDF, and visually inspected its PNG render.
- Recorded the 40-section / 20-teacher workload-cap infeasibility and local environment prerequisites.
