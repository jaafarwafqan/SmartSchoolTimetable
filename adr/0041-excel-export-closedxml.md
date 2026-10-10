# ADR 0041: Excel export with ClosedXML; printing through CSS

- Status: Accepted (Phase 4 M5)
- Date: 2026-10-09

## Context
The owner needs to print timetables and hand them out as files. PHASE_4_MVP_PROMPT asks for:
- printing through a print stylesheet, with no new library;
- an Excel export with ClosedXML;
- no server-side PDF for now (QuestPDF is deferred by its licence decision, ADR 0004); the owner saves a PDF from the browser's print dialog.

## Decision
- **Excel:** `ClosedXML` 0.105.1 (licence MIT; managed only, no native files), in Infrastructure behind `ITimetableExporter`.
  - Sheets: one master sheet «الجدول العام», one per section and one per teacher.
  - Every sheet is right to left, with Arabic headers and the school name, year and version on top.
  - Subject cells get light fills from the design palette. Page setup is A4, landscape for the master and portrait for the others.
  - Endpoint: `GET /api/v1/timetables/{id}/export.xlsx`.
- **Printing:** `@media print` in `styles/timetable.css`, active only on the timetable page (`.timetable-print-root`).
  - It hides the shell and the controls, shows a print header (school, year, term, version, view), and keeps the subject colours (`print-color-adjust: exact`).
  - Named pages set the paper: `timetable-landscape` (A4 landscape) for the master view and `timetable-portrait` for a section or a teacher.

## Alternatives
- EPPlus: its licence is not free for commercial use. Rejected.
- Writing OpenXML by hand: more code to maintain for no benefit. Rejected.
- A server PDF with QuestPDF: deferred (licence decision).

## Consequences
- The publish folder grows by the ClosedXML assemblies and their managed dependencies (DocumentFormat.OpenXml).
- Exported workbooks are snapshots of a version. They do not change when the school data changes.
