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
