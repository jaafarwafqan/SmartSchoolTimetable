# ADR 0048: Official printing

- Status: Accepted (MF5, branch `work/phase-5`)
- Date: 2026-10-10

## Context
The old print stylesheet printed the screen: wide grids were cut, there was no logo, signature lines or page numbers, and one job could not cover every section or teacher.

## Decisions
1. **A dedicated print document** (`PrintDocument`) is rendered through a React portal beside the app root, only while the print options panel is open or the browser is printing (`beforeprint`). In print the app is hidden (`body > :not(.print-document)`), so the paper is exactly the document. Each page: logo (when uploaded), school, «جدول الدروس الأسبوعي», year, semester, version, the section or teacher; the grid; a footer with the principal's name, «التوقيع» and «الختم» lines and the print date; the page number «صفحة ١ من ٥» in the bottom margin.
2. **Jobs and defaults**: current view, all sections (one per page), all teachers (one per page), whole school. Section → A4 landscape, teacher → A4 portrait, whole school → A3 landscape; the owner can change paper and orientation.
3. **Never cut**: «ملاءمة الصفحة» sets `--print-scale` from the page width and the number of columns (about 20 mm a column, never below 0.45); rows never split and column headers repeat.
4. **Clear without background graphics**: black rules, light-grey headers from print tokens (`--color-print-*`, in `tokens.css` and `DESIGN_SYSTEM.md`), plain black text.
5. **`@page` through a constructed stylesheet** (`adoptedStyleSheets`): the app's Content-Security-Policy (`default-src 'self'`) blocks inline `<style>` elements and the policy is not relaxed. Page numbers use the school's numerals (`counter(page, arabic-indic)`).
6. **Excel** gets the same header fields (semester, «جدول الدروس الأسبوعي», logo) and a footer with the principal, signature and stamp lines, the date and page numbers.

## Verification
`frontend/e2e/mf-printing.spec.ts` prints A3 landscape (school), A4 landscape (sections) and A4 portrait (teachers and sections) with `page.pdf` and checks page counts and paper sizes; every page was rendered to PNG and read. Backend tests read the Excel header and footer.
