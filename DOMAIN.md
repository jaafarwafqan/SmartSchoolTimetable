# Domain Model

## Aggregate groups
### School administration
- SchoolProfile
- AcademicSettings
- AcademicCalendar

### Planning
- Teacher
- Subject
- Resource
- Stage
- Section
- Workload
- SchedulingProfile
- TimetableVersion
- TimetableLesson

### Operations
- AuditEntry
- GenerationJob
- ConflictRecord
- LocalOwnerAccount

## Invariants
- The deployed application accepts exactly one owner account.
- A section belongs to one shift.
- Section weekly workload must match configured capacity.
- A teacher cannot be assigned to overlapping lessons.
- Published timetables are immutable.
- Only valid state transitions are allowed.

## Events
- TimetableGenerated
- TimetableApproved
- TimetablePublished
- TimetableRolledBack
- ConflictDetected
- OwnerAccountCreated
- PasswordChanged

## Non-goals in Phase 0
No implementation of domain logic; only specification and validation strategy.

## Implemented in Phase 2 (checkpoints 2A–2B)
- **`Domain/Common`:** `VersionedEntity` (`Id`, `Version`, `Touch()`), plus the `DomainErrorCode` enum, the `DomainErrors` collector and `DomainValidationException`. The Domain holds no API code strings; `Application/Common/DomainErrorMapping` maps each `DomainErrorCode` to an `ErrorCodes` constant.
- **`Domain/Text/ArabicText`:** `Normalize` builds the uniqueness and search key; `Clean` trims and collapses spaces in display values.
- **`Domain/SchoolSetup/SchoolProfile`:** a singleton (`Id` = 1).
  - Name is required (≤ 200 characters); principal and schedule-officer names are optional (≤ 150).
  - School type and study type.
  - Time zone from a fixed IANA list (default `Asia/Baghdad`).
  - Numeral system (Arabic-Indic or Western) and calendar display (Gregorian or Hijri).
  - Logo and stamp `SchoolAsset` references. `SetAsset` returns the replaced file, so the caller deletes it only after a successful save.
- **`Domain/SchoolSetup/AcademicYear`,** with owned `Term`s. Invariants:
  - start is before end;
  - the label is unique (after normalization);
  - terms lie inside the year, do not overlap and have unique names;
  - only one year is current (also enforced by a database index);
  - the current term belongs to the year, and removing it clears the current term.
- **`Domain/SchoolSetup/Shift`:** year-scoped, unique normalized name and display order per year; copies its lesson/break periods into a new year. Period rows must be ordered, nonoverlapping, same-day and end after they start; each shift has 1–12 lessons and up to 20 rows.
- **`LessonPeriod` and `PeriodGenerator`:** lessons get their own 1-based numbering (breaks excluded); helper output remains editable before saving. Each lesson stores independent start/end bell flags.
- **`WorkingWeek`:** ISO weekdays (1 = Monday … 7 = Sunday), nonempty days, configurable week start, Sunday–Thursday default.
- **`BellSettings`:** one row with a built-in tone and break bell option; browser synthesizes preview tones through Web Audio. Live ringing remains Phase 7.
- **Planned for 2C–2E:** stages and sections belong to an academic year; subjects and teachers are global.

## Implemented in checkpoints 2B–2D
- **Timetable structure:**
  - `WorkingWeek` (singleton; ISO days; week start).
  - `Shift` (per year) with owned `LessonPeriod` rows. Rules: ascending, no overlaps, end after start, at least one lesson, at most 12 lessons and 20 rows.
  - `PeriodGenerator` produces an editable proposal.
  - `BellSettings` (tone and break bell).
- **Stages and sections:**
  - `Stage` (per year) and `Section` (stage and shift), both with soft archive.
  - `Section.WeeklyCapacity(week, shift)` = working days × lessons per day of the shift (breaks excluded).
- **`ScheduleGrid`:** working days × the most lessons per day in the current year. `BlockedPeriod(Day, LessonNumber)` must lie inside it (DECISIONS_PENDING #12); teachers reuse this in 2E.
- **`Subject`** (global):
  - Name unique after normalization; colour index 1–10 (palette tokens only); priority 1–5.
  - Flags: distribution enabled, spread across days, heavy, requires double period. Notes ≤ 500.
  - Blocked periods (deduplicated and sorted); soft archive.
- **`Teacher`** (global):
  - Names: full name ≤ 150; short name ≤ 40, unique after normalization.
  - Off days must be working days.
  - Blocked periods lie inside the `ScheduleGrid`.
  - Full release: a flag, an optional reason (≤ 200) and an optional date range (from ≤ to).
  - Limits: optional; max per day ≤ lessons per day and max per week ≤ the grid's weekly capacity once periods exist (DECISIONS_PENDING #5); max per day ≤ max per week.
  - Notes; soft archive.
- **`TeacherNames.ProposeShortName`:** proposes the first two words, then the first three, then the full name, using the first that is free (DECISIONS_PENDING #13).
- **`CalendarDay`** (global, not copied with a year's structure):
  - Title ≤ 120; one day or a range of up to 366 days.
  - Kind: official holiday, school holiday, exam or special day; plus an "affects schedule" flag.
  - `IsOutside(yearStart, yearEnd)` drives the "outside the current year" warning; such entries are never blocked.

## Phase 2.5B
- **`Shift.Kind`** (morning, evening, other) and **per-day lesson counts** (`SetDayLessons`, `LessonsOn`, `WeeklyLessons`). A day teaches the first N lessons (ADR 0020).
- **`Section.WeeklyCapacity`** sums the per-day counts.
- **`ScheduleGrid.From(days, shifts)`:** lessons per day = the most any shift teaches that day; the weekly bound = the largest shift's total.
- **Shift mode = `SchoolProfile.StudyType`** (`SetStudyType`); `SetSchoolType` serves wizard step 1.
- **`SetupProgress`:** steps 1–7, completed and skipped sets (a completed step is never "skipped"), the current step, finished.

## Phase 2.5C
- **`CurriculumEntry`** (ADR 0021): a subject taught in a stage for 1–15 lessons a week, with an optional label. `SameLineAs(subject, label)` compares normalized labels. (stage, subject) may repeat.
- **`CurriculumTotals.For(planned, capacities)`:** one result per shift used by the stage's sections: `Under`, `Equal` or `Over`, with `Difference` = capacity − planned.
- **`SectionLabels`:** أ، ب، ج، د، هـ، و، ز، ح، ط، ي … then أ1، ب1 …; or numbers; or Latin A–Z then A1. `Next` skips labels already used.
- **`Stage.TemplateKey`:** the template grade (and branch) a stage came from, used to match it again.
- **`PeriodPlan`** takes a list of `BreakSlot(AfterLesson, Minutes)`; the single-break constructor is kept.

## Phase 2.5 fixes 2
- **Breaks (ADR 0026):** `PeriodPlan` takes up to three `BreakSlot(AfterLesson, Minutes)` and a `GapMinutes` (0–30) between lessons that have no break between them.
- **Lessons per stage (ADR 0027):** `Stage.DayLessonCounts`, `SetDayLessons(counts, workingDays, maxOnDay)`, `LessonsOn(day, shift)` = `min(own ?? shift, shift)`, and `ClampDayLessons`.
  - `Section.WeeklyCapacity(week, shift, stage)` sums the stage's days.
  - The shift stays the bell schedule; blocked-period grids remain per shift.
  - **Rule:** a section only uses the first N lessons of its day; Phase 3 and the solver must respect it.

## Phase 2.5 suggested curriculum
- **Template (ADR 0036):** the official plan 2026-2027. Each row is mandatory or optional. An optional row is either counted in the official total (`inStatedTotal`: اللغة الكردية) or added on top of it (اللغة الفرنسية، الحاسوب، منهج جرائم حزب البعث). `OfficialTotal()` sums the counted rows, and `Total(chosen)` sums the mandatory rows plus the chosen optional ones.
- **`CurriculumEntry.IsSuggested`:** `CreateSuggested`, `ResetToSuggestion`; cleared by every owner edit (ADR 0029).
- **`DailyDistribution.Suggest(total, days in week order, maxOnDay)`:** even split, extra lessons on the earlier days, capped per day; problems `NoCurriculum`, `BelowWorkingDays`, `AboveShiftCapacity` (ADR 0030).
- **`Stage.ApplySuggestedDayLessons` / `DayLessonsSuggested`:** counts from the suggestion; any owner edit clears the flag, and later suggestions skip such stages.

## Phase 3A: protection and hardening
- **Reference guard (`Application/Common/ReferenceGuard`):** the one place that answers which records depend on a subject, teacher, section, stage, shift, resource or curriculum line. Dependents today: a stage has sections and curriculum lines; a subject has curriculum lines; a shift has sections. Archive needs no active dependent, delete no dependent at all (DECISIONS_PENDING #48).
- **Clearing a curriculum cell** archives the line; restoring it is the undo (#49).
- **`Teacher.DropBlockedOutside(grid)` / `Subject.DropBlockedOutside(grid)`:** remove the blocked slots outside the current `ScheduleGrid` and return them; the version is bumped only when something was removed. Used only after the owner confirms the orphan preview (#50).

## Phase 3B: resources, specializations, profile (ADR 0031)
- **`Resource`:** name, `ResourceKind` (Lab, Field, Hall, Other), `Capacity` 1–20 (sections per slot, per shift), notes, soft archive, `Version`.
- **`Subject.RequiredResourceId`:** at most one resource; existence and active state are checked in Application.
- **`Teacher.Specializations`:** owned `TeacherSpecialization(SubjectId)`, at most 30. `TeacherDetails.SpecializationIds` null keeps them; `AddSpecialization` adds one (no change when already present).
- **`SchedulingProfile`:** one row; five `SchedulingRule(Key, Enabled, Weight 0–100)` in `SchedulingRuleKeys.Defaults` order. `Update` needs every key exactly once; `RestoreDefaults`; `ProfileVersion` and `Version` grow only on a real change.

## Phase 3C: workload (ADR 0032)
- **`WorkloadAssignment(SectionId, CurriculumEntryId, TeacherId)`:** `Reassign` (no change for the same teacher), `Archive`, `Restore`; lessons come from the line.
- **`TeacherAvailability.Compute(slots, offDays, blocked, maxPerDay, maxPerWeek, released)`:** `Slots` (distinct `ShiftSlot(shift, day, lesson)` without off days and blocked lesson numbers), `ByDayLimit` (each day at most max per day), `Available` (also ≤ max per week; 0 when released). `SectionSlots(shift, days, lessonsOn)` lists a section's allowed slots.

## Phase 3D: scheduling input and readiness
- **`SchedulingInput`** is the serializable Application-layer contract for one academic year. It includes working days; active shifts, ordered period/break rows, bell flags and lesson counts; all active stages (including those without sections); sections and their stage-limited allowed slots; subjects, curriculum lines, assignments; teachers and constraints; resources and capacities; and the scheduling profile and `ProfileVersion`.
- The builder uses a fixed set of `IDataStore.Read<T>()` queries and returns lists in canonical order. Archived people/resources are included only when an active assignment or subject requirement still refers to them, so the validator can report the inconsistency. No solver type enters the contract.
- **`InputHash`** is lowercase SHA-256 over compact canonical JSON. Lists are sorted by stable IDs (working days retain week order; slots by day/lesson; profile rules by key). Names, labels and assignment-row IDs are excluded; scheduling-relevant IDs, constraints, counts, assignments, profile rules and `ProfileVersion` are included. `FormatVersion` is part of the hash and is bumped when the contract shape changes. A display-only rename does not invalidate a timetable.
- **Pre-solve validation** is a deterministic pure function over `SchedulingInput`. Findings carry a stable code, `error`/`warning`, entity references, required/available/shortage values, details and fix codes. The UI resolves messages and deep links in Arabic.
- **Soundness policy:** an error is emitted only when demand exceeds a proven upper bound (section capacity, teacher slots/limits, subject slots, resource uses, or consecutive double-period pairs), or an active assignment refers to an archived/fully released teacher. Uncertain, partial-release and stale-grid cases are warnings. The validator may miss conflicts requiring combined constraint reasoning; it must not call a feasible input impossible.
- **Finding codes** are declared once in `FindingCodes` and listed in `FindingCodes.All` (contract: `FindingCodeContractTests`).
- **Generation mode (`ValidatorOptions.DoublePeriodsRequired`, default false):** in the standard mode double periods are a soft preference in Phase 4, so `DOUBLE_PERIOD_IMPOSSIBLE` is a warning whose Arabic text says it will block the «دروس مزدوجة» mode; with the option on it is an error (DECISIONS_PENDING #65).
- The validator reports unassigned section/line cells, over/under-capacity sections, grouped teacher overloads, subject and assignment slot shortages, per-shift resource capacity, break-aware double-period pairs, orphan blocked periods, and structural consistency. Phase 4 consumes this input and hash; solver placement and solver diagnostics remain Phase 4.

## Phase 3E: suggester, wizard step, demo data
- **Assignment suggester (`WorkloadService` suggestions):** deterministic. Candidates are the (section, line) cells with no active assignment, in stage, section, subject, label and line order. Each goes to the specialist with the smallest load-to-limit share after the line (ties by name) who is not fully released and still has room; otherwise the cell stays unassigned with the reason `noSpecialist`, `released` or `capacity`. Existing assignments are never changed, and applying writes exactly the preview.
- **Setup wizard:** eight steps; step 7 «الأنصبة» (assigned/total lines and the suggester) sits before the review. Migration `Phase3EWorkloadWizardStep` moves a saved review (bit 128) to bit 256 and `Down` reverses it.
- **Demo data:** removed on 2026-10-09 (owner decision). Tests create synthetic data inside the test projects only.
