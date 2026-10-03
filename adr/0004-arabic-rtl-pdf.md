# ADR 0004: Arabic-first RTL rendering requirements

- Status: Accepted
- Date: 2026-10-03

## Context
The platform is Arabic-first, and timetable output must be readable, printable, and culturally correct in Arabic presentation.

## Decision
Generate PDF and print layouts using Arabic-aware shaping and RTL table direction. Use Cairo or Noto Naskh Arabic, and verify glyph joining, table mirroring, and printed layout with a visual spike.

## Consequences
- Requires explicit validation before production release.
- Offline printing remains `window.print()`; full PDF export stays online-only.
- Must test rendering in both Arabic and English contexts.
