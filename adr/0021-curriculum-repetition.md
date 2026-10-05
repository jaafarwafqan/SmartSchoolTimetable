# ADR 0021: Curriculum entries may repeat a subject

- Status: Accepted (Phase 2.5 owner decision, spec §1.4 and §3.3)
- Date: 2026-10-05

## Context
The owner wants a curriculum table: stage × subject × weekly lessons. In Iraqi schools one subject is often taught as two lines in the same stage, for example Arabic as «قواعد» and «أدب», each with its own lesson count and, later, possibly its own teacher.

## Decision
- **`CurriculumEntry`:** `StageId`, `SubjectId`, `WeeklyLessons` (1–15), optional `Label` (≤ 40) with `NormalizedLabel`, `NeedsDoublePeriod`, `Notes` (≤ 300), soft archive, version.
- **(stage, subject) is deliberately NOT unique** in the database. There is a plain index for lookups and a check constraint for 1–15.
- **Lines are told apart by the normalized label.** The table shows a main row per subject (no label), plus one row per distinct label.
  - Two lines with the same subject and label are allowed but flagged in the cell as «مكرر». Both count in the totals.
- **Totals per stage** are compared with the weekly capacity of the stage's active sections, **per shift**. A dual-shift stage gets two results. Each result is under, equal or over, shown with an icon and text.
- **Protections:**
  - A stage or subject with active lines cannot be archived (`CURRICULUM_IN_USE`).
  - With any lines, it cannot be deleted; the foreign keys are `Restrict`.
- **Year copy** copies the active lines to the copied stages (decision #22).

## Alternatives considered
- **Unique (stage, subject) with a "parts" child table:** more structure and more UI, for the same result.
- **Repeats as separate subjects** («اللغة العربية - قواعد»): duplicates subject settings (colour, priority, blocked periods) and splits teacher qualifications.

## Consequences
- Phase 3 turns each active line into one workload line per section, so repeated lines can get different teachers.
- How to change: add a unique index on (StageId, SubjectId, NormalizedLabel) in `CurriculumEntryConfiguration` (new migration) to forbid exact duplicates.
