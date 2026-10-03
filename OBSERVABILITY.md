# Local Diagnostics and Audit

## Logging
Use structured local logs for application diagnostics. Do not include passwords, recovery codes, hashes, cookies, per-launch tokens, or unnecessary teacher personal data. There is no remote log collector, telemetry export, tenant id, or user attribution.

## Health and startup checks
- Validate the configured listener before opening the login UI; only `127.0.0.1` is allowed.
- Startup applies EF Core migrations and fails fast on errors or a non-loopback binding. No `/health` endpoints exist (ADR 0013); a loopback-only readiness check may be added if a launcher needs one.
- Do not expose health endpoints on a non-loopback interface.

## Lightweight local history
Persist the user-visible audit events listed in `SECURITY.md`: timetable version changes, publish, rollback, backup/restore, password changes, and imports. Use local timestamp, event, target, and concise change summary; multi-user attribution is not needed.

## Operations
The application is installed and run on one machine. Diagnostics, database files, exports, backups, and logs remain local unless the owner explicitly exports them.
