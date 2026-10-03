# Offline and Local Data

## Local-only operation
The application is designed to operate entirely on the local machine. Timetable data, settings, exports, and audit history are stored locally. There is no remote server, cross-device sync, account sync, outbox-to-server flow, telemetry, or internet requirement for core operation.

## Offline behavior
- Once installed, core workflows must work without internet connectivity.
- Local web UI and API communicate over loopback only; do not confuse localhost requests with remote network access.
- Timetable generation, PDF/Excel exports, printing, backups, restore, and imports operate locally.
- Offline printing uses `window.print()` and dedicated RTL-aware A4 print CSS. PDF output is generated locally by the application; neither requires a remote service.

## Local browser storage
The authoritative database is local SQLite. Browser storage, if used for cached UI state, is a convenience cache only and must not become a second source of truth. Clear sensitive cached state on logout/auto-lock where practical. Baseline SQLite is not encrypted; see the SQLCipher final-phase decision in `SECURITY.md` and ADR 0010.

## Local conflicts and backups
- The single owner session remains authoritative. Use local entity versions to detect stale edits between tabs or background operations.
- Never silently overwrite a newer timetable version.
- Backups are explicit local files and must be validated before restore. A backup is not a password-recovery path; recovery code remains the only password reset mechanism.
