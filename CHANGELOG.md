# Changelog

## [Unreleased]
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
