# ADR 0042: Backup, restore and the ready-to-run folder

- Status: Accepted (Phase 4 M6)
- Date: 2026-10-09

## Context
The school computer needs:
- a way to keep its data safe;
- a way to return to an earlier copy;
- a folder that runs the program without a development setup.

The owner rules say no backup is ever deleted and no file is deleted without confirmation.

## Decision
- **Backup** (`POST /api/v1/backup`):
  - The owner types a full folder path. The default suggestion is `Documents\SmartSchoolTimetable\Backups`.
  - The app writes a NEW file `timetable-backup-<date-time>.db` with `VACUUM INTO`, which gives a consistent copy while the app runs.
  - An existing file is never overwritten (`BACKUP_FILE_EXISTS`).
- **Restore** (`POST /api/v1/backup/restore`):
  1. Two confirmations are required: the checkbox and the dialog (`confirm`, `confirmReplace`).
  2. The file must be an SQLite database of this app with an owner account (`RESTORE_FILE_INVALID`).
  3. Every migration in the file must be known to this build (`RESTORE_INCOMPATIBLE`). An older file is upgraded after the copy.
  4. No generation may be active.
  5. An automatic backup of the current data goes to `backups\pre-restore-<date-time>.db` next to the database.
  6. SQLite's online backup API copies the chosen file into the live database. No file is deleted or moved.
  7. Migrations are applied, WAL is turned back on, and every session is signed out.
- **Release folder** (`tools/Publish-Release.ps1`):
  - `dotnet publish` produces a self-contained win-x64 build with the frontend in `wwwroot`.
  - The script writes it to a new timestamped folder under `artifacts/release/` (ignored by git) and refuses an existing folder.
  - It adds `تشغيل البرنامج.bat`, which starts the server on 127.0.0.1:5080 and opens the browser, and `اقرأني.txt` with Arabic instructions.
- **Verification:**
  - Playwright can run the published `.exe` (`SST_RELEASE_EXE`) on temporary databases.
  - The batch file itself is not run by tests, because it uses the real database location.

## Consequences
- Backups and automatic restore copies accumulate. The owner removes old ones by hand when desired.
- The release folder is about 196 MB (self-contained .NET and the OR-Tools native libraries).
