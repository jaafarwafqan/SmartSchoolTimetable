# ADR 0019: Soft archive and hard delete for school data

- Status: Accepted (Phase 2 specification section 1; taken autonomously under protocol 0.5)
- Date: 2026-10-04

## Context
Later phases need history. Workload (Phase 3) and timetable versions (Phase 4–5) will reference teachers, subjects, stages and sections. The specification asks for:
- soft archive instead of delete for these four entities;
- hard delete only for records with no references, and only after a confirmation dialog.

## Decision
- **Archive:** teachers, subjects, stages and sections have `IsArchived` and `ArchivedAt`. Archive and restore are idempotent Domain operations that bump `Version`.
  - Lists hide archived rows unless `includeArchived=true`.
  - "Active" and "archived" are one concept, not two flags (DECISIONS_PENDING #2).
- **Archive rules:**
  - A stage with active sections cannot be archived (`RECORD_IN_USE`).
  - A section cannot be created in, or restored into, an archived stage (`STAGE_ARCHIVED`).
- **Hard delete:**
  - Every delete carries the read `version` (`DELETE …?version=`) and asks for confirmation in the UI.
  - A stage can be deleted only while it has no sections, archived ones included.
  - A shift can be deleted only while no section uses it (`RECORD_IN_USE`).
  - Subjects, teachers and sections can be deleted while nothing references them. In Phase 2 nothing does. Phase 3 must add its reference checks to these delete operations.
  - Academic years cannot be deleted while they have structure (`YEAR_STRUCTURE_IN_USE`), nor while they are the current year and other years exist (`CURRENT_YEAR_REQUIRED`).
  - Calendar days are not archivable; they are deleted after confirmation.
- **Audit:** every create, update, archive, restore and delete writes an audit event (names are in API.md).
- **Database:** foreign keys from sections to stages and shifts use `Restrict`, so the database refuses a delete that the service would refuse.

## Alternatives considered
- **A global soft-delete filter** (EF query filter): hides rows everywhere, including from history views that later phases need. Rejected.
- **Separate `IsActive` and `IsArchived` flags:** they can contradict each other. Rejected.
- **Cascade delete:** silently destroys data. Rejected.

## Consequences
- Phase 3 must extend the delete checks: subjects and teachers become referenced by workload.
- How to change: the rules live in the Application services (`StagesSectionsService`, `SubjectsService`, `TeachersService`, `TimetableStructureService`) and in the Domain `Archive`/`Restore` methods.
