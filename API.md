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
| 409 | `SETUP_ALREADY_COMPLETE`, `RECOVERY_MISSING`, `CONFLICT` (stale `version`), `RECORD_IN_USE`, `CURRENT_YEAR_REQUIRED`, `YEAR_STRUCTURE_IN_USE`, `STAGE_ARCHIVED` |
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

Passwords are 8–1024 characters and usernames are 3–64 characters (`CredentialRules`). Unknown `/api` paths return 404 `NOT_FOUND`, wrong methods return 405 `METHOD_NOT_ALLOWED`, wrong content types return 415 `UNSUPPORTED_MEDIA_TYPE`, and unhandled exceptions return 500 `INTERNAL_ERROR`. The OpenAPI document is served only in the Development environment, and the frontend client is hand-written (ADR 0013).

There is no user/tenant attribution in request logs. Avoid logging credentials, recovery codes, cookies, launch tokens, or timetable personal data.
