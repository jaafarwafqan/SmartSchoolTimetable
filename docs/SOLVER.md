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
| H9 | Per (section, subject) and day, at most the daily limit: the section's override, else the grade's, else the subject's (1 to 6, «أولويات الجدول › متقدم»), else max(2, ⌈weekly ÷ working days⌉) (`DailyCaps`, one rule for the solver, the verifier and the validator; ADR 0053) | Σ ≤ cap | `SUBJECT_DAILY_CAP` |
| H11 | «أقصى حصص متتالية للمعلم» **when hard** for the teacher (school «إلزامي لكل المعلمين» or his «إلزامي»; otherwise it is S9): at most k (2 or 3; the teacher's own, else the school's, default 3) consecutive lesson numbers per teacher, shift and day; a break row does not interrupt a run | Σ over every window of k + 1 lesson numbers ≤ k | `TEACHER_CONSECUTIVE` |
| H12 | Only when «جعل التوازن إلزامياً» is on: a teacher's full days within one lesson of each other; a partly available day at most its share ⌈load × usable ÷ total usable⌉ (`TeacherDays`) | max − min ≤ 1; Σ ≤ share | `TEACHER_BALANCE` |
| H10 | «دروس مزدوجة» mode only: double lines as adjacent pairs; an odd leftover is single | x = pair starting here + pair ending here + single; Σpairs = ⌊n ÷ 2⌋; Σsingles = n mod 2 | `DOUBLE_PERIOD_BROKEN` |

A "moment" is a lesson start time: the group holds every slot whose clock interval contains that time. Intervals lie on a line, so these groups cover every overlap. With one shift, a moment is simply one lesson number.

A double line is one whose curriculum line needs double periods or whose subject requires them.

## 3. Objective (soft rules, profile weights)
The objective minimises Σ weight × penalty, for the enabled rules with weight > 0, in integers. `TimetableScorer` computes the same penalties from the lessons. For a proven-optimal timetable its total equals the objective (tested).

| Key | Rule | Penalty |
|---|---|---|
| `spreadSubjectsAcrossDays` | S1 spread (every subject; its own switch «توزيع الحصص على أيام مختلفة» turns it off, ADR 0055) | per (section, subject, day): lessons above 1 (above 2 for a double subject), plus lessons above the fair share ⌈weekly ÷ days the subject's teacher is there⌉ (`SubjectSpread`, `SpreadShare.Penalty`) |
| `avoidTeacherGaps` | S2 teacher gaps | per (teacher, day, shift): free lesson numbers between the first and the last lesson |
| `heavySubjectsEarly` | S3 heavy subjects early | per lesson of a heavy subject: lesson number − 1 |
| `avoidSameSubjectRepeated` | S4 no back-to-back repeat («تجنّب تكرار المادة في حصتين متتاليتين»; a same-day repeat in general is S1) | per section and day: consecutive lesson numbers with the same subject (double subjects excluded) |
| `keepDoubleLessonsTogether` | S5 doubles together (standard mode) | per double line: ⌊n ÷ 2⌋ − adjacent pairs it got |
| `spreadTeacherLessons` | S6 «توزيع حصص المعلم بفواصل» (exclusive with S2) | per (teacher, day, shift): lesson numbers n and n + 1 both his (a pair of two double-lesson lessons excepted) |
| `balanceTeacherDays` | S7 balanced teacher days (not weighed when H12 is on) | per teacher: max − min − 1 over his full days (not below 0), plus the lessons of a partly available day above its share |
| `maxConsecutiveLessons` | S9 consecutive lessons (a priority unless hard) | per (teacher, shift, day): windows of k + 1 consecutive lesson numbers all his |
| `fairLastPeriods` | S8 fair last periods | per teacher: weighted last-period lessons (evening day 2, morning 1) above ⌈total × load ÷ all loads⌉ |

The default weights, in display order: S1 80 (its «مهم جداً»; ADR 0055), S9 40, S6 30, S2 off (30), S7 40, S8 20, S3 15, S4 25, S5 10. S2 and S6 cannot both be on (ADR 0053). Subject names are never special cases; only subject flags matter.

### Fill checks before the solver (pre-solve validator)
A section whose weekly lessons equal its slots is full every day, so a day off or the daily limit can make it impossible. The validator proves this before the solver runs (each is an upper bound; a section may keep `slots − lessons` slots empty, and a day or a slot is reported only when that slack cannot absorb it): `SUBJECT_DAYS_SHORT` (a subject needs ⌈weekly ÷ limit⌉ days and its teacher is free on fewer), `SECTION_DAY_UNDERFILLED` (what the subjects can place on a day is below the day's lessons less the slack), `SECTION_SLOT_UNCOVERED` (no teacher of the section is free at a slot), and the warning `TEACHERS_SAME_DAY_OFF`. The checks stay silent for a section with an unassigned line or an archived or released teacher, which have their own findings.

Teacher-day checks (ADR 0053): `TEACHER_CONSECUTIVE_SHORT` (an error when the limit is hard for that teacher, a warning when it is a priority). In a run of m free lesson numbers at most m − ⌊m ÷ (k + 1)⌋ lessons fit; for P lessons that is ⌊(P + 1)·k ÷ (k + 1)⌋. It is raised only when the rule, and not the day or week maximum, makes the load impossible. `TEACHER_BALANCE_HARD_SHORT` (error, hard balance only): each full day holds at most min(its capacity, the quietest full day's capacity + 1), and a partial day at most its share.

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
