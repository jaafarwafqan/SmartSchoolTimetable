# ADR 0032: Workload assignments

- Status: Accepted (owner's Phase 3 prompt §1, §2.3, §4). Assumptions: DECISIONS_PENDING #45 and #57–#62.
- Date: 2026-10-05

## Context
The solver (Phase 4) needs to know who teaches each lesson. The curriculum says how many lessons a stage has per subject line. Every section of the stage takes those lines.

## Decision
- **`WorkloadAssignment(SectionId, CurriculumEntryId, TeacherId)`:** one ACTIVE row per (section, curriculum line).
  - A filtered unique index enforces this: `IX_WorkloadAssignments_Active_Section_Entry`, `WHERE IsArchived = 0`.
  - Archived rows keep history. Soft archive, `Version`, audit entries.
  - The weekly lessons are **derived** from the line and never copied. Editing the curriculum changes every assigned load at once.
  - One teacher per (section, line): no splitting, no per-section overrides (#45).
- **Cells:**
  - Assigning an empty cell creates the row; if someone assigned it meanwhile, the request is refused with `CONFLICT`.
  - Changing the teacher re-assigns the same row (version checked). Clearing archives it.
  - Assigning outside the teacher's specializations is allowed and flagged.
- **Bulk actions:** previewed, then applied as exactly the previewed plan in one save. An existing teacher is never replaced unless «استبدال المعلمين المعيّنين حالياً» is chosen. Applying twice changes nothing.
  - «تعيين معلم لمادة في كل شعب مرحلة» (create / replace / skip / unchanged per section)
  - «تعيين معلم الصف لمواد شعبة» (every line of one section)
  - «نقل أنصبة معلم إلى معلم آخر» (every active assignment of the year)
  - «إزالة أنصبة معلم» (archives them)
- **Loads:**
  - assigned = Σ the lessons of the teacher's active assignments in the year;
  - available = `TeacherAvailability` over the union of the teacher's sections' allowed slots (per shift), without off days and blocked periods, bounded by days × max per day;
  - limit = min(available, max per week); zero when fully released for the whole year;
  - status: «تجاوز» above the limit, «قريب» from 90% of it, otherwise «ضمن الحد» (#58).
- **Protection** through the reference guard (`WORKLOAD_IN_USE`):
  - A teacher or section with active assignments cannot be archived, and with any assignment cannot be deleted. The section stepper never removes a section with assignments.
  - Clearing or archiving a curriculum line with active assignments needs the owner's confirmation (`confirmWorkload`). The assignments are then archived at the same moment as the line, and the undo (restoring the line) brings them back unless the cell was assigned again meanwhile.

## Alternatives
- Storing lessons on the assignment: rejected. It would drift from the curriculum.
- Hard delete on clear: rejected. History is kept and undo needs the rows.
- Splitting a line between teachers: deferred (#45).

## Consequences
- Migration `Phase3CWorkload`. Endpoints under `/academic-years/{yearId}/workload`. The matrix and load reads run a fixed number of queries (`QueryCountTests`).
- The scheduling input (3D) takes the assignments as they are. The validator reports unassigned lines and loads above availability.
