# ADR 0020: Per-day lesson counts

- Status: Accepted (Phase 2.5 owner decision, spec §1.5 and §3.1)
- Date: 2026-10-04

## Context
Iraqi schools often teach fewer lessons on some days (for example Sunday 7, Thursday 6). Phase 2 assumed the same number of lessons every working day. Capacity, blocked-period checks and teacher limits all used that single number.

## Decision
- **The rule:** a shift teaches the **first N lessons** of its daily schedule on each working day. N is between 0 and the shift's lesson count.
- **Storage:** only the days that differ from the full count are stored (`ShiftDayLessons`: ShiftId, Day, Lessons; unique per shift and day). Existing data needs no rows and keeps working.
  - If periods are later reduced, a stored count above the new lesson count is capped on read (`Shift.LessonsOn`).
- **Weekly capacity** of a section = the sum over the working days of that day's count for the section's shift (`Section.WeeklyCapacity`).
- **The schedule grid** (`ScheduleGrid.From`) records, for each day, the most lessons any shift of the current year teaches that day, and the weekly total of the largest shift.
  - Blocked periods must exist on their day.
  - A teacher's maximum per day must be ≤ the most lessons of any day.
  - A teacher's maximum per week must be ≤ the largest shift's weekly total.
- **UI:**
  - A compact row of steppers per shift on the periods tab (`DayLessonsEditor`).
  - Blocked-period grids show cells for lessons that do not exist on that day as hatched, `aria-disabled` and not toggleable.

## Alternatives considered
- **A separate period list per day:** much more data entry, and the owner did not ask for different times per day.
- **Storing a count for every day:** duplicates the default, and needs a data migration for existing shifts.

## Consequences
- Phase 3 workload checks compare weekly lessons with the per-day capacity.
- The solver (Phase 4) must not place lessons beyond a day's count.
- How to change: replace `Shift.LessonsOn` with per-day period lists. Its callers (capacity, grid) stay the same.
