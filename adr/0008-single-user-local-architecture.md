# ADR 0008: Single-user local-only application

- Status: Accepted by owner scope clarification
- Date: 2026-10-03

## Context
The prior architecture described a multi-tenant service with remote synchronization, shared PostgreSQL/Redis, and distributed worker containers. The owner has clarified the product boundary: one local school, one owner account, no roles or permission matrix, no concurrent users, and no external network access.

## Decision
- Ship as one local application with a browser-based login/setup screen and local ASP.NET Core host.
- Bind Kestrel exclusively to `127.0.0.1`; local browser traffic is the only supported HTTP client.
- Use one local SQLite database and local file storage. No tenant identifier, remote database, PostgreSQL, Redis, Docker dependency, remote API, sync service, or telemetry export.
- Keep one owner-account row at runtime in an extensible local users table; do not model roles or permissions.
- Keep generation off the HTTP request path with one active local background job; no distributed queue/container or PostgreSQL advisory lock.
- Core operation, including PDF export and printing, must not require internet connectivity.
- Browser login is the baseline. A standalone WebView2 shell is optional and requires separate approval/acceptance.

## Consequences
- The system is simpler to install and operate on one workstation.
- Localhost is still reachable by browser-originated requests, so loopback binding alone is not a security boundary; the controls in ADR 0009 and `SECURITY.md` remain mandatory.
- Data protection at rest relies on OS account/file permissions in the initial release. SQLCipher remains an optional final-phase decision, not an implemented feature.
- Multi-user, multi-school, remote sync, and distributed execution require a new owner-approved ADR before they can be introduced.

## Superseded decisions
ADR 0001 ([tenant isolation](../docs/archive/0001-tenant-isolation.md)), ADR 0003 ([remote outbox/ChangeSeq sync](../docs/archive/0003-offline-sync.md)), and ADR 0006 ([distributed worker](../docs/archive/0006-separate-worker-and-locking.md) with PostgreSQL tenant lock) are retained as historical records but are superseded for this product scope.
