# ADR 0010: Optional SQLite encryption with SQLCipher

- Status: Decided in M3: **NO-GO for v1.0** (see "M3 decision" below)
- Decision gate: Phase 8 (M3)
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

## M3 decision (2026-10-11): NO-GO for v1.0
All six mandatory GO criteria were evaluated for the release on Windows 10 64-bit, 4 GB RAM school computers:
1. **Provider and licence:** the maintained candidates need a native SQLCipher build (SQLitePCLRaw.bundle_e_sqlcipher or a commercial build); licence terms and native packaging for the self-contained folder would need review. Not done: not a release-day risk worth taking.
2. **Key in an OS-protected store, not beside the database:** DPAPI could hold a random key, but the key would then be bound to one Windows user on one computer.
3. **Recovery and backup/restore without contradicting the one-time recovery code:** the school moves data between computers by backup and restore (the user guide, chapter 13). An encrypted database would make every backup unreadable on another computer unless the key travels with it, which defeats the protection or adds a second secret to lose. No design satisfies this today.
4. **Migration and crash-safe restore tests including wrong-key and corrupted-file behaviour:** none exist; the current restore, rollback and integrity-check tests are for plain SQLite.
5. **Measured startup, query and write cost on the target hardware:** not measured; the plain-SQLite figures are in `docs/PERFORMANCE.md`.
6. **Packaging tests on every supported target:** the installer is tested only for the unencrypted layout.
Since criteria 1–6 are not all met, the decision is NO-GO. The release keeps plain local SQLite protected by the Windows account of the user, loopback-only access and the owner password; **it does not claim encryption at rest**. The user guide recommends BitLocker or Windows device encryption on a shared computer and keeping backups on the school's own removable drive. A future GO needs its own ADR, threat model and migration plan.

