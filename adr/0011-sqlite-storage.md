# ADR 0011: Local SQLite storage

- Status: Accepted for the local-only product
- Date: 2026-10-03

## Context
The application runs for one owner on one workstation, with no network service or remote database. It needs reliable local persistence and backup/restore. Keeping provider details out of Application and Domain should avoid preventing a future, separately approved PostgreSQL deployment.

## Decision
- Use EF Core SQLite as the initial and default persistence provider.
- Keep one canonical database file per installation. Enable SQLite WAL mode for normal operation and concurrency between the local application processes that may read/write the database.
- WAL mode may create transient `-wal` and `-shm` sidecars while the database is open; the supported backup artifact is a consistent single `.db` file, not a casual copy of the active database and sidecars.
- For online backup, use SQLite's Online Backup API (or the provider-supported equivalent) to produce a consistent standalone database file. Alternatively, stop/quiesce all database users, checkpoint/truncate the WAL, close connections cleanly, copy the main database file, then validate the copy by opening it and running integrity checks. Never copy only the live main `.db` file while writes may be in progress.
- Define persistence through repository/unit-of-work abstractions and provider-neutral Application/Domain contracts. Keep SQLite connection APIs, SQL dialect, and provider-specific types inside Infrastructure. Use EF Core migrations and provider-specific configuration in Infrastructure.
- Preserve a documented provider migration path, but do not implement PostgreSQL now. A future switch requires an approved ADR, provider-specific migrations, compatibility review, backup/import plan, and integration tests.

## Consequences
- A local one-file backup is portable and simple to store, while WAL provides appropriate local write/read behavior.
- Backups require a safe SQLite-aware procedure; file-copy semantics must be tested during backup/restore implementation.
- The repository abstraction keeps PostgreSQL possible but does not guarantee schema/query portability; provider compatibility must be tested rather than assumed.
- The canonical file may be accompanied by transient WAL sidecars during runtime. Users must use the in-app backup operation instead of copying an active database file.
- Docker, PostgreSQL, and Redis are not required by the local application or its Phase 1 build.
