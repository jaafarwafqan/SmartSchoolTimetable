# ADR 0017: Time zone, numerals and calendar display

- Status: Accepted (Phase 2 specification 1 and 2.1; taken autonomously under protocol 0.5)
- Date: 2026-10-03

## Context
The school profile chooses:
- a time zone (default Asia/Baghdad);
- a numeral system (Arabic-Indic or Western digits);
- a calendar display (Gregorian or Hijri).

Every number, date and time on screen must follow these choices. The specification asks for exactly one formatting helper. The app runs offline on Windows in a browser.

## Decision
- **Storage:** times of day are stored as `TimeOnly` (`HH:mm`) and dates as `DateOnly` (`yyyy-MM-dd`), with no time zone. Instants (audit `OccurredAt`, `UpdatedAt`) are stored as UTC `DateTimeOffset`. The API exchanges only these invariant formats; it never sends formatted text.
- **Formatting:** one frontend helper, `createFormatter(prefs)` in `frontend/src/lib/format.ts`, wrapped by `useFormatter()`. It reads the preferences from `GET /school-context` and uses the browser's built-in `Intl` API:
  - locale `ar` with the numbering system `arab` or `latn`;
  - calendar `gregory` or `islamic-umalqura`.

  Stored calendar dates and times of day are formatted as-is (in UTC, so they never shift by a day). The school's time zone decides what "today" and "now" are: `todayIn(timeZone)` uses the school's zone, not the computer's.
- **Time zones:** chosen from a fixed list of IANA identifiers (`SchoolProfile.SupportedTimeZones`), so an invalid ID cannot be stored. See `docs/DECISIONS_PENDING.md` item 7.
- **Defaults:** before the owner saves the profile, the defaults are Arabic-Indic digits and the Gregorian calendar (item 8).
- **Bidi:** user-entered values embedded in Arabic sentences are wrapped with FSI/PDI (`i18n/isolate.ts`). This keeps values such as "2026-2027" from being reordered.
- **Dependencies:** none were added. `Intl` ships with the browser and works offline.

## Alternatives considered
- **A date library** (date-fns-tz, Luxon, Day.js): an extra dependency for what `Intl` already does offline.
- **Server-side formatting:** this violates the "codes and data only" API contract and would duplicate locale logic.
- **Free-text time zone:** error-prone, and some IDs may be unavailable in the browser's ICU data.

## Consequences
- Native `<input type="date">` and `<input type="time">` controls still display in the browser's locale. This is a known limitation, reported in the Phase 2 report. Values shown outside inputs always follow the school setting.
- Hijri display is a display choice only. All stored dates and all calculations are Gregorian.
- How to change: extend `createFormatter`. Every screen goes through it, and Vitest covers its output (`lib/format.test.ts`).
