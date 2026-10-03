# ADR 0005: Offline timetable printing

- Status: Accepted
- Date: 2026-10-03

## Context
Users must be able to print timetables when disconnected, while offline functionality must not add a client-side PDF dependency.

## Decision
Offline printing uses the browser's `window.print()` with dedicated RTL-aware A4 print CSS. Full PDF generation remains online-only and is produced server-side with QuestPDF.

## Consequences
- Offline print uses the browser print dialog and the browser/device's available fonts and printer settings.
- The offline page must preserve Arabic direction, logical layout, and readable page breaks.
- Server-side PDF remains the controlled option for branded documents and consistent Arabic font embedding.
- Do not add a client-side PDF library.
