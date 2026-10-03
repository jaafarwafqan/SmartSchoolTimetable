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
| 400 | `INVALID_HOST`, `INVALID_REQUEST` (malformed JSON/body) |
| 401 | `UNAUTHENTICATED`, `INVALID_CREDENTIALS`, `INVALID_RECOVERY_CODE`, `CURRENT_PASSWORD_INCORRECT` |
| 403 | `INVALID_ORIGIN`, `INVALID_LAUNCH_TOKEN`, `REQUEST_FORBIDDEN`, `SETUP_REQUIRED` |
| 404 | `NOT_FOUND` |
| 405 | `METHOD_NOT_ALLOWED` |
| 409 | `SETUP_ALREADY_COMPLETE`, `RECOVERY_MISSING`, `CONFLICT` |
| 415 | `UNSUPPORTED_MEDIA_TYPE` |
| 422 | `VALIDATION_FAILED` (with field codes `REQUIRED`, `USERNAME_TOO_SHORT`, `USERNAME_TOO_LONG`, `PASSWORD_TOO_SHORT`, `PASSWORD_TOO_LONG`, `PASSWORD_MISMATCH`, `INVALID_INACTIVITY_TIMEOUT`), `INVALID_USERNAME`, `INVALID_PASSWORD` |
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

Passwords are 8–1024 characters and usernames are 3–64 characters (`CredentialRules`). Unknown `/api` paths return 404 `NOT_FOUND`, wrong methods return 405 `METHOD_NOT_ALLOWED`, wrong content types return 415 `UNSUPPORTED_MEDIA_TYPE`, and unhandled exceptions return 500 `INTERNAL_ERROR`. The OpenAPI document is served only in the Development environment, and the frontend client is hand-written (ADR 0013).

There is no user/tenant attribution in request logs. Avoid logging credentials, recovery codes, cookies, launch tokens, or timetable personal data.
