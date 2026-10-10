# ADR 0049: Iraqi holidays template, one «إضافة يوم» entry, and the backup folder picker

- Status: Accepted (MF8, MF10, branch `work/phase-5`)
- Date: 2026-10-10

## MF8 — calendar
1. **A template, not hard-coded days.** `IraqHolidayTemplate` lists the holidays (Gregorian fixed; Hijri converted with .NET's `UmAlQuraCalendar`, supported 1318–1500 AH). Only holidays that summaries of Official Holidays Law No. 12 of 2024 list, plus Christmas (cabinet 2018), are included; disputed ones are excluded. The list needs the owner's review: `docs/IRAQ_HOLIDAYS.md`, DECISIONS #87.
2. **Per academic year, previewed, then confirmed** (`GET/POST /calendar-days/iraq-holidays`). Entries are ordinary `CalendarDay` rows with `Source`, `TemplateKey`, `IsApproximate` (a Hijri calculation: «تاريخ تقريبي — تحقق من الإعلان الرسمي», cleared when the owner edits the date) and `IsEnabled` (a disabled entry stays in the list but counts for nothing). Re-suggesting adds only what is missing; an entry moved by up to 30 days or disabled counts as present; a deleted one comes back.
3. **One «إضافة يوم» entry point** (the inline form and the empty-state button are gone). Date fields can show an inline month calendar (`DatePicker`, opt-in on `DateField`) so a date is chosen, not typed.
4. Each kind has its own icon, colour and start border in the list and the month view; the dashboard shows the next enabled holiday.
5. Corrective migration `Phase5CalendarHolidays` (additive columns with defaults).

## MF10 — backups
1. **A server-side folder picker** (`GET /backup/folders`): subfolders only, starting from documents, desktop and the drives, hidden/system folders skipped. A typed path is a collapsed fallback.
2. **Restore is chosen from a list** (`GET /backup/files`): the database files of the chosen folder plus the automatic backups next to the database (`pre-restore-*`, `pre-conversion-*`), newest first, each marked restorable (readable, owner present, only migrations this build knows) or not. The checkbox and the confirmation dialog stay; a restore still takes an automatic backup first. No file is deleted or overwritten.
3. The tests use folders under the test's own output directory, never the owner's Documents or database.
