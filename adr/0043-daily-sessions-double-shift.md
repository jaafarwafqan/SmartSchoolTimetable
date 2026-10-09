# ADR 0043: Daily sessions (دوام مزدوج) on one timetable

- Status: Accepted (Phase 4 follow-up R3)
- Date: 2026-10-10

## Context
In the Iraqi double shift, a school keeps one set of sections, one curriculum, one staff and one weekly load. It works every school day. What changes is the daily **session** each working day falls in: morning (صباحي), evening (مسائي), and possibly noon (ظهري) later. The day→session mapping usually flips in semester 2. For example:
- semester 1: Sunday and Monday morning, Tuesday to Thursday evening;
- semester 2: the reverse.

Before R3, «مزدوج» (`StudyType.Dual`) meant something else: two shifts, each with its **own** sections.

## Decision
- **Model.** A `SessionPlan` per academic year has:
  - `System`: `OneSession` (the default, and the meaning of "no row"), `TwoSessions`, or `ThreeSessions` (allowed by the model; shown as «قريباً» in the UI).
  - `SessionPeriods`: the timing of each session other than morning.
  - `SessionDays`: the mapping `(Term 1|2, Day) → Session`.
- **Morning timing is the shift's own periods.** The plan belongs to the year's single structural shift, so single-session behaviour and every existing screen stay unchanged.
- **Same number of lessons in every session** (`SESSION_LESSON_COUNT_MISMATCH`). Only the start, the lesson length and the breaks differ.
  - The shift's lesson count cannot change while sessions are on.
  - A second shift cannot be added while sessions are on (`SESSIONS_NEED_ONE_SHIFT`).
  - The old two-shift mode cannot be applied while sessions are on (`SESSIONS_NEED_ONE_SHIFT`).
  - Per-day lesson counts (ADR 0020) stay as they are. The solver already handles them, and they apply to every session alike.
- **One grid.** The scheduler, verifier and approval work on (day, lesson) slots exactly as before. A semester changes only the clock shown: the viewer, print and Excel compute each day's times from its session in the chosen semester.
- **Hash.** `ShiftInput.SessionBreaksAfter` lists the lesson numbers after which another session has a break.
  - It is hashed because it changes which timetables are valid: a double lesson must be adjacent in every session. The validator, model builder, verifier and scorer all use `ShiftInput.Adjacent`.
  - It is left out of the JSON when null, so every single-session school keeps its exact hash.
  - The other sessions' clock times and the mapping are not hashed: they change no lesson.
- **Teacher availability** stays by (day, lesson). Availability by session is a possible future option (DECISIONS_PENDING #83).
- **Migration.** `Phase4SessionPlans` only adds tables, so old data reads as one session. A backup from the previous schema restores and upgrades (tested).

## Consequences
- The old two-shift mode is kept and relabelled «ورديتان بشعب مختلفة», so the two meanings of «مزدوج» are not confused (DECISIONS_PENDING #82).
- Copying a year's structure to a new year does not copy the session plan yet; the new year starts as one session (#84).
