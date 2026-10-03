# Local API Design

## Boundary
- The API is a private implementation detail of the local application, bound only to `127.0.0.1`.
- The browser UI is served by the same local host and uses same-origin requests.
- No remote clients, public API, tenant identifier, network sync endpoints, or external service integration.
- No role or permission matrix. Authentication distinguishes only an unauthenticated session from the local owner session.

## Local request families
- Setup: first-run owner creation and one-time recovery-code issuance
- Session: login, logout, password change, recovery-code reset/rotation, inactivity lock
- School settings and timetable domain operations
- Local timetable generation and progress
- Local PDF/Excel exports, backup/restore, and imports

## Browser request security
- Canonical `Host` and `Origin` validation for the bound loopback address and active port
- No wildcard CORS; same-origin UI is the supported client
- HttpOnly, SameSite=Strict local session cookie
- Per-launch token required in a dedicated header for state-changing requests
- Error responses must not reveal password hashes, recovery-code hashes, filesystem secrets, or stack traces

## Error model
- 400 validation error
- 401 unauthenticated
- 404 not found
- 409 local version/state conflict
- 423 temporarily locked after repeated failed login
- 500 generic server error

There is no user/tenant attribution in request logs. Avoid logging credentials, recovery codes, cookies, launch tokens, or timetable personal data.
