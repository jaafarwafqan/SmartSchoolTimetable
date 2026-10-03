# Database Design

## Core principles
- Shared database, shared schema, tenant-scoped isolation.
- Every tenant-owned table includes `TenantId`.
- PostgreSQL RLS enforced per connection and transaction.
- EF Core global query filters and SaveChanges interceptor enforce app-level guardrails.
- All cache keys prefixed with `t:{tenantId}:`.

## Key schemas
- Users, roles, permissions, refresh tokens, audit log
- SchoolProfile
- Teachers, subjects, resources, stages, sections
- Workload, lesson periods, shift model, bell system
- AcademicCalendar
- Timetable versions, timetable lessons, generation jobs
- Sync cursors and outbox entries

## Data integrity
- Foreign keys and check constraints on all tenant-owned relations.
- Unique indexes include TenantId.
- Optimistic concurrency on versioned entities.
- Soft deletes via tombstones for offline synchronization.

## Operational notes
- One active generation per tenant enforced by PostgreSQL advisory lock.
- Background jobs restore tenant context before work begins.
- Backups and full resets remain tenant-scoped.
