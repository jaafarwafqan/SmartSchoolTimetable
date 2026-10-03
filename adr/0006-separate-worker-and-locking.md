# ADR 0006: Separate generation worker and PostgreSQL lock

- Status: Accepted
- Date: 2026-10-03

## Context
Timetable generation is CPU-intensive, must not occupy an HTTP request, and must not run concurrently more than once for a tenant.

## Decision
Run generation in a separate .NET worker process/container. Before processing a tenant's generation job, acquire a PostgreSQL advisory lock keyed by tenant identity; release it on completion, cancellation, or process/connection termination. PostgreSQL, not Redis, is the source of truth for this exclusivity.

## Consequences
- API request lifetimes and solver CPU/memory usage are isolated.
- Multiple worker replicas can share the queue while PostgreSQL enforces one active generation per tenant.
- Lock-key derivation and connection lifetime must be consistent, collision-safe for distinct tenants, and covered by integration tests.
- Worker startup must restore the job's tenant context explicitly.
- Progress is emitted from real solver events; the lock does not replace durable job state or cancellation handling.
