# ADR 0006: Separate generation worker and PostgreSQL lock

- Status: Superseded by ADR 0008
- Date: 2026-10-03

## Context
The original product scope called for a distributed generation worker and one active generation per tenant.

## Historical decision
Run generation in a separate .NET worker process/container. Acquire a PostgreSQL advisory lock keyed by tenant identity to enforce one active generation per tenant.

## Supersession
The owner clarified the product as one local user and one local installation, with no network service or multi-user concurrency. Do not implement tenant-keyed PostgreSQL locks, distributed queues, or worker containers. Generation remains a local background job, isolated from the HTTP request path, with at most one active local job. See ADR 0008.
