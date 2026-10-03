# Changelog

## [Unreleased]
### Phase 1.3 - audit fixes (tag `phase-1.3`)
Fixes only; no Phase 2 work. Source: `docs/AUDIT_REPORT.md` §11.
- **Fixed (D1):** an error code that the error middleware did not recognise was returned with HTTP 200. `Response.Clear()` had reset the status before the fallback read it. The middleware now captures the status first. Unregistered codes keep the original error status, or become 500 `INTERNAL_ERROR` when that status is below 400. Change-password with an expired or bogus session now returns 401 `UNAUTHENTICATED` (previously `200 {"code":"unauthenticated"}`), and the UI no longer reports "password changed" in that case. The client also treats any 2xx body containing `code` as an error.
- **Changed:** all error codes are constants in `Application/ErrorCodes.cs`, with statuses in `Api/ApiErrorCodes.StatusByCode`. Endpoints take the status from that table. Removed the unreachable `INVALID_SETUP`, `RECOVERY_CODE_REGENERATION_FAILED` and `PASSWORD_CHANGE_FAILED` fallbacks and the unused `CLIPBOARD_FAILED` UI code.
- **Fixed (D2):** a reload before acknowledging the recovery code no longer opens the application. A blocking screen requires generating a new code with the current password (or logging out). SECURITY.md, ADR 0009 and DELIVERY_PLAN were updated, and the Playwright test now asserts the blocking behaviour.
- **Changed:** the minimum password length is 8 (was 12) in the validators, service, UI and docs.
- **Fixed:** after a completed recovery, logging out showed the recovery form instead of the login screen. The recovery-form flag now resets.
- **Changed (frontend):** `App.tsx` was split into `features/auth`, `features/home`, `features/settings`, `layout/` and `lib/`, one component per file.
  - Removed the `RecoveryGate` pass-through and the nested `role="alert"`.
  - Password-toggle labels and the download filename moved to the dictionary.
  - `Object.hasOwn` is used for code lookup.
  - Bootstrap/session server state now lives only in TanStack Query; Zustand holds only the in-memory recovery code and a UI flag.
- **Changed (backend):**
  - Deduplicated security headers (`LocalSecurityHeaders`), cookie options (`SessionCookie`), token generation (`SecureToken`) and password/username rules (`CredentialRules` plus FluentValidation rule extensions).
  - Removed dead `context.Items` writes and the template `SmartSchoolTimetable.Api.http`.
  - Split the request/response records and validators into their own files.
- **Changed (build):** `Directory.Build.props` enables nullable, `TreatWarningsAsErrors`, .NET analyzers (`AnalysisLevel=latest-recommended`) and `EnforceCodeStyleInBuild`, and `.editorconfig` was added. The resulting CA1848 findings were fixed with source-generated `LoggerMessage` methods (`LocalLog`), and CA1305 in tests with a fixed `DateTimeOffset`. `spikes/` is excluded.
- **Fixed (D3):** `PRAGMA synchronous=FULL` is now applied on every EF Core connection (`SqlitePragmaInterceptor`); previously only the startup connection had it.
- **Fixed (D4):** the fixed one-second failed-login delay now runs after the operation lock is released.
- **Changed:** `--reset-local-database` console prompts are Arabic, and the console uses UTF-8.
- **Docs:**
  - ADR 0013 records not adopting MediatR/CQRS and deferring Serilog, health endpoints, CI and a generated OpenAPI client.
  - ARCHITECTURE.md now describes direct application services.
  - API.md has an endpoint table and the real status codes (422, not 400; no 423).
  - DATABASE.md describes the real columns and pragmas.
  - SECURITY.md says the iteration count is stored and the algorithm is fixed in code (no hash-version column was added).
  - README has interim backup instructions.
  - `docs/AUDIT_REPORT.md` §11 shows the DONE/NOT DONE status of each item.
- **Tests:**
  - .NET: 41 (was 22). New: ErrorContractTests, ArchitectureTests, LocalAuthServiceTests, change-password 401, 8-character minimum, per-connection pragma, Arabic reset prompts.
  - Vitest: 7 (was 4).
  - Playwright: 2, now against real server responses except the 500 path.
- **DB changes:** none (no migration).
- **API changes:** change-password with an invalid session now returns 401 instead of 200, and the minimum password length is 8.
- **Breaking changes:** none for stored data.

### Phase 1.2 - React owner UI and Arabic error contract (tag `phase-1.2`)
- Replaced the static owner pages with a React 19 / strict TypeScript / Vite / Tailwind UI served from the API `wwwroot` (ADR 0012). It uses TanStack Query, Zustand, React Router and lucide-react.
- Added the recovery-code confirmation screen (copy/print/save, acknowledgement gate), Settings with current-password regeneration and optional password change, client inactivity auto-lock, and accessible password visibility toggles.
- Added the unified API error envelope `{ code, correlationId, errors }` for framework, validation, security and unhandled failures; FluentValidation field/code pairs; and an Arabic dictionary covering every API code.
- Added the ESLint localization/icon rules, Vitest tests and two Playwright end-to-end tests.
- DB: migration `20261003152200_RecoveryCodeAcknowledgement` adds `Users.RecoveryCodeAcknowledged` (existing rows default to acknowledged).

### Phase 1.1 - authentication hardening (tag `phase-1.1`)
- Specified the credential parameters (PBKDF2-HMAC-SHA-256, 600,000 iterations, 16-byte salt, 32-byte key) and the recovery code (128-bit, 4×8 hex, salted SHA-256).
- EF Core command logging is `Warning` by default and `Information` only in Development; sensitive-data logging is explicitly off.
- Added tests that passwords and recovery codes never reach logs, and that the session cookie has `HttpOnly`, `SameSite=Strict` and `Path=/` without `Secure` on loopback HTTP.
- Added an accessible password visibility toggle to the login page.
- DB: migration `20261003160000_RemoveEscalatingLoginLockoutFields` drops the escalating-lockout columns; failed logins use only a fixed one-second delay.

### Phase 1 - local owner foundation (tag `phase-1`)
- Added the ASP.NET Core 9 local host with the Domain/Application/Infrastructure/Api layering, bound only to `127.0.0.1` with startup and live-binding checks.
- Added first-run owner setup, login, logout, recovery-code reset and regeneration, password change, and the configurable inactivity timeout (1–1440 minutes or `Never`) with an in-memory server session store.
- Added localhost protections: exact Host/Origin validation, SameSite=Strict HttpOnly cookie, no CORS, a per-launch token on state-changing requests, and security headers.
- Added EF Core SQLite persistence in WAL mode (migration `20261003105756_InitialLocalSchema`: `Users`, `AuditHistory`), local audit events, and the confirmed `--reset-local-database` command.
- Added xUnit integration tests for the Phase 1 acceptance criteria.

### Phase 0.4 - storage, acceptance, and risk criteria
- Added the SQLite WAL/safe-backup/provider-abstraction ADR and detailed its backup integrity requirements.
- Reworked the delivery plan into eight implementation phases with acceptance criteria for every phase.
- Recorded the 40-section / 54-teacher 30-second no-solution risk and required measured investigation of two-stage solving, decomposition, and solver hints.
- Added a multi-constraint infeasibility-diagnostic acceptance case and a QuestPDF licensing gate before Phase 6.
- Clarified that .NET 9 and Node are the only required build toolchains; Docker, PostgreSQL, and Redis are not required. .NET 9 is installed but currently not on PATH.
- Documentation only; Phase 1 had not started at this point.

### Phase 0.3 - owner scope clarification
- Replaced the multi-tenant/multi-user deployment assumptions with a single-user, local-only application architecture.
- Documented one-time owner setup/recovery, password/session/lockout requirements, loopback and localhost-attack protections, and local audit history.
- Updated the delivery and test criteria for Phases 1 and 8.
- Added ADRs for local deployment, owner authentication, and the SQLCipher final-phase go/no-go; marked tenant sync and distributed worker ADRs superseded.
- No application code was added; Phase 1 remains gated on explicit owner approval.

### Phase 0
- Added architecture, database, domain, API, sync, security, testing, observability, and delivery-plan documentation.
- Added ADRs for tenancy, C# CP-SAT, offline sync/printing, PostgreSQL worker locking, deterministic solving, and Arabic PDF rendering.
- Ran the .NET 9 / Google.OrTools C# spike with hard constraints, weighted soft preferences, shared-resource capacity, single/multi-worker timings, and assumption-core infeasibility diagnostics.
- Ran the QuestPDF/Noto Naskh Arabic RTL spike, generated a PDF, and visually inspected its PNG render.
- Recorded the 40-section / 20-teacher workload-cap infeasibility and local environment prerequisites.
