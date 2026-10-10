# Phase 1 acceptance criteria and test mapping

Moved from DELIVERY_PLAN.md (M0) so the plan can describe the whole delivery; Phase 1 was delivered and tagged. The tests named here still exist.

## Local foundation and owner authentication
- ASP.NET Core local application, SQLite/WAL, clean architecture, and a same-origin React 19/strict TypeScript/Vite frontend served from API `wwwroot`.
- Exactly one owner account. No RBAC, permission matrix, refresh tokens, remote API, or multi-user concurrency support.
- Setup creates and signs in the owner. One-time recovery-code acknowledgment gates the first entry; after interrupted acknowledgment, sign in and regenerate a code from Settings with the current password. Password changes are optional and require the current password.
- PBKDF2-HMAC-SHA-256, 600,000 iterations, 8–1024 character passwords (minimum lowered from 12 in Phase 1.3), fixed one-second delay after failed login, no temporary lockout or escalating delay, local cookie session, and configurable inactivity lock (`1-1440` minutes or `Never`).
- Kestrel binds only to `127.0.0.1`; strict Host/Origin checks, no wildcard CORS, SameSite=Strict HttpOnly cookie, and per-launch token on state-changing requests.
- Lightweight local audit history without user attribution.
- Arabic-only user-facing UI and generated text, unified API error codes, lucide-react icons, accessible RTL forms, and lint/test enforcement.

## Phase 1 acceptance criteria
- First-run setup creates exactly one owner, automatically signs in, shows the recovery code once, and requires acknowledgment before first entry. A reload before acknowledgment shows a blocking "recovery code not confirmed" screen (no application route is reachable) whose only actions are logout and regenerating a code with the current password; regeneration invalidates the old code and the new code must be acknowledged before entry.
- Password and recovery code are never stored/logged in clear text. Recovery code resets a password once and is replaced; no alternate recovery path exists. No forced password change.
- Password change requires the current password. Failed login applies only a fixed one-second delay; tests use an injected delay rather than waiting. Logout and configurable inactivity auto-lock, including `Never`, are tested.
- Authenticated application routes remain inaccessible before login and while the recovery code is unacknowledged.
- Automated startup test inspects the actual address and fails unless it is exactly `127.0.0.1`; non-loopback/wildcard/LAN binding configurations are rejected.
- Request-security tests reject invalid/missing Origin on state-changing requests, invalid Host, absent/stale launch tokens, malicious cross-origin attempts, and DNS-rebinding-style Host changes. No wildcard CORS exists. Cookie attributes are tested; `Secure` is intentionally not required on loopback HTTP.
- SQLite migration, WAL startup, single-owner invariant, and local audit history are tested.
- The API returns only stable error codes, correlation IDs, and field/code parameters for every failure path, including framework/model binding, FluentValidation, Origin/token, database, unknown route/method/media, and unhandled failures. The client never receives default ProblemDetails titles or user-readable API prose.
- Every API code is covered by the Arabic dictionary. Unknown/missing codes and network/offline/timeout failures use Arabic fallbacks. Playwright major error paths render no Latin prose. The HTML root is `lang="ar" dir="rtl"`; forms disable native validation.
- ESLint prevents hard-coded UI strings and buttons without an icon plus visible label. Password visibility controls expose Arabic labels/title and `aria-pressed`. Only lucide-react is used.

### Phase 1 acceptance test mapping
| Acceptance criterion | Proving test(s) |
|---|---|
| First-run setup, exactly one owner, automatic sign-in, one-time code and audit/WAL | `SetupCreatesSingleOwnerWithOneTimeCodeHashedCredentialsWalAndAudit` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Recovery reset/consumption/replacement, interrupted confirmation (blocking screen), current-password regeneration, no alternate path | `RecoveryCodeResetsPasswordOnceAndIssuesReplacement`, `RecoveryCodeCanBeRegeneratedOnlyWithCurrentPasswordAndMustBeAcknowledged`, `AuthRoutesExposeNoAlternatePasswordRecoveryEndpoint` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs`; `setup, blocked reload until a new code is confirmed, logout, login, and password recovery` — `frontend/e2e/auth-flow.spec.ts` |
| PBKDF2 minimum, hashed values, and secret-free logs | `PasswordHasherUsesPbkdf2Sha256AtOrAboveTheRequiredIterationCount`, `SetupCreatesSingleOwnerWithOneTimeCodeHashedCredentialsWalAndAudit`, `PasswordAndRecoveryCodeAreNeverWrittenToApplicationLogs` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| EF command logging levels and sensitive logging off | `EfCommandLoggingIsWarningByDefaultAndSensitiveDataLoggingIsDisabled` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Fixed failed-login delay without lockout, outside the operation lock | `WrongPasswordsApplyFixedOneSecondDelayWithoutTemporaryLockout` — `LocalApiTests.cs`; `FailedLoginDelayRunsAfterTheOperationGateIsReleased` — `LocalAuthServiceTests.cs` |
| 8-character password minimum | `PasswordMinimumIsEightCharacters` — `LocalApiTests.cs`; Playwright setup uses an 8-character password |
| Optional current-password change, logout, auto-lock, and `Never` | `ChangePasswordRequiresCurrentPasswordAndInvalidatesSession`, `LogoutRevokesAuthenticatedSessionImmediately`, `InactivityTimeoutLocksAuthenticatedSessionAndProtectsPrivateRoutes`, `InactivityTimeoutSupportsNeverWithoutExpiringSessions` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Loopback binding and noncanonical-listener rejection | `LiveKestrelStartupBindsOnlyToLoopback`, `ListenerGuardRejectsAnyAddressOtherThanCanonicalLoopback` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Host/Origin/token/CORS, SameSite cookie attributes and loopback HTTP behavior | `MaliciousCrossOriginAndDnsRebindingRequestsAreRejected`, `LoginCookieHasRequiredAttributesAndOmitsSecureOnLoopbackHttp` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Unified error envelope, 404/405/415/422/500 behavior, stable API codes and Arabic coverage, never 2xx for an error | `FrameworkAndValidationFailuresUseUnifiedApiErrorContract`, `EveryApiErrorCodeHasAnArabicDictionaryEntry`, `ChangePasswordWithBogusOrExpiredSessionCookieReturns401Unauthenticated` — `LocalApiTests.cs`; `EveryErrorCodeConstantIsRegisteredMappedToAnErrorStatusAndTranslated`, `BackendSourceEmitsErrorCodesOnlyThroughRegisteredConstants`, `MiddlewareNeverSendsAnErrorWithASuccessStatus`, `ResolveErrorNeverReturnsANonErrorStatusForAnyInput` — `ErrorContractTests.cs`; `apiRequest` Vitest tests — `frontend/src/api.test.ts`; `real validation and not-found responses, mocked 500, and a stopped server stay Arabic` — `frontend/e2e/auth-flow.spec.ts` |
| Wrong-password UI and complete owner browser journey | `setup, recovery confirmation, automatic entry, logout, login, and password recovery` — `frontend/e2e/auth-flow.spec.ts` |
| Password visibility accessibility and frontend localization | `LoginPasswordVisibilityToggleHasAccessibleLabelAndPressedState` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs`; `toggles visibility with a labelled, pressed-state eye control`, Arabic error mapping Vitest tests — `frontend/src/components/PasswordField.test.tsx`, `frontend/src/i18n/errors.test.ts` |
| Clean Architecture and confirmed reset (Arabic prompts) | `ArchitectureDependenciesFlowInward`, `LocalDatabaseResetRequiresExactConfirmationAndRemovesOnlyDatabaseFiles` — `LocalApiTests.cs`; `ApiDoesNotReferenceDomainOrEntityFrameworkCore`, `EndpointsAndOtherApiTypesDoNotUseDbContextOrDomainEntities`, `OrToolsIsNotUsedByDomainOrApplication` — `ArchitectureTests.cs` |
| SQLite WAL and per-connection `synchronous=FULL` | `SetupCreatesSingleOwnerWithOneTimeCodeHashedCredentialsWalAndAudit`, `EveryEfCoreConnectionAppliesSynchronousFull` — `LocalApiTests.cs` |
| Frontend build/lint enforcement | `npm.cmd --prefix frontend run build`, `npm.cmd --prefix frontend run lint` |

## Phase 1.3 - audit fixes (no new features)
Fixes the defects and documentation gaps recorded in `docs/AUDIT_REPORT.md` §11.
- Never return an error with a 2xx status; single error-code registry with status and Arabic coverage tests; client rejects 2xx bodies carrying `code`.
- Blocking unacknowledged-recovery-code screen after reload.
- 8-character password minimum.
- Real-server Playwright checks for validation and not-found; mocks only for the 500 path.
- Frontend split into `features/`, `layout/`, `lib/`; server state only in TanStack Query.
- `TreatWarningsAsErrors` with analyzers; architecture tests for endpoints and OR-Tools.
- Per-connection `synchronous=FULL`; failed-login delay outside the operation lock.
- Arabic reset prompts; ADR 0013; corrected docs; CHANGELOG entries; interim backup instructions.

Acceptance: `dotnet build -c Release` with 0 warnings and 0 errors; all .NET, Vitest and Playwright tests pass; `tsc` and ESLint clean; the audit section 11 items each marked DONE or NOT DONE with evidence.

## Phase 1.4 - design system adoption (no new features)
- `DESIGN_SYSTEM.md` and `frontend/src/styles/tokens.css` are the only design authority.
- Bundled Noto Sans Arabic; all styles use tokens; `components/ui` primitives (Alert, Field, Dialog, Table, Badge, Spinner, Select, Checkbox, IconButton, TimetableCell).
- `/design` style guide in development only.
- Enforcement: Stylelint, ESLint design rules, the contrast test, axe, and breakpoint screenshots.
- Inactivity auto-lock setting in Settings (persisted per owner, applied at runtime).
- Quieter test logs; a guarded interim backup command.

Acceptance:
- `dotnet build -c Release --no-incremental` reports 0 warnings and 0 errors.
- `dotnet test`, `npm run lint` and `npm test` are green.
- No serious or critical axe violations on the account screens.

