# Database Design

## Storage model
- One local SQLite database per installation, WAL enabled; no shared database, remote database, tenant key, Redis, or PostgreSQL RLS. See [ADR 0011](./adr/0011-sqlite-storage.md).
- EF Core migrations are the only schema change mechanism.
- Database and backups are local files. Access control relies on the operating-system account and file permissions; baseline SQLite is not encrypted.
- SQLCipher encryption is an optional final-phase decision, recorded in `adr/0010-sqlcipher-go-no-go.md`.
- The canonical database is one `.db` file. WAL mode may create transient `-wal` and `-shm` sidecars while open; never copy the live main database file as a backup.
- Use SQLite Online Backup API/provider equivalent for a consistent single-file backup. A file-copy alternative requires stopping/quiescing database users, checkpointing/truncating WAL, closing connections, copying the main file, and validating the copy with SQLite integrity checks.

## Persistence abstraction
Domain and Application depend on repository/unit-of-work contracts, not SQLite connection types or SQL dialect. EF Core SQLite mappings and database operations live in Infrastructure. This keeps a future PostgreSQL provider possible without committing to or claiming unverified cross-provider compatibility. PostgreSQL would require a new ADR, provider-specific migrations, and compatibility tests.

## Owner account
The local `Users` table stores the single owner account and is intentionally shaped as one account row per identity so additional users could be added later without replacing the identity table. The application enforces exactly one owner account at present; the schema does not include tenant, role, or permission columns.

Account fields (`Users` table, migrations in `src/SmartSchoolTimetable.Infrastructure/Migrations/`):
- `Id` (INTEGER PK); `OwnerSlot` (always `owner`, unique index — enforces the single owner); `Username`; `NormalizedUsername` (unique index);
- `PasswordSalt` (16 bytes), `PasswordHash` (32 bytes), `PasswordIterations` (600,000). The algorithm (PBKDF2-HMAC-SHA-256) is fixed in code; there is no algorithm/version column, so changing algorithms requires an ADR, a version column and a migration;
- `RecoverySalt` (16 bytes) and `RecoveryCodeHash` (32-byte salted SHA-256), both NOT NULL; the hash is replaced (never nulled) on recovery or regeneration;
- `RecoveryCodeAcknowledged` (bool);
- `HasCustomInactivityTimeout` (bool, default false) and `InactivityTimeoutMinutes` (int, nullable) hold the owner's auto-lock choice. Until a choice is made the configured default applies; when `HasCustomInactivityTimeout` is true, a `NULL` value means "never". These columns were added by `20261003175642_AddOwnerInactivityTimeoutPreference`, generated with the local `dotnet-ef` tool (ADR 0015);
- `CreatedAt`, `UpdatedAt`.

The failed-login/lockout columns from the initial migration were removed by `20261003160000_RemoveEscalatingLoginLockoutFields`. `AuditHistory` stores `Id`, `OccurredAt` (indexed), `EventType`, `Target`, `Summary`; current events are `OwnerAccountCreated`, `RecoveryCodeRegenerated`, `PasswordChanged`, and `InactivityTimeoutChanged`. The test `EfModelHasNoPendingChangesVersusTheLatestMigration` fails if the EF model drifts from the latest migration.

First-run setup is permitted only while the account table is empty. It atomically creates the owner and stores only the recovery-code hash. There is no refresh-token table.

## Application data
- School profile; teachers, subjects, resources, stages, sections, workload, shifts, bell times, calendar
- Timetable versions and lessons
- Local audit history for version changes, publish, rollback, backup/restore, password changes, and imports
- Any future application tables use local stable identifiers and explicit foreign keys; no `TenantId`

## Connection settings
- `Foreign Keys=True`, `Default Timeout=30`, `Pooling=False` in the connection string (`LocalInfrastructureRegistration`).
- `PRAGMA journal_mode=WAL` is set once at startup and persists in the database file.
- `PRAGMA synchronous=FULL` is connection-scoped, so `SqlitePragmaInterceptor` applies it every time EF Core opens a connection (tested by `EveryEfCoreConnectionAppliesSynchronousFull`).

## Integrity and concurrency
- Current tables use primary keys and unique indexes only (no relations exist yet). Future application tables must add foreign keys, unique and check constraints, and optimistic version columns where they protect local data.
- A local single-user process does not need cross-tenant isolation or distributed concurrency coordination.
- UI and application services prevent conflicting local edits and concurrent generation jobs.
- Backups/restores operate on the local database and files and must be validated before replacement. Until the Phase 6 in-app backup exists, the README documents an interim stopped-app file copy.
