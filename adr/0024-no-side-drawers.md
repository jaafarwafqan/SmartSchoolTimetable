# ADR 0024: No side drawers; four add patterns

- Status: Accepted (Phase 2.5 owner decision, spec §1.3 and §2.4)
- Date: 2026-10-04

## Context
During Phase 2 testing the owner added records "through a narrow side panel". The cause was a CSS defect: the reset sets `margin: 0` on every element, so the modal `<dialog>` lost its UA `margin: auto` and stuck to a screen edge. The mobile navigation was also a real side drawer. Long forms (subject and teacher dialogs) made data entry heavy.

## Decision
- **Dialogs are centred:** `.ui-dialog { margin: auto }`, 36rem wide (within the 32–40rem rule).
  - An E2E helper (`expectCenteredDialog`) checks that the year, term, shift, generate-periods and calendar-day dialogs are centred, 32–40rem wide, and have no inner scrolling at 1280×720.
- **The mobile navigation opens in the page flow** under the top bar (`MobileMenu`), not as an overlay or an edge-anchored panel. Esc closes it and returns focus to the menu button.
  - The `MobileDrawer` component and its CSS were deleted.
- **Four add patterns** (DESIGN_SYSTEM.md 14):
  - inline row (`InlineAddForm`);
  - centred dialog;
  - quick add by name with details in place (`ExpandableRow`);
  - bulk panel inside the page.

  The subject and teacher dialogs were replaced by quick add plus in-place editors. Bulk add of teachers became a panel.
- **Enforcement:** the ESLint rule `design-system/no-drawers` (component, import and class names containing "drawer" or "sheet") and the Vitest `styles/noDrawers.test.ts` (file names and stylesheets).

## Alternatives considered
- **Keep the drawer for phone navigation only:** this conflicts with the owner's "NO side drawers anywhere".
- **A bottom sheet:** still a sheet, and edge-anchored.

## Consequences
- Screens with long details grow vertically (expanded rows) instead of opening overlays.
- How to change: an exception needs an owner decision, a change to DESIGN_SYSTEM.md 14, and an update to the two guards.
