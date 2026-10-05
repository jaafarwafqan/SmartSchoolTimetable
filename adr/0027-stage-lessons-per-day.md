# ADR 0027: Lessons per day are set per stage, within the shift

- Status: Accepted (owner model change M2, approved 2026-10-05). It refines ADR 0020.
- Date: 2026-10-05

## Context
In one shift, younger grades often go home earlier than older ones: the bell schedule is the same, but the first grade has fewer lessons a day than the sixth. Capacity per shift alone made every stage of a shift equal.

## Decision
- **The shift defines the bell schedule:** its periods and the most lessons per working day (ADR 0020).
- **Each stage has its own lessons per working day** (`StageDayLessons`: StageId, Day, Lessons ≥ 1; unique per stage and day).
  - A day without a row inherits the shift's count.
  - A value is validated against what the stage's shifts teach that day (the shifts of its active sections, or all shifts of the year while it has none).
  - At run time a value is capped by the section's shift: `min(stage, shift)`.
- **Sections inherit their stage's counts.** Weekly capacity of a section = the sum over working days of `Stage.LessonsOn(day, shift)` (breaks excluded). Section lists, stage cards, curriculum totals (each stage against its own capacity, per shift when needed), the dashboard and the setup review all use it.
- **Shortening a shift** below a stage's own count on some day:
  - The periods tab previews the affected stages (`POST …/day-lessons/impact`). After confirmation, those stage counts are lowered to the shift (`confirmStageChanges`).
  - Without confirmation the save is refused with `STAGE_LESSONS_ABOVE_SHIFT`. The wizard's timing step has no such preview, so it reports this code with an Arabic explanation.
- **Teachers' and subjects' blocked-period grids stay on the shift's periods,** because teachers cross stages. A section only uses the first N lessons of its day; Phase 3 workload checks and the Phase 4 solver must respect this.
- **Stage-level, not section-level:** sections of one grade normally follow the same timetable (decision #35).

## Consequences
- Migration `Phase25FixStageDayLessons` adds the table. Existing stages have no rows and therefore inherit the shift: no data change.
- A year copy copies each stage's counts.
- How to change: `Stage.LessonsOn`, `Section.WeeklyCapacity(week, shift, stage)`.
