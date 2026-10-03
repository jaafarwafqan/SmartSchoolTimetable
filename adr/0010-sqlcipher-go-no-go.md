# ADR 0010: Optional SQLite encryption with SQLCipher

- Status: Deferred; NO-GO for the initial release
- Decision gate: Phase 8
- Date: 2026-10-03

## Context
The local application stores school timetable data in SQLite. Baseline SQLite does not encrypt the database at rest. SQLCipher could add encryption, but it introduces a native dependency and requires a durable, recoverable key-management design.

## Decision
Do not require or claim SQLCipher encryption for the initial release. In Phase 8, evaluate SQLCipher and record an explicit GO or NO-GO before adding it.

The Phase 8 GO criteria are all mandatory:
- a vetted maintained provider/package with documented license and native platform support;
- a key stored using an OS-protected secret facility, never beside the database or in source/configuration;
- a user-understandable recovery and backup/restore design that does not contradict the one-time recovery-code policy;
- migration and crash-safe restore tests, including wrong-key and corrupted-file behavior;
- measured startup/query/write performance on supported hardware;
- packaging tests on every supported target and a documented update/rollback procedure.

If any criterion fails, record NO-GO and retain unencrypted local SQLite with OS file permissions and full-disk encryption recommended. Do not add an unreviewed encryption dependency or create a false claim of protection.

## Consequences
- Initial release remains vulnerable to offline inspection of local database files by someone with OS-level file access.
- Password hashing does not encrypt timetable data at rest.
- A later GO is a material security and storage change and requires updated threat modeling, ADR approval, migration testing, and user-facing recovery documentation.
