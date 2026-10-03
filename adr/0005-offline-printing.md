# ADR 0005: Offline timetable printing

- Status: Accepted
- Date: 2026-10-03

## Context
Users must be able to print timetables when disconnected, while offline functionality must not add a client-side PDF dependency.

## Decision
Browser printing uses `window.print()` with dedicated RTL-aware A4 print CSS. Branded PDF generation uses local QuestPDF and is available without internet access.

## Consequences
- Browser print uses the browser print dialog and the browser/device's available fonts and printer settings.
- The page must preserve Arabic direction, logical layout, and readable page breaks.
- Local QuestPDF is the controlled option for branded documents and consistent Arabic font embedding.
- Do not add a client-side PDF library.
