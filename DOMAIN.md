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
- SyncEvent
- ConflictRecord

## Invariants
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
- TenantProvisioned

## Non-goals in Phase 0
No implementation of domain logic; only specification and validation strategy.
