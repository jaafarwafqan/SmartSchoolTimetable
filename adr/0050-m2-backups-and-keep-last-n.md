# ADR 0050: M2 — preferences, backups, safe upgrade, and the keep-last-N exception

- Status: Accepted (M2, branch `work/phase-5`; this ADR is extended as each M2 part lands)
- Date: 2026-10-11

## Keep-last-N: the single approved exception to «never delete backups» (owner-approved, step 2)
The owner's rule «never delete backups» stays in force with exactly one exception, with these safeguards, all enforced in `BackupService.PruneAutomaticAsync` and covered by `BackupManagementTests`:
1. **Scope.** Only files named exactly `auto-backup-yyyyMMdd-HHmmss.db`, made by the app's scheduled backup, directly in the configured backup folder (no subfolders, no links/reparse points). Never `timetable-backup-*` (manual), `pre-restore-*`, `pre-migration-*`, `pre-upgrade-*`, `pre-conversion-*`, or any other file, even if its name starts with `auto-backup-`.
2. **N.** Default 14; choices 3, 7, 14, 30. The minimum is 3 and the screen offers nothing lower; the former «keep all» (0) is no longer accepted, and a stored 0 (older rows; the check constraint is not changed, no migration) is applied as 3 (`EffectiveKeepLast`).
3. **Integrity first.** Every candidate is opened and passes `PRAGMA integrity_check` before it counts. Damaged or unreadable copies are never deleted and never counted as backups.
4. **Delete nothing short of N.** When no more than N valid automatic backups exist, nothing is deleted. Otherwise only the oldest valid ones beyond the newest N go, so the newest valid backup is always kept.
5. **Audited.** One `AutoBackupPruned` history entry per deleted file (its file name, never a path), plus a developer-log line with the count.
6. The pruning runs only inside the automatic backup, after the new copy was written and verified; it never runs on a manual backup, a restore or an upgrade.
CLAUDE.md records this as the single approved exception.

## Safe upgrade (step 3b)
1. `DatabaseUpgrader.MigrateSafelyAsync` replaces the plain `MigrateAsync` at start-up. A database that already holds data and has pending migrations first gets a safety copy `backups\pre-upgrade-yyyyMMdd-HHmmss.db` (SQLite online backup, then `PRAGMA integrity_check` on the copy; a name that exists is never reused). A new or current database needs no copy.
2. If the copy cannot be made or fails the check, nothing is migrated (`BackupFailed`). If a migration fails, the copy is put back with the online backup API and the program stops (`MigrationFailedRestored`; if even that fails, `MigrationFailedNotRestored` names the kept copy). A database written by a newer program is refused untouched (`DatabaseNewerThanProgram`).
3. The data and the program disagree after a failure, so no screen can be served: the Arabic message goes to the console and to `startup-error.txt` in the data folder (for the M5 launcher), and the process exits with code 2. The API-codes rule applies to HTTP responses; this is the pre-HTTP channel.
4. `pre-upgrade-*` copies are never pruned and are listed as «تلقائية قبل ترقية البرنامج». Guide: `docs/UPGRADE_AR.md`. Tests: `SafeUpgradeTests`.
