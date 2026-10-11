# ADR 0051: PDF output — the browser prints timetables; Chromium builds the user guide

- Status: Accepted (M2 documentation, extended by the printing step and the user-guide step of `work/phase-5`)
- Date: 2026-10-11

## Context
The owner prints timetables on real paper and also wants PDF files (a timetable to send, the Arabic user guide). The product is a local, offline, Arabic RTL application. A server-side PDF library would add a dependency and a licence question (QuestPDF was deferred by ADR 0004, and ADR 0041 kept printing in the browser), and Arabic shaping and bidirectional layout are exactly what a browser already does best.

## Decision
1. **Timetables are printed by the browser's print engine** (`@media print` and named pages, ADR 0041 and ADR 0048). «Save as PDF» in the print dialog is the PDF export. The program ships no PDF library and makes no PDF on the server.
2. **Colours must survive the print dialog.** The print document sets `print-color-adjust: exact` (and `-webkit-print-color-adjust: exact`) so subject colours print even when the browser's «Background graphics» option is off. A «ملوّن / أبيض وأسود» choice (default colour) switches to borders and bold subject names for a black-and-white printer.
3. **The program's own footer and header are the official ones** (logo, school, title, year/semester/version, principal, signature, stamp, date, page numbers). The browser's «Headers and footers» option must be off; the user guide says so.
4. **The Arabic user guide is built by the repository's own pipeline** (`tools/Build-UserGuide.ps1`): an HTML source with an embedded local Arabic font and no network access, rendered to PDF with Chromium's `page.pdf` through Playwright (already a development dependency). Both the HTML source and the PDF are committed (`docs/`); the installer ships the PDF.
5. **Printing is verified with `page.pdf`** in the test run (A4 landscape section, A4 portrait teacher, A3 landscape whole school, colour and black and white); every page is rendered to an image and viewed by a person before a change is accepted.

## Consequences
- No new dependency; the PDF quality equals the browser's. A user who prints with a different browser gets that browser's behaviour for margins and headers, which the guide documents.
- Server-side PDF stays deferred; it can be reconsidered only through a new ADR (licence and size review first).
