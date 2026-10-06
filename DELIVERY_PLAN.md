# Delivery Plan

The product has eight implementation phases (Phases 1-8). Phase 0 is a documentation/approval gate and is not counted. The scope is one local school, one workstation, exactly one owner account, and no external network service.

## Phase 0 - Documentation and owner approval gate
- Architecture, ADRs, isolated C# solver/PDF spikes, risk records, and environment prerequisites.
- Phase 0 is committed/tagged. Phase 1 began only after the owner replied exactly `approved`.
- QuestPDF Phase 0 proof: .NET 9 / QuestPDF 2026.9.1 / Noto Naskh Arabic rendered an inspected one-page RTL PDF. QuestPDF licensing review and packaged-runtime visual validation remain gates before Phase 6 distribution/implementation.

## Phase 1 - Local foundation and owner authentication
- ASP.NET Core local application, SQLite/WAL, clean architecture, and a same-origin React 19/strict TypeScript/Vite frontend served from API `wwwroot`.
- Exactly one owner account. No RBAC, permission matrix, refresh tokens, remote API, or multi-user concurrency support.
- Setup creates and signs in the owner. One-time recovery-code acknowledgment gates the first entry; after interrupted acknowledgment, sign in and regenerate a code from Settings with the current password. Password changes are optional and require the current password.
- PBKDF2-HMAC-SHA-256, 600,000 iterations, 8–1024 character passwords (minimum lowered from 12 in Phase 1.3), fixed one-second delay after failed login, no temporary lockout or escalating delay, local cookie session, and configurable inactivity lock (`1-1440` minutes or `Never`).
- Kestrel binds only to `127.0.0.1`; strict Host/Origin checks, no wildcard CORS, SameSite=Strict HttpOnly cookie, and per-launch token on state-changing requests.
- Lightweight local audit history without user attribution.
- Arabic-only user-facing UI and generated text, unified API error codes, lucide-react icons, accessible RTL forms, and lint/test enforcement.

### Phase 1 acceptance criteria
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

#### Phase 1 acceptance test mapping
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

### Phase 1.3 - audit fixes (no new features)
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

### Phase 1.4 - design system adoption (no new features)
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

## Phase 2 - School setup
- School profile, teachers, subjects, stages, sections, shifts, bell system, and academic calendar.
### Acceptance criteria
- Owner can create, read, update, deactivate, and validate in-scope school setup records through the local UI.
- Referential integrity, uniqueness, and shift/lesson-time rules are enforced in Application and SQLite.
- Arabic/RTL forms and tables work at supported desktop/tablet sizes; validation errors are actionable and localized.
- Unit and SQLite integration tests cover CRUD, invalid references, uniqueness, and migrations.

### Phase 2 checkpoints (branch `phase-2`)
| Checkpoint | Scope | Tag |
|---|---|---|
| 2A | App shell (sidebar, drawer, top bar, breadcrumbs), school profile with logo and stamp, academic years and terms, settings, formatter, Arabic normalization, concurrency versions, audit, dashboard, `dotnet-ef` tool and pending-model test | `phase-2a` |
| 2B | Shifts, working days, lesson periods (with a generator), bells (Web Audio) | `phase-2b` |
| 2C | Stages and sections | `phase-2c` |
| 2D | Subjects | `phase-2d` |
| 2E | Teachers | `phase-2e` |
| 2F | Calendar, demo seed, hardening, docs, full E2E, owner test script | `phase-2f`, then `phase-2-final` |

Each checkpoint is one commit with a green build and tests, plus updated docs and CHANGELOG. Decisions taken without the owner are listed in `docs/DECISIONS_PENDING.md`.

## Phase 2.5 - Simpler setup (branch `phase-2-5`)
Fixes data-entry weight before Phase 3. The full specification is in `docs/PHASE_25_SPEC.md`.

| Checkpoint | Scope | Tag |
|---|---|---|
| 2.5A | LtrText, DateField/TimeField, Arabic labels, no drawers, add patterns, regrouped navigation, quick add | `phase-2-5a` |
| 2.5B | Per-day lesson counts, capacity and validation, shift mode, setup progress | `phase-2-5b` |
| 2.5C | JSON templates, stage/section generator, curriculum entries with totals, copy helpers, auto colours | `phase-2-5c` |
| 2.5D | Setup wizard (7 steps, resumable, idempotent) | `phase-2-5d` |
| fixes 1 | Owner findings B1–B8 (curriculum table layout, overflow, overlap guard, subject chips, Arabic counts, Iraqi months) | `phase-2-5-fix1` |
| fixes 2 | M1 editable breaks, M2 lessons per day per stage, M3 capacity in curriculum headers | `phase-2-5-fix2` |
| 2.5E | E2E scenarios, demo variants, docs, owner test script, report | `phase-2-5e`, then `phase-2-5-final` |
| curriculum | Owner's suggested Iraqi curriculum (preview, idempotent apply, «مقترح» flag, per-stage reset) and the daily distribution suggestion | `phase-2-5-curriculum` |

## Phase 3 - Workload and capacity
- Workload, resources, scheduling profiles, capacity analysis, and deterministic validation.
- **Source of weekly workload:** the curriculum table (stage × subject × weekly lessons; repeated lines allowed, ADR 0021), including lines filled from the owner's suggested curriculum (ADR 0028; a suggestion, not an official plan). Teachers are assigned to curriculum entries per section.
- **Allowed periods:** each section may only use its stage's day counts (ADR 0027, 0030).
- **Capacity rule (ADR 0027):** a section's day has the first N lessons of its shift's day, where N is its stage's own count (default: the shift's). Workload checks and the Phase 4 solver must not use lessons beyond N.
### Acceptance criteria
- Workload totals and section capacity accurately report shortage/excess in Arabic.
- Teacher availability, subject allowed slots, required resources, and impossible block combinations are validated before generation.
- Soft-rule weights/defaults are persisted and validated; hard constraints cannot be configured away.
- Tests cover valid cases and measured boundary/shortage cases against SQLite.
- Phase 3D builds a deterministic, serializable `SchedulingInput` snapshot with canonical `InputHash`; the pure validator and «جاهزية الجدولة» report run before solver invocation. Phase 4 consumes this exact contract and persists its hash.
- The validator is deliberately conservative: only proven upper-bound violations are errors; uncertain combinations remain warnings. It does not claim complete constraint solving.
- Phase 4 calls the validator with `ValidatorOptions.DoublePeriodsRequired = true` when the owner generates in the «دروس مزدوجة» mode, so an impossible double period blocks only that mode (DECISIONS_PENDING #65).
- Phase 3E: the deterministic assignment suggester, wizard step «الأنصبة», demo data with every checklist item done (and `--with-problems`), Playwright scenarios (a)–(h). Tags `phase-3d`, `phase-3e`, `phase-3-final`.

## Phase 4 - Local CP-SAT generation
- C# Google.OrTools, local background generation service, progress, cancellation, deterministic mode, and infeasibility diagnostics.
- The solver adapter consumes Phase 3's `SchedulingInput` and `InputHash`; it does not rebuild input from database records. Queueing, progress, cancellation, placement and solver conflict diagnostics remain Phase 4.
- No generation worker container, remote queue, or PostgreSQL advisory lock.

### Phase 4 open risk and mandatory investigation gate
The exploratory **40-section / 54-teacher** case found no feasible solution within **30 seconds**. Fifty-four teachers is only an aggregate workload lower bound and does not establish that a particular roster/availability input is feasible. Before Phase 4 acceptance, first construct and independently establish a feasible 40-section/54-teacher baseline, then investigate two-stage solving, decomposition, and solver hints. Compare against the same input with repeatable solver version/profile/seed. Record status, first-solution and total timings, objective/bound, memory measurement method, and independent hard-constraint validation. Do not relax hard constraints to meet a target.

### Phase 4 acceptance criteria
- The 12-section/20-teacher case is feasible within the agreed performance target and independently passes all hard constraints.
- A feasible 40-section/54-teacher baseline and measured comparison of baseline/two-stage/decomposition/hints are documented and reviewed by the owner.
- Soft objective implements configured teacher-gap, subject-spread, heavy-subject, repetition, and double-period weights; do not claim optimality unless solver status proves it.
- Pre-solve diagnostics identify shortage/capacity errors deterministically.
- A tested infeasible multi-constraint conflict contains two named teachers, a shared lab, and a blocked period. Assumptions/relaxation diagnostics identify the implicated teachers, section/subject, resource and rule groups, quantify the conflict/shortage, and suggest an actionable correction. A generic infeasible status is not acceptance.
- Cancellation, timeout, deterministic single-worker, and multi-worker status handling have automated tests.

## Phase 5 - Timetable lifecycle
- Timetable versioning, manual editor, approval, publication, rollback, and comparison.
### Acceptance criteria
- Only valid state transitions succeed; published/archived versions are immutable.
- Rollback creates a new version; comparison identifies added, removed, and moved lessons.
- Manual changes enforce hard constraints and stale-version protection; keyboard alternatives accompany pointer interactions.
- Local audit history records version changes, publish, and rollback; integration/UI tests cover transitions and history.

## Phase 6 - Local reporting and data portability
### Pre-start licensing gate
- Before QuestPDF dependency distribution or feature implementation, review the exact version's current license, Community/Professional/Enterprise terms, commercial/organizational use, redistribution, and attribution. Record version, terms, and owner/legal GO/NO-GO. Do not infer distribution permission from the Phase 0 render.
- If terms are unacceptable or unclear, NO-GO for QuestPDF and assess the pinned local HTML-to-PDF fallback in an ADR; no client-side PDF library.
### Technical setup and implementation
- First technical setup task after the licensing gate: render the QuestPDF Arabic A4 landscape proof in the packaged runtime using Cairo or Noto Naskh Arabic. Inspect joining, RTL column order, clipping, and layout; capture a visual regression baseline. If QuestPDF is NO-GO, decide on the approved fallback before implementation.
- PDF/Excel export, offline printing via `window.print()`, local backup/restore, and import.
- SQLite backup uses Online Backup API/provider equivalent for a consistent single-file database. File-copy alternative must quiesce/checkpoint/close before copying and run integrity checks; never directly copy an active main `.db`.
### Acceptance criteria
- QuestPDF licensing decision precedes dependency distribution and feature implementation.
- Packaged Arabic PDF passes the visual baseline; Excel is RTL-correct and printable.
- Backup/restore yields and validates a consistent single-file SQLite backup in WAL mode; corruption/interrupted restore are tested.
- Import dry-run/validation reports errors and never silently overwrites data.
- Printing, exports, backup/restore, and import work with external networking disabled.

## Phase 7 - School operations
- Attendance, monitoring, dashboard, and local lesson status.
### Acceptance criteria
- Attendance and monitoring respect school-local dates and configured lesson times.
- Dashboard values derive from persisted data and update after relevant changes; no fabricated statistics.
- Monitoring rotation/workload rules have domain and UI tests.
- Local audit history captures applicable operations without user attribution.

## Phase 8 - Package security and release validation
- Repeat setup/login/recovery/auto-lock, loopback binding, and localhost-attack suites against the packaged release.
- Verify core workflows, generation, reporting, backup/restore, and imports work without external networking.
- Browser-based login is the baseline. Optional WebView2 requires separate acceptance and the same security controls.
- SQLCipher is optional; ADR 0010 records a NO-GO for initial release and final-phase reevaluation. Do not claim encryption without approval and passing tests.
- No RBAC, refresh-token rotation, tenant isolation, remote sync, or multi-user acceptance tests.
### Acceptance criteria
- Packaged release passes setup/login/recovery/auto-lock, one-time code display/consumption, and no-alternate-recovery tests.
- Listener is exactly `127.0.0.1`; localhost-attack tests cover malicious cross-origin requests, invalid Host/Origin, SameSite, missing/stale token, and DNS rebinding.
- Release works offline; backups restore; no unexpected outbound requests are observed.
- SQLCipher and optional WebView2 have recorded GO/NO-GO decisions; no encryption claim is made on NO-GO.
- Builds/tests pass, docs and changelog are current, known risks/performance are reported, and release is tagged.

## Environment requirements
- .NET SDK 9.0.318 and Node.js v24.18.0 are available on PATH in the current environment; both are needed for the API/frontend build and test workflow.
- Docker, PostgreSQL, and Redis are not required. SQLite is embedded/local.

## Approval gate
Phase 1.4 is tagged. Phase 2 is developed on the `phase-2` branch in checkpoints 2A–2F and is merged only after owner acceptance.
