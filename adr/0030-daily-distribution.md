# ADR 0030: Daily lessons suggested from the curriculum total

- Status: Accepted (owner instruction, 2026-10-05). It builds on ADR 0027.
- Date: 2026-10-05

## Context
With the curriculum known, a stage's weekly total fixes how many lessons it needs. Typing lessons per day for every stage is unnecessary.

## Decision
- **The distribution rule (`DailyDistribution.Suggest`):**
  - For weekly total T over the working days in week order (from the week start), spread as evenly as possible, with the extra lessons on the EARLIER days. For 5 days: 28 → 6,6,6,5,5; 27 → 6,6,5,5,5; 29 → 6,6,6,6,5; 30 → 6×5; 31 → 7,6,6,6,6; 33 → 7,7,7,6,6.
  - A day never exceeds what the shift teaches that day. Lessons that do not fit move to the earliest days with room.
  - In a stage with sections in two shifts, each day is bounded by the smaller shift, so both fit.
  - **No suggestion** when there is no curriculum yet, when T is below the number of working days, or when T is above the week's capacity. The last case is shown with the reason and a hint (increase the shift's periods, or reduce the curriculum), and applying it is refused with `DAILY_TOTAL_ABOVE_SHIFT`.
- **Applying:**
  - The suggestion is shown per stage (stages screen and wizard step 5) and applied only to the stages the owner selects and confirms.
  - It writes normal stage day counts, editable per day afterwards, and marks them `Stage.DayLessonsSuggested`.
- **Owner edits are never overwritten:**
  - Counts the owner set by hand (`DayLessonsSuggested` = false with own counts) are shown as «معدّل يدوياً» and skipped.
  - When the curriculum total changes after a suggestion was applied, a non-blocking notice says «تغيّر مجموع المنهج، يتوفر اقتراح جديد».

## Consequences
- The capacity status chips (ناقص / مطابق / زائد) work unchanged; applying the suggestion makes a stage "مطابق" when its total fits.
- How to change: `DailyDistribution` (Domain), `DailySuggestionService`.
