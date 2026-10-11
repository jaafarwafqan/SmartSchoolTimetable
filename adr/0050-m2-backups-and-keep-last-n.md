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

## Backup settings and list (step 3a)
`GET/PUT /backup/settings` (folder, automatic backup on/off, keep-last-N, versioned) are edited in «النسخ الاحتياطي والاستعادة» with the folder picker and a Save button. The restore list shows, per file: date, kind (manual, automatic, before an upgrade, before a restore, before a conversion), size, the program version and database version that made it, and the integrity result («سليمة وصالحة للاستعادة» / «تالفة» / «ليست قاعدة بيانات للبرنامج»).

## «حول البرنامج» and the support log (steps 3c, 3e)
- Settings › «حول» (and a link in the user menu) shows the name, version, build date (stamped at build time), database version (migration count), the licence notice from `LICENSE` (embedded; the Arabic part is displayed), the developer, the data folder, and the third-party licences. The version is also in the footer of every screen.
- «فتح مجلد البيانات» is a POST (launch token) that takes no path: the server opens only the folder the database lives in, with `explorer.exe` and an argument list (no shell). Tests replace the opener, so no test ever launches Explorer.
- «تصدير سجل الدعم» downloads a plain-text report: versions, environment, database size, per-table row counts (numbers only; the owner table is omitted), backup settings, and the last 300 log lines of this run (kept in memory, never written to disk). The Windows user name, machine name and `C:\Users\<name>` paths are redacted (a profile folder name may contain spaces, so redaction runs to the next separator). No password, code, cookie or token is ever in the data it reads.

## Excel import of teachers (step 3d)
- Template (`GET /teachers/import/template`): an RTL workbook with Arabic headers (full name, short name, specializations, daily and weekly limits, notes) and an instructions sheet; it contains no sample data.
- Dry run (`POST /teachers/import/preview`) and apply (`POST /teachers/import`) take the same upload (2 MB, 500 rows maximum); apply re-reads the file, so the preview cannot differ from the result. Each row ends as «ready», «exists» or «error» with coded issues (stable codes; the screen maps each to an Arabic sentence with its parameter): missing or too long name, duplicate in the file, short name too long or taken, limit not a number, day limit above week limit, notes too long, a constraint the current schedule rejects; warnings for an unknown subject (ignored); a proposed short name is information only.
- **Names are matched after Arabic normalisation**: alef forms (أ إ آ ا), ya and alef maqsura (ى ي), ta marbuta and ha (ة ه), diacritics and tatweel, and runs of spaces. The ta-marbuta fold applies only to import matching (`TeacherImportService.MatchKey`); stored values and the unique index are unchanged (no migration).
- **Never overwritten, idempotent:** a teacher who already exists is shown as «exists» and left exactly as it is; importing the same file again creates nothing. Only valid rows are added, in one transaction, with one `ImportApplied` audit entry.
- The workbook reader (ClosedXML, Infrastructure behind `ITeacherSheet`) treats any parser failure on a damaged file as «not a usable workbook» (`IMPORT_FILE_INVALID`), never a server error.

## Stored preferences (step 3f)
Already delivered in M2 (1/6): theme (system/light/dark), digits, default semester, print defaults per job (paper, orientation, fit to page), and the auto-lock time (Settings › عام). No further work was needed for this step.

