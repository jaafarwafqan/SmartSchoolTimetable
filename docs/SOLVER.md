# Solver (Phase 4)

The timetable engine is Google OR-Tools CP-SAT 9.15.6755 (ADR 0002, ADR 0037). It reads the Phase 3 `SchedulingInput` (ADR 0034) and the generation settings. It returns a neutral `SolverResult`. Code:
- `Infrastructure/Solver/CpSatModelBuilder.cs`: the model.
- `CpSatSolver.cs`: the solve, progress and cancellation.
- `CpSatDiagnostics.cs`: the conflict cores.
- `Application/Generation`: the contracts, `TimetableVerifier`, `TimetableScorer` and `GenerationEngine`.

## 1. Variables
- `x[line, day, lesson]` (Boolean) for every assignment line (section, curriculum line, teacher), working day and lesson number. It exists only when the slot is allowed:
  - lesson ≤ the stage's lesson count for that day in the section's shift (H3);
  - not the teacher's off day, blocked period or full release (H5);
  - not the subject's blocked period (H6).
- Real time of lesson *n* in a shift: the *n*-th `Lesson` period row. If a shift has no period rows, which happens only in hand-built inputs, the times are estimated as 45-minute lessons from the shift start, or from 8:00 (DECISIONS_PENDING #69).
- Lessons *n* and *n+1* are adjacent (for doubles) only when no break row lies between them (DECISIONS_PENDING #64).

## 2. Hard constraints (never violated)

| ID | Rule | Model | Verifier code |
|---|---|---|---|
| H1 | Each assignment line gets exactly its weekly lessons | Σx = weekly | `WRONG_LESSON_COUNT`, `UNKNOWN_LESSON` |
| H2 | A section has at most one lesson per slot | AtMostOne per (section, day, lesson) | `SECTION_CONFLICT` |
| H3 | Lessons fill the first periods; free periods fall at the end; never beyond the stage's count | Σx(n+1) ≤ Σx(n) per section and day; no variables beyond the count | `SECTION_GAP`, `OUTSIDE_SECTION_DAY` |
| H4 | A teacher teaches at most one lesson at the same real time, across shifts | AtMostOne per teacher and "moment" | `TEACHER_CONFLICT` |
| H5 | Off days, full release and blocked periods are never used | no variables | `TEACHER_UNAVAILABLE` |
| H6 | Subject blocked periods are never used | no variables | `SUBJECT_BLOCKED` |
| H7 | Teacher maximum per day and per week | Σ ≤ max | `TEACHER_DAY_LIMIT`, `TEACHER_WEEK_LIMIT` |
| H8 | Resource capacity at every moment, across shifts | Σ ≤ capacity per resource and moment | `RESOURCE_CAPACITY` |
| H9 | Per (section, subject) and day, at most max(2, ⌈weekly ÷ working days⌉) | Σ ≤ cap | `SUBJECT_DAILY_CAP` |
| H10 | «دروس مزدوجة» mode only: double lines as adjacent pairs; an odd leftover is single | x = pair starting here + pair ending here + single; Σpairs = ⌊n ÷ 2⌋; Σsingles = n mod 2 | `DOUBLE_PERIOD_BROKEN` |

A "moment" is a lesson start time: the group holds every slot whose clock interval contains that time. Intervals lie on a line, so these groups cover every overlap. With one shift, a moment is simply one lesson number.

A double line is one whose curriculum line needs double periods or whose subject requires them.

## 3. Objective (soft rules, profile weights)
The objective minimises Σ weight × penalty, for the enabled rules with weight > 0, in integers. `TimetableScorer` computes the same penalties from the lessons. For a proven-optimal timetable its total equals the objective (tested).

| Key | Rule | Penalty |
|---|---|---|
| `spreadSubjectsAcrossDays` | S1 spread (subjects flagged «توزيع على الأيام») | per (section, subject, day): lessons above 1 (above 2 for a double subject) |
| `avoidTeacherGaps` | S2 teacher gaps | per (teacher, day, shift): free lesson numbers between the first and the last lesson |
| `heavySubjectsEarly` | S3 heavy subjects early | per lesson of a heavy subject: lesson number − 1 |
| `avoidSameSubjectRepeated` | S4 no back-to-back repeat | per section and day: consecutive lesson numbers with the same subject (double subjects excluded) |
| `keepDoubleLessonsTogether` | S5 doubles together (standard mode) | per double line: ⌊n ÷ 2⌋ − adjacent pairs it got |

The default weights are 20 / 30 / 15 / 25 / 10. Subject names are never special cases; only subject flags matter.

## 4. Parameters, determinism and progress
- Time limit: 60 s by default, 10–600 s allowed.
- Workers: max(1, logical cores ÷ 2) by default.
- Seed: stored with the run.
- Mode: «عادي» (`standard`) or «دروس مزدوجة» (`doublePeriods`).
- Deterministic mode («نتيجة قابلة للإعادة»):
  - It uses one worker, the fixed seed and `max_deterministic_time` = the limit, so the same input and settings give the same timetable (tested).
  - Deterministic units are slower than seconds on this machine (docs/PERFORMANCE.md). A wall-clock safety net stops the run at 5 × the limit; reaching it ends reproducibility for that run.
- Progress comes from the CP-SAT solution callback only: elapsed time, improving solutions, best objective, best bound, and the time of the first timetable. There is no estimated percentage.
- Cancelling calls `StopSearch`. The best timetable found so far is returned with status `Cancelled`.

## 5. Statuses and diagnostics
- Statuses:
  - `Optimal`: proven best.
  - `Feasible`: valid, not proven best.
  - `Infeasible`: proven impossible.
  - `TimedOut`: no timetable within the limit; this is not a proof.
  - `Cancelled`.
  - `Failed`: the engine could not run (`SOLVER_FAILED`), or the verifier rejected the result (`TIMETABLE_VERIFICATION_FAILED`).
- Infeasible: the diagnostic model enforces every restriction family under its own assumption literal (ADR 0040):
  - `teacherAvailability`, `teacherLimits`, `sectionPacking`, `stageDays`, `subjectBlocked`, `subjectDailyCap`, `resourceCapacity`, `doublePeriods`.
  - H1, H2 and H4 are always enforced.
  - A sufficient core is shrunk by deletion within 25% of the time limit. Each family of the core is then tested alone for "relaxing it makes the timetable possible".
  - Findings use `DiagnosticCodes` (`CORE_*`) with entities, numbers and fix codes, which the frontend renders in Arabic with links.
- TimedOut: one finding, `TIMEOUT_NO_SOLUTION`, with the advice to raise the limit or review the warnings.

## 6. Verifier
`TimetableVerifier` re-checks H1–H10 from the raw input with its own code (ADR 0039). Every solver timetable passes it before it is saved, and every manual edit is checked by it. A violation in a solver timetable is a defect: the run fails and nothing is saved.

## 7. Limits
- No two-stage solve and no decomposition in this delivery (PHASE_4_MVP_PROMPT). They are the first things to try for large schools (docs/PERFORMANCE.md).
- A blocked period applies to that lesson number in every shift (DECISIONS_PENDING #57).
- S2 counts gaps within one shift; a teacher in two shifts on one day has no gap counted between the shifts.
