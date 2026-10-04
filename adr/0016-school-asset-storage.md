# ADR 0016: School logo and stamp storage

- Status: Accepted (Phase 2 specification 2.1; taken autonomously under protocol 0.5)
- Date: 2026-10-03

## Context
The school profile has an optional logo and stamp. Later, printed timetables and reports will use them. The specification requires:
- PNG, JPEG or WebP up to 2 MB, verified by magic bytes;
- no SVG;
- storage under the app data folder;
- serving only to the authenticated owner, with `nosniff`.

## Decision
- **Where:** files are stored in `<database folder>/assets/`, by default `%LOCALAPPDATA%\SmartSchoolTimetable\assets\`. They are not stored as BLOBs in SQLite. The `SchoolProfile` row stores only the generated file name and the detected content type.
- **Names:** `{logo|stamp}-{random 128-bit hex}.{png|jpg|webp}`. The uploaded file name is ignored. `FileAssetStore` accepts only names that match `^(logo|stamp)-[0-9a-f]{32}\.(png|jpg|webp)$`, which makes path traversal impossible.
- **Validation:** `ImageSignature` (Application) detects the type from the first bytes. The declared type, if any, must agree with it.
- **Order of operations:**
  1. Write the new file.
  2. Save the database row (version-checked).
  3. Delete the previous file.

  If the save fails, the new file is deleted. A crash between steps 2 and 3 can leave an orphan file, but never a dangling reference.
- **Serving:** files go only to the owner session, with `X-Content-Type-Options: nosniff`, `Content-Security-Policy: default-src 'none'; sandbox` and `Cache-Control: no-store`.
- **Dependencies:** none were added.

## Alternatives considered
- **BLOB in SQLite:** atomic with the row, but it bloats the database and every backup, and needs streaming code. Rejected for 2 MB images that rarely change.
- **Keeping the original file name:** a path-injection risk, and the name leaks into logs. Rejected.
- **Sanitising and re-encoding the image:** would need an image library (a new dependency). Magic bytes plus a strict type allow-list, a sandbox CSP and `nosniff` are enough for owner-uploaded images displayed in an `<img>`.

## Consequences
- Backups must copy the `assets` folder together with the database. The interim README backup command documents this. The Phase 6 backup must include it.
- Orphaned files from a crash are harmless. A future maintenance task may sweep names that no row references.
- How to change: move the storage behind the existing `IAssetStore` port; no Application change is needed.
