# Architecture

## Goal
Build a multi-tenant, Arabic-first school timetable platform with one tenant = one school, shared deployment, strong isolation, offline-capable client, and a CP-SAT scheduling engine.

## Layering
- Domain: entities, value objects, aggregates, invariants, domain events; no infrastructure dependencies.
- Application: commands, queries, handlers, DTOs, validators, policies, interfaces.
- Infrastructure: EF Core, PostgreSQL, Redis, OR-Tools adapter, file storage, background jobs, auth providers.
- Api: composition root and thin endpoints using IMediator only.

## High-level flow
1. Users authenticate with tenant-scoped JWTs.
2. API receives requests and dispatches commands/queries through MediatR.
3. Application services validate and authorize.
4. Infrastructure persists state and integrates with PostgreSQL/Redis.
5. Generation jobs are queued to a separate .NET worker process/container; a PostgreSQL advisory lock enforces one active generation per tenant.
6. Solver emits real progress events via SignalR.
7. Client syncs changes through outbox + ChangeSeq model.

The CP-SAT adapter uses Google.OrTools for .NET in Infrastructure. The worker remains in C#; solver types do not cross into Domain or Application.

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
- Audit and security

## Deployment
- api service
- worker service
- web frontend
- PostgreSQL
- Redis
- optional observability stack (OpenTelemetry + Seq/Prometheus/Grafana)

## Acceptance criteria for Phase 0
- Architecture documented
- Major decisions captured in ADRs
- Security and tenancy model explicitly stated
- Solver and Arabic PDF spikes executed and reported honestly
- no implementation beyond isolated spikes
