# ADR 0047: Checking a saved timetable against today's data; minimal repair; teacher replacement

- Status: Accepted (MF11, branch `work/phase-5`)
- Date: 2026-10-10

## Context
A saved version is checked against the scheduling input it was made from, so after the owner blocks a teacher's day or gives a subject to another teacher it still said «لا خرق لأي قيد إلزامي» next to a vague «تغيّرت بيانات المدرسة بعد هذا الإصدار». Saved and approved versions must not change by themselves, but the owner must see the truth and have a safe way to fix it.

## Decisions
1. **Re-verify against the CURRENT input with the same independent `TimetableVerifier`** (`CurrentDataAnalyzer`). The saved lessons are the subject; today's input (`SchedulingInputBuilder`) is the rulebook. Two findings exist only here: `TEACHER_REASSIGNED` (the line now has another teacher; the lesson is verified as if that teacher taught it) and `ASSIGNMENT_REMOVED`. Everything else reuses `ViolationCodes`. `GET /timetables/{id}/current-check` returns findings, the input changes, `stale`, `canRepair`, `canReplaceTeachers` and the names needed to write sentences. The version's own snapshot check stays and is labelled «الفحص وقت التوليد».
2. **What changed** (`InputDiff`): assignments, availability (off days, blocked periods, release, archive, specializations), limits, curriculum hours, timing/sections, subject rules, resources, priorities. A stale hash with no listed difference shows `OTHER_CHANGE`, never an empty "changed".
3. **Repair = a generation run with `IsRepair`** that reuses the M1 locks. Locked lessons are the saved lessons that are not part of any finding (a reassigned lesson keeps its slot with the new teacher). The free set per finding is the least that can resolve it (the lesson itself; both lessons of a clash; the teacher's day for a day limit; and so on). If the model is infeasible, three tries: (1) only those lessons free; (2) also the other lessons of the same section and day (a day must start at the first lesson with no gap, so freeing a middle lesson needs its neighbours free); (3) everything of the affected sections and teachers. If none works the run ends «تعذّر إصلاح الجدول بتغيير محدود» and the owner can generate fully. The result is a new draft (source `repaired`, parent = the repaired version, saved with TODAY's snapshot and hash); the result page reports how many lessons changed. The solver does not penalise moves inside the free set; the set is what keeps the change small (measured: 4 conflicting lessons → 9 changed of 56).
4. **Replace teacher** (`POST /timetables/{id}/replace-teachers`) is synchronous: new teacher, same slots, new draft (source `teacherReplaced`), allowed only when the substituted timetable has no finding at all; otherwise `TIMETABLE_REPLACE_CONFLICT` and the screen offers the repair.
5. **Approval**: refused with `TIMETABLE_CONFLICTS_WITH_CURRENT_DATA` while any finding exists; allowed with a warning when the data only changed. Archiving and rollback are unaffected.
6. **Nothing changes by itself.** Both fixes create new drafts; the original version, approved or not, is untouched.

## Consequences
- Corrective migration `Phase5RepairedVersions`: `GenerationRuns.IsRepair`, source check 1–5.
- New error codes `TIMETABLE_CONFLICTS_WITH_CURRENT_DATA`, `TIMETABLE_REPLACE_CONFLICT`, `TIMETABLE_NOTHING_TO_REPLACE`, `TIMETABLE_NOTHING_TO_REPAIR`; audit events `TimetableRepaired`, `TimetableTeacherReplaced`; code families `CurrentFindingCodes` and `InputChangeCodes` live in `GenerationCodes.cs` with Arabic sentences checked by a contract test.
- Manual editing still checks against the parent's own snapshot; an edited version of a stale version stays stale. A later milestone (M4b) adds a dashboard alert when the approved version conflicts with current data.
