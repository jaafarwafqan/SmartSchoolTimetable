# ADR 0031: Resources, teacher specializations and the scheduling profile

- Status: Accepted (owner's Phase 3 prompt §1, §2.1, §2.2, §2.4). The decided points are not reopened; the assumptions are listed in DECISIONS_PENDING #45–#47 and #53–#55.
- Date: 2026-10-05

## Context
Phase 4 places lessons with CP-SAT. Before that, the school's data has to say:
- which shared places a lesson needs;
- which subjects a teacher is qualified for;
- how much each soft rule matters.

All three must be plain, validated data that the scheduling input (Phase 3D) can read without guessing.

## Decision
- **Resource** (global, like subjects and teachers):
  - name (unique after Arabic normalization), kind (`lab`, `field`, `hall`, `other`: مختبر، ساحة، قاعة، أخرى), capacity 1–20 (default 1), notes;
  - soft archive, `Version`, audit entries.
  - **Capacity** is the number of sections that may use the resource in the same slot. It is evaluated **per shift and per slot**: the two shifts never share a slot, so each shift has the full capacity.
- **Subject → resource:** `Subject.RequiredResourceId`, at most one resource per subject (assumption #46).
  - A newly chosen resource must exist and be active. A subject may keep a resource that was archived later.
  - The resource is protected by the reference guard (`RESOURCE_IN_USE`) and by a restricting foreign key.
- **Teacher specializations:** an owned list of subject ids (`TeacherSpecializations`, key (teacher, subject)).
  - They describe the teacher only. Assigning outside them is allowed with a warning (3C).
  - Deleting a subject removes it from every teacher's list (cascade, #53).
  - An update that does not send the list keeps it.
- **Scheduling profile:** exactly one row (Id = 1), created at start-up. It holds five soft rules, each with enabled + weight 0–100:

  | Rule | Default weight |
  |---|---|
  | spread subjects across days | 20 |
  | avoid teacher gaps | 30 |
  | heavy subjects early | 15 |
  | avoid the same subject repeated | 25 |
  | keep double lessons together | 10 |

  - `Version` is the concurrency token.
  - `ProfileVersion` grows on every real change, including «استعادة الإعدادات الافتراضية», so a generated timetable can record the profile state it used.
  - An update that changes nothing creates no new version. Hard constraints are never part of the profile.

## Alternatives
- A resource per curriculum line, or several resources per subject: deferred (#46).
- Several named profiles: deferred (#47).
- Weights typed freely: replaced by a choice in steps of five (the saved value is always offered), following "choose, don't type".

## Consequences
- Migration `Phase3BResourcesProfile` adds `Resources`, `Subjects.RequiredResourceId`, `TeacherSpecializations`, `SchedulingProfile` and `SchedulingProfileRules`. Existing databases keep their rows; the profile row is created on the next start.
- The scheduling input (3D) reads resources with capacities, subject requirements, specializations and the profile with its version.
- How to change: `Domain/Resources/Resource`, `Domain/Scheduling/SchedulingProfile`, `Teacher.Specializations`, `Phase3Configuration`.
