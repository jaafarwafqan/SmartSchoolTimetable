# DESIGN_SYSTEM.md

Single source of truth for how the Smart School Timetable looks and behaves.
Tokens live in `frontend/src/styles/tokens.css`. This document explains and constrains their use.
If this file and any other design source disagree (including generated design recommendations), this file wins.

---

## 1. Principles

1. **Work tool, not a marketing site.** Dense, calm, predictable screens. No hero sections, no landing-page patterns.
2. **Arabic first, RTL native.** Layout, icons, tables and numerals are designed for right-to-left.
3. **Flat and quiet.** No gradients, no decorative shadows. Only cards and floating layers have elevation.
4. **Clarity over decoration.** One primary action per screen area. State is never conveyed by color alone.
5. **Offline and local.** No CDN fonts, scripts or images. Everything is bundled.
6. **Consistent.** A new screen is assembled from existing components and tokens. If something is missing, add it to the system first.

---

## 2. Colors

Use semantic tokens only. Never write a hex value outside `tokens.css`.

| Role | Token | Value | Use |
|---|---|---|---|
| Page background | `canvas` | #F3F6FA | App background |
| Card / input | `surface` | #FFFFFF | Cards, dialogs, fields |
| Muted surface | `surface-muted` | #EEF2F7 | Table header, disabled field, zebra rows |
| Decorative line | `line` | #DFE6EF | Card border, dividers (never the only edge of a control) |
| Control border | `line-strong` | #7A8CA5 | Input, select, checkbox borders |
| Text | `ink` | #172B4D | Body and headings |
| Secondary text | `ink-muted` | #465B77 | Labels, descriptions |
| Hint text | `ink-subtle` | #5B6B82 | Captions, placeholders |
| Primary | `primary` / `primary-hover` | #1758A6 / #124888 | Main actions, links, focus |
| Primary soft | `primary-soft` + `primary-ink` | #E8F0FF + #17468D | Selected rows, icon tiles |
| Danger | `danger` / `danger-soft` | #B42318 / #FEF3F2 | Errors, delete, conflicts |
| Success | `success` / `success-soft` | #067647 / #ECFDF3 | Saved, valid |
| Warning | `warning` / `warning-soft` | #93370D / #FFFAEB | Shortages, cautions |
| Info | `info` / `info-soft` | #1758A6 / #E8F0FF | Neutral notices |
| Subjects | `subject-1` to `subject-10` + `on-subject` | pastel set | Timetable cells |
| Print (MF5) | `print-ink` / `print-header` / `print-paper` | #000000 / #e6e6e6 / #ffffff | Official printed timetable only: black text and rules, grey column headers, clear with background graphics off |

Rules:
- Text on any surface must reach **4.5:1**; large text and control borders **3:1**. A unit test enforces the token pairs.
- Status is always shown with **color + icon + text**, never color alone.
- Subjects pick a color from the 10-color palette only. If a free color is ever allowed, compute the text color automatically and reject combinations below 4.5:1.

---

## 3. Typography

- **UI font:** Noto Sans Arabic (variable), bundled locally via `@fontsource-variable/noto-sans-arabic`. Fallback: Tahoma, Segoe UI. Latin text and digits render with the same family.
- **Print / PDF font:** Noto Naskh Arabic (already validated in the PDF spike). Reports may differ from the UI font on purpose.
- Never import fonts from the internet. No serif display fonts for the UI.

| Style | Size | Weight | Line height | Use |
|---|---|---|---|---|
| Page title (h1) | 1.5-1.875rem | 700 | 1.35 | One per screen |
| Section title (h2) | 1.25rem | 700 | 1.35 | Cards, panels |
| Subsection (h3) | 1.125rem | 600 | 1.35 | Groups |
| Body | 1rem | 400 | 1.7 | Default |
| Label | 0.9375rem | 600 | 1.5 | Form labels |
| Caption | 0.8125rem | 400 | 1.5 | Hints, metadata |

- Minimum text size is 0.8125rem. Body text is never below 1rem on forms.
- Tables and timetable grids use `font-variant-numeric: tabular-nums`.
- Do not use `letter-spacing` on Arabic text. Do not use italics for Arabic.
- Emphasis uses weight, not underline (underline is reserved for links).

---

## 4. Spacing, layout, shape

- Base unit **4px** (Tailwind scale). Common gaps: 8, 12, 16, 24, 32.
- Radius: `sm` 6px (chips, cells), `md` 10px (controls), `lg` 16px (cards, dialogs).
- Page content max width 80rem; forms max width 34rem; dialogs 32-40rem.
- Use logical properties only: `margin-inline`, `padding-inline-start`, `inset-inline-end`, `text-align: start`. No `left/right`, `ml-/mr-/pl-/pr-`.
- Elevation: cards `shadow-card`; menus/popovers `shadow-popover`; everything else none.

---

## 5. Icons

- One library: **lucide-react**. No emoji, no other sets.
- Size 20px (16px inside dense tables, 24px in empty states), stroke width 2, `aria-hidden="true"`.
- Every action has icon + Arabic text. Icon-only buttons only for show/hide password, close, and row actions in dense tables, and they require Arabic `aria-label` and `title`.
- Directional icons (arrows, chevrons, back/forward) mirror in RTL. Non-directional icons (check, trash, search) do not.
- Status icons: success = CircleCheck, error = CircleAlert, warning = TriangleAlert, info = Info.

---

## 6. Components

All UI is built from `frontend/src/components/ui/*`. Raw `<button>`, `<input>`, `<select>`, `<table>` are not used in feature code.

### 6.1 Button
| Variant | Look | Use |
|---|---|---|
| primary | `primary` fill, `on-primary` text | The single main action in a region |
| secondary | `surface` fill, `line-strong` border, `ink` text | Alternative actions |
| ghost | transparent, `primary` text | Tertiary, toolbar |
| danger | `danger` fill, white text | Destructive, always behind a confirmation |

- Heights: sm 36px, md 40px (default), lg 48px. Touch screens: minimum 44px target.
- Always icon + label. Icon sits at the start side of the label (right in RTL), gap 8px.
- States: hover (darker fill or `surface-muted`), focus (3px primary ring, 2px offset), disabled (60% opacity, `not-allowed`, no hover), loading (spinner replaces icon, label stays, button disabled, `aria-busy`).
- Max one primary button per card or dialog footer. Order in dialogs: primary first (right in RTL), then secondary.

### 6.2 Form fields
- Label above the field (`label for`), required marked with text "(مطلوب)" not only an asterisk.
- Input height 40px, `surface` fill, `line-strong` border, radius `md`. Focus: primary border + ring. Error: `danger` border, icon, message below in `danger` text.
- Hint text below in `ink-subtle`. Placeholders are examples only, never the label.
- Password field: eye / eye-off icon button inside the field at the logical end, with `aria-pressed`.
- Validation is shown inline after submit or blur; focus moves to the first invalid field. Messages are Arabic (see localization rule).

### 6.3 Messages
- Inline alert: soft background + status color text + icon + `role="alert"` (errors) or `role="status"` (success/info). Never nest two alerts.
- Toasts only for transient confirmations ("تم الحفظ"), auto-dismiss 5 s, never for errors that need action.
- Confirmation dialogs for destructive actions: title, consequence sentence, danger button with verb ("حذف"), cancel button. Typing a confirmation word only for irreversible bulk actions.

### 6.4 Tables
- Header on `surface-muted`, sticky when scrolling, text `ink` weight 600.
- Rows 44px, hover `primary-soft` at low emphasis, selected `primary-soft`.
- Numeric columns use tabular numerals. Wide tables scroll inside their own container.
- Empty state: icon + sentence + primary action. Loading: skeleton rows, not a blocking spinner.
- Pagination or virtual scrolling above 100 rows.
- **Only the table container scrolls.** Grid and flex children that hold a table get `min-inline-size: 0`; the page never scrolls sideways at 375, 768, 1024 or 1440px (`expectNoPageScrollX`).
- **Sticky rules:** only `thead` cells stick to the top. A row-header column (`tbody th`) sticks to the inline start with an opaque background. Stacking: corner > header > first column > cells.
- **Headers wrap** to two lines rather than being cut (minimum column width). They may carry a small second line, such as a stage's weekly capacity.
- **Totals go in `<tfoot>`:** exactly one cell per column, aligned under it, at most two lines (value, then a status chip with icon + text). In a table that scrolls vertically, the totals row sticks to the bottom with an opaque background. Never change a table cell's `display` and never position text absolutely.
- **Numeric input cells are compact** (about 3rem × 2.25rem) so six or more columns fit.
- **No overlapping text.** Stacked Arabic lines need enough line height (about 1.6–1.8), because the glyph box is taller than a tight line box. `expectNoTextOverlap` checks the line boxes of every visible text in tables, cards and the dashboard.
- **Counted nouns use `arabicCount`** (`format.count(n, noun)`): «مادة واحدة»، «مادتان»، «٣ مواد»، «١١ مادة». Never write `${number} مادة` in a string; a unit test rejects it.
- **Gregorian dates show the Iraqi month names** (أيلول، تشرين الأول … حزيران). The numerals setting stays independent.

### 6.5 Timetable grid (core screen)
- Days as rows or columns per the school setting; periods labeled with time.
- A cell shows subject name (weight 600), teacher short name (caption). Background = subject color, text `on-subject`.
- **Conflict:** `danger` 2px outline + `CircleAlert` icon + tooltip text. **Blocked slot:** diagonal hatch pattern + `Ban` icon (not color alone). **Selected:** `primary` 3px outline. **Drag target valid:** dashed `success` outline; **invalid:** dashed `danger` outline.
- Every interaction available by keyboard: arrow keys move focus, Enter picks up/drops, Esc cancels. Screen readers get a text description per cell.
- Print stylesheet: white background, black text, subject colors as light fills, A4 landscape.

### 6.6 Navigation and layout
- Right-hand sidebar (RTL) with icon + label; collapsible to icons with tooltips. Top bar holds the school name, current academic year/term, and user menu (settings, lock, logout).
- On screens below 768px the navigation opens in the page flow under the top bar (no overlay, no side drawer: section 14 and ADR 0024), and the weekly grid becomes a day view.
- Six sidebar items. Phase 4 added «الجدول», with the tabs «التوليد» and «الجداول» (DECISIONS_PENDING #76). Screens of a group are tabs under the page title (route tabs, `aria-current="page"`). Breadcrumbs: dashboard › group › screen.
- Breadcrumbs for nested screens. The current page is marked with `aria-current="page"`.

---

## 7. Motion

150-200ms ease for hover, focus and open/close. No bouncing, parallax or looping animations. `prefers-reduced-motion` disables transitions (already in `tokens.css`).

---

## 8. RTL and numerals

- `<html lang="ar" dir="rtl">`.
- Numerals follow the school setting (Arabic-Indic or Western) through one formatting helper; never hard-code digits.
- Mixed text: wrap Latin identifiers (file names, codes) with `dir="ltr"` and `unicode-bidi: isolate`.
- Punctuation lives inside the Arabic string so it renders correctly.

---

## 9. Accessibility

- WCAG 2.1 AA minimum. Visible focus on every interactive element. Full keyboard operation.
- Touch targets at least 44px. Hit areas may exceed the visible icon.
- Every input has a programmatic label; every icon-only control has `aria-label` and `title`.
- Dialogs trap focus, close on Esc, and return focus to the trigger.
- Color is never the only carrier of meaning.

---

## 10. Responsive breakpoints

Test and design at 375, 768, 1024 and 1440 px. Layout may reflow but must never scroll horizontally except inside tables and the timetable grid.

---

## 11. Do and don't

| Do | Don't |
|---|---|
| Use tokens and `components/ui` | Write hex, `px` font sizes or inline styles |
| Icon + label on actions | Emoji, or unlabeled icon buttons |
| Soft surfaces for status | Saturated large color blocks |
| One primary button per area | Several competing primary buttons |
| Arabic error text from the dictionary | Any English user-facing text |
| Hatch + icon for blocked slots | Color-only states |
| Local bundled fonts | Google Fonts or any CDN |

---

## 12. Enforcement (automated, runs in CI/lint/tests)

1. **Stylelint:** `color-no-hex` everywhere except `tokens.css`; disallow physical properties (`margin-left`, `padding-right`, `float`, `left`, `right`) via logical-properties plugin; disallow `font-family` outside `tokens.css`; disallow `font-size` in `px`.
2. **ESLint:** forbid Tailwind arbitrary color classes (`bg-[#...]`, `text-[#...]`), inline `style` with colors, raw `<button>/<input>/<select>` in `features/**`, and icon imports from any package except `lucide-react`.
3. **Contrast unit test:** parses `tokens.css`, checks every documented text/background pair is at least 4.5:1 and `line-strong` on `surface` is at least 3:1. Fails the build on regressions.
4. **Style guide route** (`/design`, development only): renders every token, type style, button state, field state, alert, table, dialog and timetable cell state. Reviewed visually on each design change.
5. **Playwright accessibility check** on key screens (axe) and screenshot checks at 375/768/1024/1440 once the style guide exists.
6. **Review checklist** for every new screen: tokens only, existing components only, icon + label, RTL logical properties, Arabic text, keyboard path, empty/loading/error states.

---

## 13. Change policy

- Tokens and component specs change only through a commit that updates `tokens.css`, this file, the style guide route and `CHANGELOG.md` together.
- A new component type or a new color requires an entry in this document first.
- Generated design recommendations (design skills, templates) are inputs for discussion, never authority. Nothing from them is adopted unless written here.
- Dark theme and theme presets are deferred. When added, they override semantic tokens only and must pass the contrast test; components do not change.

---

## 14. Add and edit patterns

Records are added by choosing and by quick add, not by long forms. Each pattern has one component.

| Fields | Pattern | Component | Used for |
|---|---|---|---|
| 1–2 | **Inline row** in the list: type, then press Enter. The fields clear after a successful add, so the next item can be typed straight away. | `InlineAddForm` | quick add of subjects, teachers and calendar days; section count; curriculum cells |
| 3–6 | **Centred dialog**: 32–40rem wide, focus trapped, Esc closes, no inner scrolling at 1280×720. Never anchored to a screen edge (`margin: auto`; checked by E2E). | `Dialog` | academic year, term, generate periods, calendar day, shift |
| Long or optional details | **Quick add by name only**, then details edited **in place** on an expandable row. Advanced options are folded under «خيارات متقدمة». Never a long modal form. | `ExpandableRow` | teacher constraints, subject options |
| Many at once | **Bulk panel** inside the page, with a preview before saving. | page `Card` | teachers from pasted names |

- **No side drawers or sheets** anywhere. The ESLint rule `design-system/no-drawers` and the test `styles/noDrawers.test.ts` fail if one is introduced (ADR 0024).
- `/design` shows every pattern (section «أنماط الإضافة والتعديل»).

## 16. Scheduling and readiness

- The workload matrix is a scannable table: sections are rows, curriculum lines are columns, and each cell uses a native keyboard-operable teacher chooser. Unassigned cells show an icon and text, never color alone. Specialization filtering and «عرض الجميع» are visible in the cell interaction.
- Teacher load uses the shared `LoadBar` with its accessible meter value and `LoadStatusBadge` with icon + text. Assigned, limit and available numbers remain visible next to the bar; overload fills the meter without changing its dimensions.
- Readiness is an unframed report with a status banner, error/warning counts, a small LTR hash and check time, and findings grouped by entity. Each finding states measured numbers, explanation and actionable Arabic links. `error` blocks generation; `warning` does not. A group with a lesson shortage states it with a label («النقص: …»), never a bare number. The «فحص وضع الدروس المزدوجة» checkbox re-runs the check in that mode. The development-only `/design` guide demonstrates the matrix, load bar and a grouped finding (rendered by `SchedulingSection.test.tsx`).
- The suggester shows a preview (assignments, unassigned lines with a reason, loads before → after) and applies only after a centred confirmation. Inside the wizard its title is an `h3` under the step's `h2`.
- Keep cards for the report summary and individual entity groups only. Use logical CSS and shared `DataTable`, `Badge`, `Alert`, `LoadBar`, and icon + Arabic label controls. Validate at 375/768/1024/1440 px for axe, horizontal scrolling and text overlap.

## 15. Dates, times and left-to-right runs

- **`DateField`:** day / month / year segments, in that order.
  - It shows the school's numerals.
  - Typing accepts Arabic-Indic or Western digits; ArrowUp and ArrowDown change the focused segment.
  - The full Arabic date (Hijri when chosen) appears as the hint.
  - The value sent to the API is ISO `yyyy-MM-dd`.
- **`TimeField`:** 24-hour `HH:mm` (hours, then minutes; minutes step by 5 with the arrow keys). The time is kept left-to-right and uses the school's numerals.
- **No native date or time inputs:** native `<input type="date|time">` are forbidden (`design-system/no-native-date-time`), because they show browser-locale formats (mm/dd/yyyy, AM/PM).
- **`LtrText`** (and the `ltrRuns()` string helper for options and ARIA labels): years, times, dates, codes, file names and usernames keep their logical order in RTL. "2026 - 2027" must never be shown as "2027 - 2026". A Vitest unit test and an E2E position check enforce this.
- **Fixed UI text is Arabic only.** The dictionary test (`i18n/noLatinText.test.ts`) and the E2E check `expectNoLatinText` fail on Latin letters. Allowed exceptions: data (usernames) and the image format codes PNG, JPEG, WebP.

## 17. Suggestions (Phase 2.5)
- **«مقترح» marks a value that came from a template and was not edited since.**
  - A primary badge appears on panels.
  - A small mark under a table cell (`suggested-mark`).
  - The mark disappears on the owner's first edit.
- **Provenance banner:** a panel that applies suggested content starts with an info alert naming its source honestly. Never use «رسمي».
- **Review warning:** a warning icon plus the full Arabic sentence (`ReviewWarning`). In a table header, a short «يحتاج مراجعة» with the full sentence available to screen readers and as a tooltip. Text uses `ink`, the icon `warning`; never colour alone.
- **Preview before apply:**
  - A suggestion's apply button never changes anything the owner typed.
  - A destructive variant (a reset to the suggestion) shows before → after in a centred confirmation dialog.
- **Status per row:** a badge with icon and text, for example «اقتراح جديد»، «مطابق للاقتراح»، «معدّل يدوياً، لن يتغير». Rows that cannot be applied explain why and what to do.

## 16. Wizard pattern (Phase 2.5D)
- **Layout:** a step list (`nav` named «خطوات الإعداد», the current step marked with `aria-current="step"`, done steps with a check icon and hidden text) beside one card for the current step. The list sits above the card under 1024px.
- **Footer:** «السابق»، «حفظ والمتابعة» (saves the step)، optional «تخطي هذه الخطوة»، and the «إكمال لاحقاً» link back to the dashboard. Errors appear in the footer next to the step's buttons.
- **Focus:** moving to a step focuses its title (`tabIndex=-1`).
- **Choose, don't type:** choice cards, selects, steppers and previews. Proposed values carry the hint «قيمة مقترحة، يمكنك تعديلها».
- **Reuse:** steps reuse the screens' components (template panels, stage cards, the curriculum table, bulk add), shown open in place. Never a drawer.
