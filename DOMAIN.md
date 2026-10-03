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

## Implemented in Phase 2 (checkpoint 2A)
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
- **Planned for 2B–2E** (recorded in `docs/DECISIONS_PENDING.md`):
  - Shifts, lesson periods, stages and sections belong to an academic year; subjects and teachers are global.
  - Weekdays use ISO numbering (1 = Monday … 7 = Sunday); the default working days are Sunday–Thursday.

