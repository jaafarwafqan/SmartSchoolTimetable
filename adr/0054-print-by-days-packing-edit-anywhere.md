# ADR 0054: The school printed by days, packed batches, a page with no browser margin, and editing from any view

- Status: Accepted (owner testing feedback for 1.0.0, branch `work/phase-5`)
- Date: 2026-10-16

## Decisions
1. **The whole school is printed BY DAYS.**
   - Layout: rows are days × lessons. The day label spans its lessons, and each row shows the lesson number with its 12-hour time. Columns are the sections grouped by stage, under a stage header row. Thick rules separate stages and days.
   - Primitive: `components/ui/print-days-grid.tsx`; feature component `PrintMasterGrid`.
   - Planner: `planSchoolByDays` (in `printing.ts`) aims for **one** page at a readable size (subjects 8 pt or more, teacher names 7 pt or more), in the orientation that gives the larger size when the orientation is «تلقائي» (the new default of the school jobs).
   - When the school truly cannot fit, it takes the fewest pages of two ways. When the grid is too tall (rows are days × lessons, whatever the sections), every stage stays on each page and the days are split. When it is too wide, there is a page per stage. Headers repeat on every page.
   - Colour and black-and-white, the logo, header, footer and signature lines are unchanged.
2. **Deterministic rows.**
   - Rule: a printed lesson cell is exactly three lines — the subject on at most two (clamped), the teacher or section on one (ellipsis). Line height is 1.1 and padding 0.15 em. The lesson-number header holds the number and at most two lines of time.
   - Why: with fixed heights the planner's arithmetic holds for any school data. The constants in `printing.ts` were measured in Chromium print layout: a row is 13 mm at full size; the page header is 30 + 3 mm and the footer 22 mm.
   - Consequence: a 12-section school with 6 lessons a day (30 a week) fits one A3 portrait page; with 7 lessons a day it needs two pages (the days split).
3. **Batches packed** (`planPacking`): «عدد المعلمين في الصفحة» / «عدد الشعب في الصفحة» offers «تلقائي» / 1 / 2 / 4 / 6. «تلقائي» takes the most blocks that fit at a readable size, then the largest size for that number. A fixed number that cannot be read at 8 pt is lowered (`reduced`). Each block holds the name, the weekly load (or the section's weekly lessons) and the full grid with times, and never splits over two pages.
4. **No browser margin.** `@page { size; margin: 0 }`, so Chromium has no room to draw its own date, title and address. Every `.print-page` is exactly one piece of paper (padding 10 mm, height a hair under the paper). The document numbers its own pages («صفحة ١ من ٢») in the footer, which replaces the margin-box counter. The print panel tells the owner to turn off «الرؤوس والتذييلات» if anything still appears.
5. **On screen.**
   - The school view and the new **stage** view («حسب المرحلة») offer «عرض: الأيام عمودياً» (the default) or «الأيام أفقياً».
   - `TimetableGrid` gained row groups (a day label spanning its rows) and column groups (a thick rule before a stage).
6. **Manual editing from any view** — section, teacher, stage or the whole school (`TimetableEditor` with `EditView`).
   - One history spans the views: switching the view keeps the edits.
   - A move stays inside the lesson's own section. In the teacher view, the chosen lesson moves to the chosen day and lesson in its section and swaps with what is there; the section's timetable changes with it.
   - Every step goes through the same independent check, with the same Arabic reasons. Unchanged: save as a new version, keep or discard before regenerating, the audit and the stale check.
   - There is no per-lesson "locked" state in the data model (locks exist only inside «إبقاء تعديلاتي» regeneration), so the editor has no locked reason to show.
7. **«حول البرنامج»** moved to the sidebar, directly below «الإعدادات» (`/about`). `/settings/about` redirects, the user menu keeps its link, and the settings tab is removed.

## Consequences
- The print planner depends on the print stylesheet's clamped rows; changing their line height or padding requires re-measuring the constants (the e2e checks that no page overflows and that the PDF has exactly the planned pages).
- A long subject name can be cut after two lines in print (it is readable on screen and in Excel).
