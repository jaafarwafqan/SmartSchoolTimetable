# Database Design

## Storage model
- One local SQLite database per installation; no shared database, remote database, tenant key, Redis, or PostgreSQL RLS.
- EF Core migrations are the only schema change mechanism.
- Database and backups are local files. Access control relies on the operating-system account and file permissions; baseline SQLite is not encrypted.
- SQLCipher encryption is an optional final-phase decision, recorded in `adr/0010-sqlcipher-go-no-go.md`.

## Owner account
The local `Users` table stores the single owner account and is intentionally shaped as one account row per identity so additional users could be added later without replacing the identity table. The application enforces exactly one owner account at present; the schema does not include tenant, role, or permission columns.

Account fields include:
- stable account id and unique normalized username;
- versioned password hash, random salt, and password-hash parameters;
- one-time recovery-code hash, nullable after use or rotation;
- failed-login count, last failure time, and temporary lockout-until value;
- created/updated timestamps.

First-run setup is permitted only while the account table is empty. It atomically creates the owner and stores only the recovery-code hash. There is no refresh-token table.

## Application data
- School profile; teachers, subjects, resources, stages, sections, workload, shifts, bell times, calendar
- Timetable versions and lessons
- Local audit history for version changes, publish, rollback, backup/restore, password changes, and imports
- Any future application tables use local stable identifiers and explicit foreign keys; no `TenantId`

## Integrity and concurrency
- Foreign keys, unique constraints, check constraints, and optimistic version columns protect local data.
- A local single-user process does not need cross-tenant isolation or distributed concurrency coordination.
- UI and application services prevent conflicting local edits and concurrent generation jobs.
- Backups/restores operate on the local database and files and must be validated before replacement.
