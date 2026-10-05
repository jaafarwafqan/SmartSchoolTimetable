# Local API Design

## Boundary
- The API is a private implementation detail of the local application, bound only to `127.0.0.1`.
- The browser UI is served by the same local host and uses same-origin requests.
- No remote clients, public API, tenant identifier, network sync endpoints, or external service integration.
- No role or permission matrix. Authentication distinguishes only an unauthenticated session from the local owner session.

## Local request families
- Setup: first-run owner creation and one-time recovery-code issuance
- Session: login, logout, password change, recovery-code reset/rotation, inactivity lock
- School settings and timetable domain operations
- Local timetable generation and progress
- Local PDF/Excel exports, backup/restore, and imports

## Browser request security
- Canonical `Host` and `Origin` validation for the bound loopback address and active port
- No wildcard CORS; same-origin UI is the supported client
- HttpOnly, SameSite=Strict local session cookie
- Per-launch token required in a dedicated header for state-changing requests
- Error responses must not reveal password hashes, recovery-code hashes, filesystem secrets, or stack traces

## Error model
Every failure returns `{ "code": string, "correlationId": string, "errors": [{ "field", "code" }] }` with an `X-Correlation-ID` header. The body never contains display text, ProblemDetails titles or stack traces.

- Every code is a constant in `src/SmartSchoolTimetable.Application/ErrorCodes.cs`.
- Each code's HTTP status comes from `ApiErrorCodes.StatusByCode` (`src/SmartSchoolTimetable.Api/ApiErrorCodes.cs`).
- The Arabic text for each code lives in `frontend/src/i18n/messages.ts`. `ErrorContractTests` fails if any of these three is missing for a code.
- An unregistered code keeps the original error status. If that status is below 400, the response becomes `500 INTERNAL_ERROR`. An error is never sent with a 2xx status, and the client also treats any 2xx body that carries `code` as an error.

| Status | Codes |
|---|---|
| 400 | `INVALID_HOST`, `INVALID_REQUEST` (malformed JSON/body, or a non-multipart upload) |
| 401 | `UNAUTHENTICATED`, `INVALID_CREDENTIALS`, `INVALID_RECOVERY_CODE`, `CURRENT_PASSWORD_INCORRECT` |
| 403 | `INVALID_ORIGIN`, `INVALID_LAUNCH_TOKEN`, `REQUEST_FORBIDDEN`, `SETUP_REQUIRED` |
| 404 | `NOT_FOUND` |
| 405 | `METHOD_NOT_ALLOWED` |
| 409 | `SETUP_ALREADY_COMPLETE`, `RECOVERY_MISSING`, `CONFLICT` (stale `version`), `RECORD_IN_USE`, `CURRENT_YEAR_REQUIRED`, `YEAR_STRUCTURE_IN_USE`, `STAGE_ARCHIVED`, `NO_CURRENT_YEAR`, `SHIFT_MODE_IN_USE` |
| 413 | `PAYLOAD_TOO_LARGE` (request body over the endpoint limit) |
| 415 | `UNSUPPORTED_MEDIA_TYPE` |
| 422 | `VALIDATION_FAILED` (with field codes `REQUIRED`, `USERNAME_TOO_SHORT`, `USERNAME_TOO_LONG`, `PASSWORD_TOO_SHORT`, `PASSWORD_TOO_LONG`, `PASSWORD_MISMATCH`, `INVALID_INACTIVITY_TIMEOUT`; Phase 2: `VALUE_TOO_LONG`, `VALUE_OUT_OF_RANGE`, `INVALID_OPTION`, `INVALID_DATE`, `INVALID_TIME`, `INVALID_DATE_RANGE`, `DUPLICATE_NAME`, `TERM_OUTSIDE_YEAR`, `TERMS_OVERLAP`, `INVALID_TIME_RANGE`, `PERIODS_OVERLAP`, `PERIODS_NOT_ASCENDING`, `NO_LESSON_PERIODS`, `TOO_MANY_PERIODS`, `NO_WORKING_DAYS`, `BLOCKED_PERIOD_INVALID`, `MAX_PER_DAY_EXCEEDS_PERIODS`, `MAX_PER_WEEK_EXCEEDS_CAPACITY`, `SHIFT_NOT_IN_YEAR`, `ASSET_TOO_LARGE`, `ASSET_TYPE_NOT_ALLOWED`, `ASSET_TYPE_MISMATCH`), `INVALID_USERNAME`, `INVALID_PASSWORD` |
| 429 | `TOO_MANY_REQUESTS` (reserved; no rate limiter exists) |
| 500 | `INTERNAL_ERROR` |

There is no 423/lockout response: failed logins only incur a fixed one-second delay.

## Endpoints (`/api/v1`)
Global rules apply to every route: the exact Host is required (400 `INVALID_HOST`). A present Origin must be canonical, and state-changing requests (POST/PUT/PATCH/DELETE) must carry an Origin (403 `INVALID_ORIGIN`). They also require `X-Local-Launch-Token` (403 `INVALID_LAUNCH_TOKEN`).

| Method | Route | Session required | Request | Success | Endpoint-specific errors |
|---|---|---|---|---|---|
| GET | `/bootstrap` | No | — | 200 `{ setupRequired, authenticated, username, recoveryCodeAcknowledgementRequired, launchToken, inactivityTimeoutMinutes, inactivityTimeoutChoices }` (`inactivityTimeoutMinutes` is the effective value: the owner choice, else the configured default; `null` = never) | — |
| POST | `/auth/setup` | No (only while no owner exists) | `{ username, password, confirmPassword }` | 201 `{ recoveryCode }` + session cookie | 422 `VALIDATION_FAILED`, 422 `INVALID_USERNAME`, 409 `SETUP_ALREADY_COMPLETE` |
| POST | `/auth/login` | No | `{ username, password }` | 204 + session cookie | 422 `VALIDATION_FAILED`, 401 `INVALID_CREDENTIALS`, 403 `SETUP_REQUIRED` |
| POST | `/auth/recovery` | No | `{ recoveryCode, newPassword }` | 200 `{ recoveryCode }` (replacement) + session cookie | 422 `VALIDATION_FAILED`, 401 `INVALID_RECOVERY_CODE`, 403 `SETUP_REQUIRED` |
| POST | `/auth/recovery-code/regenerate` | Yes | `{ currentPassword }` | 200 `{ recoveryCode }` | 401 `UNAUTHENTICATED`, 422 `VALIDATION_FAILED`, 401 `CURRENT_PASSWORD_INCORRECT` |
| POST | `/auth/recovery-code/acknowledge` | Yes | `{}` | 204 | 401 `UNAUTHENTICATED`, 409 `RECOVERY_MISSING` |
| POST | `/auth/logout` | No (revokes the cookie's session if any) | `{}` | 204 + cookie deletion | — |
| POST | `/auth/change-password` | Yes | `{ currentPassword, newPassword }` | 204 + cookie deletion (all sessions revoked) | 401 `UNAUTHENTICATED`, 422 `VALIDATION_FAILED`, 401 `CURRENT_PASSWORD_INCORRECT` |
| PUT | `/settings/inactivity-timeout` | Yes | `{ inactivityTimeout: "5" \| "15" \| "30" \| "60" \| "never" }` | 200 `{ inactivityTimeoutMinutes }` + session cookie with the new lifetime; applies immediately | 401 `UNAUTHENTICATED`, 422 `VALIDATION_FAILED` (field `InactivityTimeout`: `REQUIRED` or `INVALID_INACTIVITY_TIMEOUT`) |
| GET | `/private/status` | Yes | — | 200 `{ status: "authenticated" }` | 401 `UNAUTHENTICATED` |

### School setup (Phase 2, checkpoints 2A–2C)
Every route below requires the owner session (401 `UNAUTHENTICATED`). Editable records carry an integer `version`. Updates, deletes and state changes must send the version that was read; a stale one returns 409 `CONFLICT`, and the UI shows an Arabic reload message. Validation failures are 422 `VALIDATION_FAILED` with per-field codes. Enum values travel as camelCase strings (for example `preparatory`, `arabicIndic`). Dates are `yyyy-MM-dd`.

| Method | Route | Request | Success | Endpoint-specific errors |
|---|---|---|---|---|
| GET | `/school-profile` | — | 200 profile (name, types, people, time zone, numeral system, calendar display, `hasLogo`, `hasStamp`, `version`, allowed `options`) | — |
| PUT | `/school-profile` | `{ name, schoolType, studyType, principalName, scheduleOfficerName, timeZone, numeralSystem, calendarDisplay, version }` | 200 profile | 422, 409 `CONFLICT` |
| GET | `/school-profile/{logo\|stamp}` | — | 200 image bytes with `X-Content-Type-Options: nosniff`, `Content-Security-Policy: default-src 'none'; sandbox` and `Cache-Control: no-store` | 404 `NOT_FOUND` |
| POST | `/school-profile/{logo\|stamp}` | multipart: `file`, `version` | 200 profile | 400 `INVALID_REQUEST`, 413 `PAYLOAD_TOO_LARGE`, 422 (`File`: `REQUIRED`, `ASSET_TOO_LARGE`, `ASSET_TYPE_NOT_ALLOWED`, `ASSET_TYPE_MISMATCH`), 409 `CONFLICT`, 404 |
| DELETE | `/school-profile/{logo\|stamp}?version=` | — | 200 profile | 404, 409 |
| GET | `/school-context` | — | 200 `{ schoolName, numeralSystem, calendarDisplay, timeZone, currentYear?, currentTerm? }`; drives the top bar and the formatter | — |
| GET | `/dashboard-summary` | — | 200 `{ counts: [{ key, value }], checklist: [{ key, done }] }`, computed from stored data only | — |
| GET | `/academic-years?search=&sort=&page=&pageSize=` | sort: `label`, `-label`, `startDate`, `-startDate` (default `-startDate`); `pageSize` ≤ 100 | 200 `{ items, total, page, pageSize }` | — |
| GET | `/academic-years/{id}` | — | 200 year with terms | 404 |
| POST | `/academic-years` | `{ label, startDate, endDate, version: 0, copyStructureFromYearId? }` | 201 year (the first year becomes current) | 422 (`DUPLICATE_NAME`, `INVALID_DATE_RANGE`, …) |
| PUT | `/academic-years/{id}` | same shape with the read `version` | 200 year | 404, 409, 422 (`TERM_OUTSIDE_YEAR` if a term would fall outside) |
| DELETE | `/academic-years/{id}?version=` | — | 204 | 404, 409 `CONFLICT`, 409 `RECORD_IN_USE` (structure exists), 409 `CURRENT_YEAR_REQUIRED` (the current year while other years exist) |
| POST | `/academic-years/{id}/make-current` | `{ version }` | 200 year | 404, 409 |
| POST | `/academic-years/{id}/terms` | `{ name, startDate, endDate, version }` (the year's version) | 200 year | 404, 409, 422 (`TERM_OUTSIDE_YEAR`, `TERMS_OVERLAP`, `DUPLICATE_NAME`) |
| PUT | `/academic-years/{id}/terms/{termId}` | same | 200 year | 404, 409, 422 |
| DELETE | `/academic-years/{id}/terms/{termId}?version=` | — | 200 year | 404, 409 |
| POST | `/academic-years/{id}/terms/{termId}/make-current` | `{ version }` | 200 year | 404, 409 |
| GET | `/working-days` | — | 200 `{ days, weekStartDay, version }` (ISO weekdays 1=Monday … 7=Sunday) | 401 |
| PUT | `/working-days` | `{ days, weekStartDay, version }` | 200 working week | 401, 409 `CONFLICT`, 422 `NO_WORKING_DAYS`, `INVALID_OPTION` |
| GET | `/academic-years/{yearId}/shifts?search=&sort=&page=&pageSize=` | sort: `name`, `-name`, `order`, `-order` | 200 paged shifts with periods | 401, 404 |
| POST | `/academic-years/{yearId}/shifts` | `{ name, displayOrder, version: 0 }` | 201 shift | 401, 404, 409, 422 |
| PUT | `/academic-years/{yearId}/shifts/{id}` | `{ name, displayOrder, version }` | 200 shift | 401, 404, 409, 422 |
| DELETE | `/academic-years/{yearId}/shifts/{id}?version=` | — | 204 | 401, 404, 409 `CONFLICT`, 409 `RECORD_IN_USE` while a section (archived included) uses the shift |
| PUT | `/academic-years/{yearId}/shifts/{id}/periods` | `{ periods: [{ kind, startTime, endTime, startBell, endBell }], version }` | 200 shift | 401, 404, 409, 422 (`NO_LESSON_PERIODS`, `TOO_MANY_PERIODS`, `PERIODS_OVERLAP`, `PERIODS_NOT_ASCENDING`, `INVALID_TIME_RANGE`) |
| POST | `/academic-years/{yearId}/shifts/generate-periods` | `{ firstStartTime, lessonMinutes, lessonCount, breakMinutes, breakAfterLesson }` | 200 editable period drafts; does not save | 401, 422 |
| GET | `/bell-settings` | — | 200 `{ tone, breakBell, version }` | 401 |
| PUT | `/bell-settings` | `{ tone, breakBell, version }` | 200 bell settings | 401, 409, 422 |
| GET | `/academic-years/{yearId}/stages?search=&sort=&page=&pageSize=&includeArchived=` | paged stage list; default excludes archived rows | 200 `{ items, total, page, pageSize }` | 401 |
| POST | `/academic-years/{yearId}/stages` | `{ name, displayOrder, version: 0 }` | 201 stage | 401, 404, 409, 422 `DUPLICATE_NAME` |
| PUT | `/academic-years/{yearId}/stages/{id}` | `{ name, displayOrder, version }` | 200 stage | 401, 404, 409, 422 |
| DELETE | `/academic-years/{yearId}/stages/{id}?version=` | — | 204 | 401, 404, 409 `CONFLICT`, 409 `RECORD_IN_USE` while the stage has sections (archive it instead) |
| POST | `/academic-years/{yearId}/stages/{id}/archive` | `{ version }` | 200 archived stage | 401, 404, 409 `RECORD_IN_USE` if active sections exist |
| POST | `/academic-years/{yearId}/stages/{id}/restore` | `{ version }` | 200 restored stage | 401, 404, 409 |
| GET | `/academic-years/{yearId}/stages/{stageId}/sections?search=&sort=&page=&pageSize=&includeArchived=` | paged sections with computed `weeklyCapacity` | 200 | 401, 404 |
| POST | `/academic-years/{yearId}/stages/{stageId}/sections` | `{ label, shiftId, studentCount?, version: 0 }` | 201 section | 401, 404, 422 (`DUPLICATE_NAME`, `SHIFT_NOT_IN_YEAR`) |
| PUT | `/academic-years/{yearId}/stages/{stageId}/sections/{id}` | `{ label, shiftId, studentCount?, version }` | 200 section | 401, 404, 409, 422 |
| DELETE | `/academic-years/{yearId}/stages/{stageId}/sections/{id}?version=` | — | 204 | 401, 404, 409 `CONFLICT` |
| POST | `/academic-years/{yearId}/stages/{stageId}/sections/{id}/archive` or `/restore` | `{ version }` | 200 section | 401, 404, 409 |

Creating a section in an archived stage, or restoring a section of an archived stage, returns 409 `STAGE_ARCHIVED`. Display order is a sort key; equal orders are allowed and sort by name. Duplicate names (after Arabic normalization) return 422 `DUPLICATE_NAME` on the name/label field. Audit events: `ShiftCreated/Updated/Deleted`, `ShiftPeriodsUpdated`, `WorkingWeekUpdated`, `BellSettingsUpdated`, `StageCreated/Updated/Archived/Restored/Deleted`, `SectionCreated/Updated/Archived/Restored/Deleted`.

Audit events: `SchoolProfileUpdated`, `SchoolAssetUploaded`, `SchoolAssetRemoved`, `AcademicYearCreated`, `AcademicYearUpdated`, `AcademicYearDeleted`, `AcademicYearMadeCurrent`, `TermCreated`, `TermUpdated`, `TermDeleted`, `TermMadeCurrent`.

### Phase 2.5B: per-day lessons, shift mode, setup progress
| Method | Route | Request | Success | Errors |
|---|---|---|---|---|
| PUT | `/academic-years/{yearId}/shifts/{id}/day-lessons` | `{ dayLessons: [{ day, lessons }], version }`: 0..lesson count per working day | 200 shift; shifts now also carry `kind` (`morning`, `evening` or `other`), `dayLessons` for every working day, and `weeklyLessons` | 401, 404, 409 `CONFLICT`, 422 `DayLessons`: `INVALID_OPTION` (not a working day), `DUPLICATE_NAME` (day repeated), `VALUE_OUT_OF_RANGE` |
| GET | `/shift-mode/impact?mode=morning\|evening\|dual` | — | 200 `{ mode, allowed, shiftsToCreate, shiftsToRemove, affectedSections: [{ sectionId, stageName, label, shiftName, isArchived }] }` | 401, 409 `NO_CURRENT_YEAR`, 422 `Mode` |
| PUT | `/shift-mode` | `{ mode, version }` (school-profile version) | 200 `{ mode, shifts, profileVersion }`; creates or adopts morning/evening shifts of the current year, removes unneeded unused ones | 401, 409 `CONFLICT`, 409 `NO_CURRENT_YEAR`, 409 `SHIFT_MODE_IN_USE`, 422 |
| GET | `/setup-progress` | — | 200 `{ currentStep, completedSteps, skippedSteps, isFinished, schoolType, shiftMode, version }` | 401 |
| PUT | `/setup-progress` | `{ currentStep (1–7), completedSteps, skippedSteps, isFinished, version }` | 200 progress | 401, 409 `CONFLICT`, 422 (`VALUE_OUT_OF_RANGE`) |

`/schedule-grid` now also returns `lessonsByDay: [{ day, lessons }]` and `maxWeeklyLessons`. Audit events: `ShiftDayLessonsUpdated`, `ShiftModeChanged`, `SetupProgressSaved`, `SetupFinished`.

### Phase 2.5C: stage cards, curriculum, templates
Every `…/preview` route returns the plan without saving; its twin without `/preview` applies it (ADR 0022). Plan line actions: `create`, `update`, `exists`, `unchanged`, `ambiguous`, `notApplicable`.

| Method | Route | Request | Success | Errors |
|---|---|---|---|---|
| GET | `/academic-years/{yearId}/stage-cards?includeArchived=` | — | 200 `[{ stage, sections }]`; stages now carry `templateKey` | 401 |
| PUT | `/academic-years/{yearId}/stage-cards/{stageId}/section-count` | `{ count (0–30), shiftId, labelStyle: arabic\|numbers\|latin }` | 200 card; adds the next labels or removes the last sections | 401, 404, 409 `STAGE_ARCHIVED`, 422 `Count`, `LabelStyle`, `ShiftId` (`SHIFT_NOT_IN_YEAR`) |
| GET | `/academic-years/{yearId}/curriculum` | — | 200 `{ stages: [{ id, name, plannedLessons, totals: [{ shiftId, shiftName, sections, weeklyCapacity, status, difference }] }], rows: [{ subjectId, subjectName, colorIndex, label, cells: [{ stageId, entryId, weeklyLessons, version, duplicates }] }] }`; status is `under`, `equal` or `over` | 401 |
| PUT | `/academic-years/{yearId}/curriculum/cell` | `{ stageId, subjectId, label, weeklyLessons (1–15, or null to clear), entryId, version }` | 200 table | 401, 404, 409 `CONFLICT`, 409 `STAGE_ARCHIVED`, 422 `SubjectId`, `WeeklyLessons`, `Label` |
| POST | `/academic-years/{yearId}/curriculum/copy[/preview]` | `{ fromStageId, toStageIds }` | 200 plan; creates only missing lines | 401, 404 |
| POST | `/academic-years/{yearId}/curriculum/set-across[/preview]` | `{ subjectId, label, weeklyLessons, stageIds }` | 200 plan | 401, 422 `SubjectId`, `WeeklyLessons` |
| PUT | `/curriculum-entries/{id}` | `{ weeklyLessons, label, needsDoublePeriod, notes, version }` | 200 entry | 401, 404, 409 `CONFLICT`, 422 |
| POST | `/curriculum-entries/{id}/archive`, `/restore` | `{ version }` | 200 entry | 401, 404, 409 `CONFLICT` |
| DELETE | `/curriculum-entries/{id}?version=` | — | 204 | 401, 404, 409 `CONFLICT` |
| GET | `/templates` | — | 200 `{ branches, grades: [{ key, name, branchStem, schoolTypes }], periodPresets, workingDayPresets }` | 401 |
| POST | `/academic-years/{yearId}/templates/stages[/preview]` | `{ schoolType, grades: [{ gradeKey, branches, sections, shiftId, labelStyle }] }` | 200 `{ lines: [{ key, name, action, existingSections, sectionsToAdd }], changes }`; one transaction | 401, 404, 422 `SchoolType`, and any error of the services it calls (everything rolled back) |
| GET | `/academic-years/{yearId}/templates/suggested-subjects` | — | 200 subject names | 401 |
| POST | `/templates/subjects[/preview]` | `{ names }` | 200 `{ lines: [{ name, action }], changes }` | 401, 422 |

`POST …/shifts/generate-periods` also accepts `breaks: [{ afterLesson, minutes }]` (period presets with several breaks). Stage and subject archive/delete can now return 409 `CURRICULUM_IN_USE`. Audit events: `SectionsAdded`, `SectionsRemoved`, `CurriculumEntryCreated`, `CurriculumEntryUpdated`, `CurriculumEntryDeleted`, `CurriculumEntryArchived`, `CurriculumEntryRestored`, `CurriculumCopied`, `CurriculumLessonsSet`.

### Phase 2.5D: setup wizard
Each step is one transaction through the normal services, and records the step in the setup progress (ADR 0023). All return 200 with the setup progress.

| Method | Route | Request | Errors |
|---|---|---|---|
| PUT | `/setup-wizard/school` | `{ name, schoolType, shiftMode, principalName }`; with a current year, the shifts follow the mode | 401, 403, 409 `CONFLICT`, 409 `SHIFT_MODE_IN_USE`, 422 (`Name`, `SchoolType`, `StudyType`) |
| PUT | `/setup-wizard/year` | `{ label, startDate, endDate, terms: [{ name, startDate, endDate }] }`; reuses a year with the same label, makes it current, adds or updates terms by name, sets a current term | 401, 409, 422 (year and term validation codes) |
| PUT | `/setup-wizard/timing` | `{ days, weekStartDay, shifts: [{ kind, firstStartTime, lessonMinutes, lessonCount, breaks, dayLessons }] }`; working days, the mode's shifts, generated periods and per-day counts | 401, 409 `NO_CURRENT_YEAR`, 409 `CONFLICT`, 422 (`Shifts` `INVALID_OPTION`, generator and period codes) |
| GET | `/setup-wizard/review` | — ; 200 `{ schoolName, yearLabel, shifts, stages, sections, subjects, curriculumLines, teachers, warnings: [{ code: noSections\|emptyCurriculum\|under\|over, stageName, shiftName, value }] }` | 401 |

`/dashboard-summary` now also returns `curriculum` (the stages with planned lessons and per-shift totals, as in `/curriculum`) and `setupFinished`.

### Phase 2.5 fixes 2: breaks and lessons per stage
| Method | Route | Request | Success | Errors |
|---|---|---|---|---|
| POST | `/academic-years/{yearId}/shifts/generate-periods` | also `breaks` (up to 3 × `{ afterLesson, minutes }`) and `gapMinutes` (0–30) | 200 generated periods | 422 `Breaks`, `GapMinutes`, `BreakAfterLesson`, `BreakMinutes` |
| PUT | `/academic-years/{yearId}/stages/{id}/day-lessons` | `{ dayLessons: [{ day, lessons }], version }`; an empty list inherits the shift | 200 stage; stages now carry `dayLessons` | 401, 403, 404, 409 `CONFLICT`, 409 `STAGE_ARCHIVED`, 422 `DayLessons` (`INVALID_OPTION`, `DUPLICATE_NAME`, `VALUE_OUT_OF_RANGE`) |
| POST | `/academic-years/{yearId}/shifts/{id}/day-lessons/impact` | `{ dayLessons, version }` | 200 `[{ stageId, stageName, day, stageLessons, shiftLessons }]`; nothing is saved | 401, 404, 422 |
| PUT | `/academic-years/{yearId}/shifts/{id}/day-lessons` | also `confirmStageChanges` (default false) | 200 shift; with confirmation, the affected stages are lowered | 409 `STAGE_LESSONS_ABOVE_SHIFT` when stages would exceed the shift without confirmation |

`/templates` also returns `breakDefaults: { minutes: { primary, intermediate, preparatory, secondary, other } }` (suggestions). The wizard timing step accepts `gapMinutes` per shift. Section `weeklyCapacity`, curriculum totals, the dashboard and the review use the stage's own counts. Audit events: `StageDayLessonsUpdated`, `StageDayLessonsLowered`.

### Phase 2.5: suggested curriculum and daily distribution
| Method | Route | Request | Success | Errors |
|---|---|---|---|---|
| POST | `/academic-years/{yearId}/curriculum/suggested/preview` | `{ optionalSubjects: [] }` | 200 `{ provenance, subjects: [{ name, action: create\|exists, existingName, optional, included }], stages: [{ stageId, stageName, needsReview, statedTotal, suggestedTotal, currentTotal, resultingTotal, entries: [{ subject, lessons, action, optional, currentLessons }] }], optionalSubjects, changes }` | 401, 403, 404 |
| POST | `/academic-years/{yearId}/curriculum/suggested` | same | 200 plan; adds only missing subjects and (stage, subject) lines, marked suggested; idempotent; one transaction | 401, 403, 404, 409, 422 |
| POST | `/academic-years/{yearId}/curriculum/suggested/stages/{stageId}/reset/preview` | `{ optionalSubjects }` | 200 before/after (`update`, `unchanged`, `create` with `currentLessons`) | 401, 404 |
| POST | `/academic-years/{yearId}/curriculum/suggested/stages/{stageId}/reset` | `{ optionalSubjects, confirm: true }` | 200 | 401, 404, 422 `Confirm` `REQUIRED` |
| GET | `/academic-years/{yearId}/daily-suggestion` | — | 200 `{ stages: [{ stageId, stageName, weeklyTotal, capacity, suggested: [{ day, lessons }], current, status: apply\|same\|manual\|aboveCapacity\|belowDays\|noCurriculum, changedSinceSuggestion, version }] }` | 401 |
| POST | `/academic-years/{yearId}/daily-suggestion` | `{ stageIds }` | 200 updated suggestion; manual stages are skipped | 401, 403, 409 `DAILY_TOTAL_ABOVE_SHIFT`, 422 `StageIds` |

Curriculum cells and entries carry `isSuggested`. Audit events: `SuggestedCurriculumApplied`, `SuggestedCurriculumStageReset`, `StageDayLessonsSuggested`.

### Phase 3A: reference protection, soft clear, orphan blocked periods
| Method | Route | Request | Success | Errors |
|---|---|---|---|---|
| GET | `/references/{kind}/{id}` | `kind`: `subject`, `teacher`, `section`, `stage`, `shift`, `resource`, `curriculumEntry` | 200 `{ kind, id, dependents: [{ kind: section\|curriculumEntry, active, archived, samples (≤ 5 names), errorCode }], archiveBlockedBy, deleteBlockedBy }` | 401, 404 (unknown kind) |
| GET | `/blocked-periods/orphans/` | — | 200 `{ owners: [{ kind: teacher\|subject, id, name, version, periods: [{ day, lessonNumber }] }], total }` | 401 |
| POST | `/blocked-periods/orphans/clean` | `{ owners: [{ kind, id, version }] }` | 200 the remaining report; removes orphan slots only from the listed records | 401, 403, 404, 409 `CONFLICT` |

- **Every delete and archive** of a stage, section, subject, teacher, shift and curriculum line asks the reference guard first. Archive is refused while an active dependent exists, delete while any dependent exists: `409 RECORD_IN_USE` (sections) or `409 CURRICULUM_IN_USE` (curriculum lines).
- **`PUT /academic-years/{yearId}/curriculum/cell` with `weeklyLessons: null`** archives the line instead of deleting it. The table response carries it as `cleared` (`{ id, version, … }`); undo is `POST /curriculum-entries/{id}/restore` with that version (`409 STAGE_ARCHIVED` when the stage was archived meanwhile).
- Audit events: `CurriculumEntryCleared`, `OrphanBlockedPeriodsRemoved`.

### Phase 3B: resources, specializations, scheduling profile
| Method | Route | Request | Success | Errors |
|---|---|---|---|---|
| GET | `/resources/` | `search`, `sort` (`name`, `kind`, `capacity`, `-` for descending), `page`, `pageSize`, `includeArchived` | 200 paged `{ id, name, kind: lab\|field\|hall\|other, capacity, notes, isArchived, archivedAt, version }` | 401 |
| POST | `/resources/` | `{ name, kind, capacity (default 1), notes, version: 0 }` | 201 | 401, 403, 422 (`Name` `DUPLICATE_NAME`, `Kind` `INVALID_OPTION`, `Capacity` `VALUE_OUT_OF_RANGE`) |
| PUT | `/resources/{id}` | same with the read `version` | 200 | 401, 403, 404, 409 `CONFLICT`, 422 |
| POST | `/resources/{id}/archive`, `/restore` | `{ version }` | 200 | 401, 403, 404, 409 `CONFLICT`, 409 `RESOURCE_IN_USE` (an active subject requires it) |
| DELETE | `/resources/{id}?version=` | — | 204 | 401, 403, 404, 409 `CONFLICT`, 409 `RESOURCE_IN_USE` (any subject requires it) |
| POST | `/teachers/{id}/specializations/{subjectId}` | `{ version }` | 200 teacher («إضافة المادة لتخصصاته»; adding twice changes nothing) | 401, 403, 404, 409, 422 `SubjectId` `INVALID_OPTION` |
| GET | `/scheduling-profile/` | — | 200 `{ rules: [{ key, enabled, weight, enabledByDefault, defaultWeight }], profileVersion, isDefault, version }` | 401 |
| PUT | `/scheduling-profile/` | `{ rules: [{ key, enabled, weight }], version }` (every rule once, weight 0–100) | 200; `profileVersion` + 1 when something changed | 401, 403, 409 `CONFLICT`, 422 `Rules` |
| POST | `/scheduling-profile/restore-defaults` | `{ confirm: true, version }` | 200 defaults; `profileVersion` + 1 | 401, 403, 409, 422 `Confirm` `REQUIRED` |

- **Subjects** carry `requiredResourceId` (DTO and save command). A newly chosen resource must be active: `422 RequiredResourceId INVALID_OPTION`.
- **Teachers** carry `specializationIds`. The save command's `specializationIds` is optional: null keeps the list. New ids must be active subjects (`422 SpecializationIds INVALID_OPTION`).
- **`GET /references/resource/{id}`** lists the subjects that require the resource (dependent kind `subject`).
- Audit events: `ResourceCreated`, `ResourceUpdated`, `ResourceArchived`, `ResourceRestored`, `ResourceDeleted`, `TeacherSpecializationAdded`, `SchedulingProfileUpdated`, `SchedulingProfileDefaultsRestored`.

### Phase 3C: workload assignments (ADR 0032)
| Method | Route | Request | Success | Errors |
|---|---|---|---|---|
| GET | `/academic-years/{yearId}/workload/matrix?stageId=` | — | 200 `{ stages: [{ stageId, stageName, assignedCells, totalCells }], stage: { stageId, stageName, lines: [{ entryId, subjectId, subjectName, colorIndex, label, weeklyLessons }], sections: [{ sectionId, label, shiftName, assignedLines, totalLines, assignedLessons, totalLessons, cells: [{ entryId, assignmentId, teacherId, version, outsideSpecialization }] }] } }` (the first stage when `stageId` is omitted) | 401, 404 |
| GET | `/academic-years/{yearId}/workload/teachers` | — | 200 `[{ teacherId, fullName, shortName, specializationIds, assignedLessons, maxPerWeek, available, limit, status: within\|near\|over, released, assignments: [...], version }]` | 401, 404 |
| PUT | `/academic-years/{yearId}/workload/cell` | `{ sectionId, entryId, teacherId (null clears), assignmentId, version }` | 200 the stage matrix | 401, 403, 409 `CONFLICT`, 422 `SectionId`/`EntryId`/`TeacherId` `INVALID_OPTION` |
| POST | `/academic-years/{yearId}/workload/bulk/across-stage[/preview]` | `{ teacherId, entryId, overwrite }` | 200 plan `{ lines: [{ sectionId, stageName, sectionLabel, entryId, subjectName, label, weeklyLessons, currentTeacher, newTeacher, action }], changes, loads: [{ teacherId, fullName, before, after, limit }] }` | 401, 403, 404, 422 |
| POST | `/academic-years/{yearId}/workload/bulk/class-teacher[/preview]` | `{ teacherId, sectionId, entryIds ([] = every line), overwrite }` | 200 plan | 401, 403, 404, 422 |
| POST | `/academic-years/{yearId}/workload/bulk/transfer[/preview]` | `{ fromTeacherId, toTeacherId }` | 200 plan (`transfer` lines) | 401, 403, 404, 422 `ToTeacherId` |
| POST | `/academic-years/{yearId}/workload/bulk/remove[/preview]` | `{ teacherId }` | 200 plan (`remove` lines; archived) | 401, 403, 404, 422 |

- Plan actions: `create`, `replace` (only with `overwrite`), `skip`, `unchanged`, `transfer`, `remove`. The applied plan equals its preview; applying again gives `changes: 0`.
- **`409 WORKLOAD_IN_USE`:**
  - archive or delete of a teacher or section with assignments;
  - the section stepper removing a section with assignments;
  - clearing a curriculum cell (`PUT …/curriculum/cell` with `weeklyLessons: null`) or archiving a line (`POST /curriculum-entries/{id}/archive`) with active assignments, unless `confirmWorkload: true`. With confirmation the assignments are archived with the line, and `POST /curriculum-entries/{id}/restore` restores them.
- **`GET /references/{teacher|section|curriculumEntry}/{id}`** lists the assignments (dependent kind `workloadAssignment`, «المرحلة / الشعبة: المادة — المعلم»).
- Audit events: `WorkloadAssigned`, `WorkloadReassigned`, `WorkloadCleared`, `WorkloadAssignedAcrossStage`, `WorkloadClassTeacher`, `WorkloadTransferred`, `WorkloadRemoved`, `WorkloadArchivedWithLine`.

### Subjects and the schedule grid (Phase 2, checkpoint 2D)
| Method | Route | Request | Success | Endpoint-specific errors |
|---|---|---|---|---|
| GET | `/schedule-grid` | — | 200 `{ days, lessonsPerDay }`: working days in display order and the most lessons per day of any shift in the current year (0 until periods exist) | 401 |
| GET | `/subjects?search=&sort=&page=&pageSize=&includeArchived=` | sort: `name`, `-name`, `priority`, `-priority` | 200 paged subjects | 401 |
| POST | `/subjects` | `{ name, colorIndex (1–10), priority (1–5), distributionEnabled, spreadAcrossDays, heavy, requiresDoublePeriod, blockedPeriods: [{ day, lessonNumber }], notes?, version: 0 }` | 201 subject | 401, 422 (`DUPLICATE_NAME`, `VALUE_OUT_OF_RANGE` on `ColorIndex`/`Priority`, `VALUE_TOO_LONG`, `BLOCKED_PERIOD_INVALID` when a slot is outside the schedule grid) |
| PUT | `/subjects/{id}` | same, with the read `version` | 200 subject | 401, 404, 409 `CONFLICT`, 422 |
| POST | `/subjects/{id}/archive` or `/restore` | `{ version }` | 200 subject | 401, 404, 409 |
| DELETE | `/subjects/{id}?version=` | — | 204 (nothing references subjects before Phase 3) | 401, 404, 409 |

Quick add: `colorIndex: 0` picks the next unused palette colour (cycling after ten), and `priority: 0` means the default 3. A teacher created with an empty `shortName` gets a proposed one; if none is free, `ShortName` returns `REQUIRED`.

Audit events: `SubjectCreated`, `SubjectUpdated`, `SubjectArchived`, `SubjectRestored`, `SubjectDeleted`. The dashboard adds the counts `subjects` and `capacityGaps` and the checklist step `subjects`.

### Teachers (Phase 2, checkpoint 2E)
| Method | Route | Request | Success | Endpoint-specific errors |
|---|---|---|---|---|
| GET | `/teachers?search=&sort=&page=&pageSize=&includeArchived=&released=` | search matches the full or short name (normalized); sort: `name`, `-name`, `shortName`, `-shortName`; `released=true` lists fully released teachers only | 200 paged teachers | 401 |
| POST | `/teachers` | `{ fullName, shortName, offDays: [ISO day], blockedPeriods: [{ day, lessonNumber }], fullyReleased, releaseReason?, releaseFrom?, releaseTo?, maxLessonsPerDay?, maxLessonsPerWeek?, notes?, version: 0 }` | 201 teacher | 401, 422 (`DUPLICATE_NAME` on `ShortName`, `INVALID_OPTION` on `OffDays` for a non-working day, `BLOCKED_PERIOD_INVALID`, `MAX_PER_DAY_EXCEEDS_PERIODS`, `MAX_PER_WEEK_EXCEEDS_CAPACITY`, `VALUE_OUT_OF_RANGE`, `INVALID_DATE`, `INVALID_DATE_RANGE`) |
| PUT | `/teachers/{id}` | same, with the read `version` | 200 teacher | 401, 404, 409 `CONFLICT`, 422 |
| POST | `/teachers/{id}/archive` or `/restore` | `{ version }` | 200 teacher | 401, 404, 409 |
| DELETE | `/teachers/{id}?version=` | — | 204 (nothing references teachers before Phase 3) | 401, 404, 409 |
| POST | `/teachers/bulk/preview` | `{ names: [string] }` (≤ 200 lines; blank lines ignored) | 200 `{ lines: [{ line, fullName, shortName?, status }], readyCount }`; status is `ready`, `tooLong`, `duplicateInList`, `exists` or `noShortName` | 401, 422 `REQUIRED` / `VALUE_OUT_OF_RANGE` on `Names` |
| POST | `/teachers/bulk` | `{ names: [string] }` | 200 `{ created }`: every line is re-classified and the whole batch is refused if any line is not ready | 401, 422 (`Names[i]`: `DUPLICATE_NAME` or `VALUE_TOO_LONG`) |

Release reason and dates are kept only while `fullyReleased` is true. Limits are optional (no value means no limit). Audit events: `TeacherCreated`, `TeacherUpdated`, `TeacherArchived`, `TeacherRestored`, `TeacherDeleted`, `TeachersBulkCreated`. The dashboard adds the count `teachers` and the checklist step `teachers`.

### Academic calendar (Phase 2, checkpoint 2F)
| Method | Route | Request | Success | Endpoint-specific errors |
|---|---|---|---|---|
| GET | `/calendar-days?search=&sort=&page=&pageSize=&from=&to=` | sort: `startDate` (default), `-startDate`, `title`, `-title`; `from`/`to` (yyyy-MM-dd) return entries overlapping the window (month view) | 200 paged `{ id, title, startDate, endDate, kind, affectsSchedule, outsideCurrentYear, version }` | 401, 422 `INVALID_DATE` on `From`/`To` |
| POST | `/calendar-days` | `{ title, startDate, endDate?, kind, affectsSchedule, version: 0 }`; kind is `officialHoliday`, `schoolHoliday`, `exam` or `specialDay`; no `endDate` means one day | 201 calendar day | 401, 422 (`REQUIRED`, `VALUE_TOO_LONG`, `INVALID_DATE`, `INVALID_DATE_RANGE`, `VALUE_OUT_OF_RANGE` above 366 days, `INVALID_OPTION`) |
| PUT | `/calendar-days/{id}` | same, with the read `version` | 200 calendar day | 401, 404, 409 `CONFLICT`, 422 |
| DELETE | `/calendar-days/{id}?version=` | — | 204 | 401, 404, 409 |

Entries outside the current academic year are saved; `outsideCurrentYear: true` is a warning, not an error (spec 2.11). Calendar days are never copied to a new year. Audit events: `CalendarDayCreated`, `CalendarDayUpdated`, `CalendarDayDeleted`.

Passwords are 8–1024 characters and usernames are 3–64 characters (`CredentialRules`). Unknown `/api` paths return 404 `NOT_FOUND`, wrong methods return 405 `METHOD_NOT_ALLOWED`, wrong content types return 415 `UNSUPPORTED_MEDIA_TYPE`, and unhandled exceptions return 500 `INTERNAL_ERROR`. The OpenAPI document is served only in the Development environment, and the frontend client is hand-written (ADR 0013).

There is no user/tenant attribution in request logs. Avoid logging credentials, recovery codes, cookies, launch tokens, or timetable personal data.
