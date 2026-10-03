# Delivery Plan

The delivery plan has **eight implementation phases (Phases 1-8)**. Phase 0 is a documentation/approval gate and is not counted as an implementation phase. The scope is one local school, one owner account, one workstation, and no external network service.

## Phase 0 - Documentation and owner approval gate
- Architecture, ADRs, isolated solver and Arabic PDF spikes, and documented environment prerequisites
- No application implementation
- **Acceptance:** Phase 0 documents and spike evidence are committed; owner replies exactly `approved` before Phase 1 begins.

## Phase 1 - Local foundation and owner authentication
- Single-machine ASP.NET Core application, browser setup/login screen, local SQLite (WAL), and clean architecture
- Exactly one owner account at runtime; no RBAC, permission matrix, refresh tokens, remote API, or multi-user concurrency support
- One-time recovery code, password hashing, incremental failure delay/temporary lockout, current-password change flow, local session, configurable inactivity auto-lock
- Kestrel binds only to `127.0.0.1`; exact Host/Origin validation; no wildcard CORS; SameSite=Strict HttpOnly cookie; per-launch random token on all state-changing requests
- Lightweight local audit history without user attribution

### Phase 1 acceptance criteria
- First-run setup creates exactly one owner account, warns the recovery code is the only reset path, and shows it once with store/print instructions.
- Password and recovery code are never stored in clear text; recovery code resets the password once, is consumed, and is replaced with a new one-time code; no other password-recovery path exists.
- Password change requires the current password. Login failure delay and temporary lockout, session invalidation, logout, and configurable inactivity auto-lock are tested.
- Authenticated application routes remain inaccessible until login succeeds.
- Automated startup test inspects Kestrel's actual address and fails unless it is exactly `127.0.0.1`; demonstrate rejection of `0.0.0.0`, wildcard, and LAN binds.
- Browser security tests reject invalid Host/Origin, confirm no wildcard CORS, enforce SameSite cookies, and reject state-changing requests without the current per-launch token. Include a malicious cross-origin page and DNS-rebinding-style Host test.
- SQLite migration, WAL startup, single-account invariant, and minimal local audit history are tested.

#### Phase 1 acceptance test mapping
| Acceptance criterion | Proving test(s) |
|---|---|
| First-run setup, one owner, one-time recovery display, only-recovery-path warning, and store/print instructions | `SetupCreatesSingleOwnerWithOneTimeCodeHashedCredentialsWalAndAudit` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| PBKDF2-HMAC-SHA-256 minimum parameters and verification, hashed credentials, one-time recovery reset/replacement, no alternate recovery endpoint, and exact 128-bit recovery-code format | `PasswordHasherUsesPbkdf2Sha256AtOrAboveTheRequiredIterationCount`, `SetupCreatesSingleOwnerWithOneTimeCodeHashedCredentialsWalAndAudit`, `RecoveryCodeResetsPasswordOnceAndIssuesReplacement`, `AuthRoutesExposeNoAlternatePasswordRecoveryEndpoint` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| EF database-command log level by environment, sensitive-data logging disabled, and credentials/recovery codes absent from application logs | `EfCommandLoggingIsWarningByDefaultAndSensitiveDataLoggingIsDisabled`, `PasswordAndRecoveryCodeAreNeverWrittenToApplicationLogs` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Incremental wrong-password delay and lockout | `WrongPasswordsApplyIncrementalDelayAndTemporaryLockout` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Current-password change and session invalidation | `ChangePasswordRequiresCurrentPasswordAndInvalidatesSession` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Logout and configurable inactivity auto-lock | `LogoutRevokesAuthenticatedSessionImmediately`, `InactivityTimeoutLocksAuthenticatedSessionAndProtectsPrivateRoutes` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Authenticated routes are inaccessible before login | `InactivityTimeoutLocksAuthenticatedSessionAndProtectsPrivateRoutes` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Actual Kestrel loopback startup and rejection of wildcard/LAN bindings | `LiveKestrelStartupBindsOnlyToLoopback`, `ListenerGuardRejectsAnyAddressOtherThanCanonicalLoopback` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Invalid Host/Origin, no wildcard CORS, strict-cookie attributes including loopback HTTP Secure behavior, launch-token requirement, cross-origin and DNS-rebinding-style requests | `MaliciousCrossOriginAndDnsRebindingRequestsAreRejected`, `LoginCookieHasRequiredAttributesAndOmitsSecureOnLoopbackHttp` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Accessible login password show/hide control | `LoginPasswordVisibilityToggleHasAccessibleLabelAndPressedState` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| SQLite migration, WAL, exactly-one-owner invariant, and local audit entries | `SetupCreatesSingleOwnerWithOneTimeCodeHashedCredentialsWalAndAudit`, `RecoveryCodeResetsPasswordOnceAndIssuesReplacement`, `ChangePasswordRequiresCurrentPasswordAndInvalidatesSession` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Clean Architecture dependency boundaries | `ArchitectureDependenciesFlowInward` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |
| Destructive first-run database reset requires explicit confirmation | `LocalDatabaseResetRequiresExactConfirmationAndRemovesOnlyDatabaseFiles` — `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` |

## Phase 2 - School setup
- School Profile, teachers, subjects, stages, sections, shifts, bell system, academic calendar

### Phase 2 acceptance criteria
- Owner can create, read, update, deactivate, and validate each in-scope school setup record through the local UI.
- Referential integrity, uniqueness, and shift/lesson-time rules are enforced in Application and SQLite.
- Arabic/RTL forms and tables work at supported desktop and tablet sizes; validation errors are actionable.
- Unit and SQLite integration tests cover CRUD, invalid references, uniqueness, and migrations.

## Phase 3 - Workload and capacity
- Workload, resources, scheduling profiles, capacity analysis, deterministic validation

### Phase 3 acceptance criteria
- Workload totals and section capacity are calculated accurately and report shortage/excess in Arabic and English.
- Teacher availability, subject allowed slots, required resources, and impossible block combinations are validated before generation.
- Soft-rule weights/defaults are persisted and validated; hard constraints cannot be configured away.
- Tests cover valid cases and measured boundary/shortage cases against SQLite.

## Phase 4 - Local CP-SAT generation
- C# Google.OrTools, local background generation service, real progress, cancellation, deterministic mode, and infeasibility diagnostics
- No generation worker container, remote queue, or PostgreSQL advisory lock

### Phase 4 open risk and mandatory investigation gate
The exploratory 40-section / 54-teacher case found no feasible solution within 30 seconds. Fifty-four teachers is only the aggregate workload lower bound; it does not prove a particular roster and availability profile is feasible. Before accepting Phase 4, first construct and independently establish a feasible 40-section / 54-teacher baseline, then investigate two-stage solving, decomposition, and solver hints. Compare each against that same baseline using repeatable inputs, solver version, profile, and seed. Measure status, time to first feasible solution, total time, objective/bound, memory (label the measurement method), and independent hard-constraint validation. Report results; do not relax hard constraints to meet a target.

### Phase 4 acceptance criteria
- For the 12-section / 20-teacher case, generation produces a feasible schedule within the agreed performance target and independent validation confirms every hard constraint.
- For a feasible 40-section / 54-teacher case, results from the investigation gate are measured and documented; owner reviews the evidence before performance acceptance.
- Soft objective implements configured teacher-gap, subject-spread, heavy-subject, repetition, and double-period weights; objective values are not presented as optimal unless solver status proves optimality.
- Pre-solve diagnostics identify shortage/capacity errors deterministically.
- Infeasibility diagnostics demonstrate a multi-constraint conflict, for example: two named teachers contend for the same period, the only shared lab is required by both assignments, and one assignment also has a blocked period. CP-SAT assumptions/relaxation must identify the implicated teacher/section/subject/resource and rule groups, quantify the shortage/conflict, and provide actionable correction suggestions. Test the exact expected conflict core and suggestions; a generic “no solution” message is not acceptance.
- Cancellation, timeout, deterministic single-worker mode, and multi-worker status handling have automated tests.

## Phase 5 - Timetable lifecycle
- Timetable versioning, manual editor, approval, publication, rollback, and comparisons

### Phase 5 acceptance criteria
- Only valid timetable state transitions succeed; published/archived versions are immutable.
- Rollback creates a new version; comparison reports added, removed, and moved lessons.
- Manual changes enforce hard constraints and stale-version protection; keyboard alternatives work alongside pointer interactions.
- Local audit history records version changes, publish, and rollback; integration and UI tests cover transitions and history.

## Phase 6 - Local reporting and data portability

### Phase 6 pre-start licensing gate
- Before implementation or package distribution, review the exact QuestPDF version's current license, including Community/Professional/Enterprise terms, commercial and organizational use, redistribution, and attribution requirements. Record the version, applicable terms, and owner/legal GO/NO-GO decision. Do not infer permission from the Phase 0 render or proceed with QuestPDF distribution until cleared.
- If the terms are not acceptable or unclear, NO-GO for QuestPDF and open an ADR to assess a pinned local HTML-to-PDF renderer; no client-side PDF library.

### Phase 6 technical setup and implementation
1. First technical task after the licensing gate: render the QuestPDF Arabic A4 landscape proof in the target packaged runtime using Cairo or Noto Naskh Arabic. Inspect rendered PNGs for glyph joining, RTL column order, clipping, and page layout; capture a visual regression baseline.
2. **Phase 0 rendering result:** QuestPDF feasibility GO. .NET 9, QuestPDF 2026.9.1, and Noto Naskh Arabic produced a visually inspected, one-page RTL PDF.
3. **Production go/no-go:** GO only after the packaged-runtime render and visual regression pass. Otherwise NO-GO for QuestPDF release and use the separately approved fallback ADR.
- PDF/Excel export, `window.print()` path, local backup/restore, and import; all work without internet.
- SQLite backups use the Online Backup API/provider equivalent to produce a consistent single-file database. A file-copy alternative requires quiescing users, checkpointing WAL, closing connections, copying, and integrity-checking. Never copy a live main `.db` file directly.

### Phase 6 acceptance criteria
- QuestPDF licensing decision is documented before dependency distribution or feature implementation.
- Packaged Arabic PDF passes the visual baseline for joining, RTL order, clipping, and page layout; Excel is RTL-correct and printable.
- Backup/restore produces and validates a consistent single-file SQLite backup in WAL mode; corruption and interrupted restore cases are tested.
- Import uses dry-run/validation, reports errors, and never silently overwrites data.
- Printing, exports, backup/restore, and import run with external networking disabled.

## Phase 7 - School operations
- Attendance, monitoring, dashboard, and local lesson status

### Phase 7 acceptance criteria
- Attendance and monitoring respect school-local dates and configured lesson times.
- Dashboard values derive from persisted local data and update after relevant changes; no fabricated statistics.
- Monitoring rotation and workload rules have domain and UI tests.
- Local audit history captures applicable operations without multi-user attribution.

## Phase 8 - Package security and release validation
- Repeat owner setup/login/recovery/auto-lock, loopback binding, and localhost-attack acceptance suites against the packaged release.
- Verify core workflows, generation, reporting, backup/restore, and imports work with external networking disabled.
- Browser-based login remains the baseline. Optional WebView2 standalone shell needs separate acceptance and the same security controls.
- SQLCipher is optional; perform the go/no-go decision documented in ADR 0010. Default is NO-GO unless key management, recovery, packaging, performance, and backup implications are resolved and tested.
- No RBAC, refresh-token rotation, tenant isolation, remote sync, or multi-user acceptance tests.

### Phase 8 acceptance criteria
- Packaged release passes full setup/login/recovery/auto-lock tests, including one-time code display/consumption and explicit no-recovery-path behavior when both secrets are lost.
- Automated test confirms the actual listener is exactly `127.0.0.1`; localhost-attack suite covers malicious cross-origin requests, invalid Host/Origin, SameSite, absent/stale launch token, and DNS-rebinding-style Host changes.
- Release works fully offline; backups restore; no unexpected outbound network requests are observed.
- SQLCipher has a documented GO/NO-GO decision; no encryption claim is made on NO-GO. Optional WebView2 has passed equivalent authentication and localhost security tests if included.
- All relevant builds/tests pass, docs and changelog are current, known risks and measured performance are reported, and the release commit is tagged.

## Environment requirements
- Application build: .NET 9 SDK.
- Web UI build: Node.js (use the supported project version declared by its manifest when added).
- SQLite is embedded/local; Docker, PostgreSQL, and Redis are not required.
- On this machine, .NET 9 is installed at `C:\Program Files\dotnet\dotnet.exe` but is not currently on `PATH`; add `C:\Program Files\dotnet` before relying on the bare `dotnet` command.

## Approval gate
Owner must reply exactly `approved` before Phase 1 starts.
