# CLAUDE.md

## Project
Single-user local Smart School Timetable application. One local school, one owner account, browser-based login, Arabic-first RTL, and core operation without internet access. Local Kestrel binds only to `127.0.0.1`.

## Active phase
Phase 0 documentation only. Do not start Phase 1 until the owner replies exactly `approved`.

## Rules
- No application code before Phase 1 approval.
- No multi-tenancy, RBAC, permission matrix, refresh-token rotation, remote sync, or multi-user concurrency.
- Keep a minimal local users table with one owner row now and a future-extensible account shape.
- Protect localhost endpoints from malicious websites with strict Host/Origin validation, SameSite cookies, no wildcard CORS, and a per-launch token on state-changing requests.
- Maintain ADRs for non-trivial decisions; do not silently alter architecture.
- No dependencies without purpose, security, maintenance, and alternative analysis.
- Never log passwords, recovery codes, cookies, or session tokens.

## Current status
- Phase 0 architecture/security scope clarified and documented.
- Awaiting owner approval before implementation.
