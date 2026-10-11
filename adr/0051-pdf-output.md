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

## Printing decisions (M2, step 4)
1. **Colour:** `print-color-adjust: exact` (and the `-webkit-` form) is set on the whole print document, so subject colours and the stage tints print even when the browser's «Background graphics» is off (checked with `page.pdf({ printBackground: false })`). The print option «ملوّن / أبيض وأسود» (default colour; kept when the job or view changes; not stored in the preferences) switches to no fills, bold subject names, black borders and the stage header rows.
2. **Stages in a multi-section print:** the whole-school grid groups its rows by stage: a full-width header row with the stage name (text, so it reads in black and white), a thick rule between stages, and a light tint (six tokens `--color-print-stage-*`) on the first column in colour mode. The grid is the print-only primitive `components/ui/print-master-grid`.
3. **Readable sizes:** the print base is 10 pt; subjects never go below 8 pt (fit scale floor 0.8) and teacher names never below 7 pt (`max(7pt, 0.85em)`). What does not fit at those sizes is split, never shrunk further: `planDays` puts all days on one page when they fit, otherwise the fewest pages at 8 pt and then the largest scale that keeps that page count (for example A3 landscape, 5 days × 7 lessons: pages of 3 and 2 days). Every page repeats the column headers, the official header (with the days it carries) and the signature footer; the page number «صفحة X من Y» is the page margin box.
4. **Scope «صفحة لكل مرحلة»** prints each stage on its own page (split by days when needed); the stage is named in the page title.
5. **Official header and footer** (logo, school, title, year/semester/version, principal, signature, stamp, date) stay the program's own; the user guide and the panel hint say the browser's «Headers and footers» option must be off.
6. Verified by `m2-printing.spec.ts` (computed print styles, page counts from the PDF, font floors, stage rows and bands) and by rendering every page of the saved PDFs (`tools/render_pdf.py`, A4 landscape section, A4 portrait teacher, A3 landscape whole school and per stage, colour and black and white) and looking at them.

