# Audit Report — SmartSchoolTimetable

- Audit date: 2026-10-03
- Audited commit: `1c032f3a02b7b717324a7b67e6c2e98729f526b3` (tag `phase-1.2`), working tree clean
- Mode: read-only. No source, config, test or doc file was changed. The only commit is this file.
- Toolchain on the audit machine: .NET SDK 9.0.318, Node v24.18.0, npm 11.16.0, Playwright 1.63.0
- Raw command logs were written to a session scratch directory and are summarised below. They are not committed.

Conventions: `file:line` points at the audited commit. "Evidence" means command output or a source line. Where something could not be verified, this report says so.

---

## 1. Snapshot

### 1.1 Git

```
$ git describe --tags --always      -> phase-1.2
$ git rev-parse HEAD                -> 1c032f3a02b7b717324a7b67e6c2e98729f526b3
$ git tag                           -> phase-0, phase-0.3, phase-0.4, phase-1, phase-1.1, phase-1.2
$ git log --oneline -20             (only 6 commits exist)
1c032f3 Complete Phase 1.2 React owner UI and Arabic errors        (tag phase-1.2, 2026-10-03 17:40 +0300)
4c50a90 Harden Phase 1 authentication and operator docs            (tag phase-1.1, 15:02)
fe3072a Implement Phase 1 local owner foundation                   (tag phase-1,   14:33)
2f32f17 Complete Phase 0.4 storage and delivery docs               (tag phase-0.4, 13:29)
6ba06e7 Clarify Phase 0 local single-user scope                    (tag phase-0.3, 13:24)
badebde Complete Phase 0 documentation and spikes                  (tag phase-0,   11:30)
```

All six commits carry `Co-authored-by: Copilot`. 95 files are tracked.

### 1.2 File tree (tracked files; bin/obj/node_modules excluded)

```
.gitignore  API.md  ARCHITECTURE.md  CHANGELOG.md  CLAUDE.md  DATABASE.md  DELIVERY_PLAN.md
DOMAIN.md  OBSERVABILITY.md  OFFLINE_SYNC.md  README.md  SECURITY.md  SmartSchoolTimetable.sln  TESTING.md
adr/0001-tenant-isolation.md … adr/0012-react-arabic-localization.md   (12 ADRs)
frontend/
  components.json  eslint.config.js  index.html  package.json  package-lock.json
  playwright.config.ts  tsconfig.json  vite.config.ts
  e2e/auth-flow.spec.ts
  src/App.tsx  src/api.ts  src/main.tsx  src/styles.css  src/test-setup.ts
  src/components/PasswordField.tsx  src/components/PasswordField.test.tsx
  src/components/ui/button.tsx  card.tsx  input.tsx
  src/i18n/errors.ts  errors.test.ts  messages.ts
  src/state/session.ts
spikes/README.md
spikes/CSharpSpikes/CSharpSpikes.csproj  Program.cs  benchmark-results.txt
  fonts/NotoNaskhArabic-Regular.ttf  fonts/OFL.txt  rtl_questpdf_sample.pdf  rtl_questpdf_sample.png
src/SmartSchoolTimetable.Domain/          LocalAuditEntry.cs  OwnerAccount.cs  *.csproj
src/SmartSchoolTimetable.Application/     ICredentialHasher.cs  ILocalSessionStore.cs  ILoginDelay.cs
                                          IOwnerRepository.cs  LocalAuthContracts.cs  LocalAuthService.cs  *.csproj
src/SmartSchoolTimetable.Infrastructure/  LocalDbContext.cs  LocalDbContextFactory.cs
                                          LocalInfrastructureRegistration.cs  LocalOwnerRepository.cs
                                          LocalSessionStore.cs  Pbkdf2CredentialHasher.cs  RealLoginDelay.cs  *.csproj
  Migrations/20261003105756_InitialLocalSchema.cs (+ .Designer.cs)
  Migrations/20261003152200_RecoveryCodeAcknowledgement.cs        (no .Designer.cs)
  Migrations/20261003160000_RemoveEscalatingLoginLockoutFields.cs  (no .Designer.cs)
  Migrations/LocalDbContextModelSnapshot.cs
src/SmartSchoolTimetable.Api/             AuthEndpoints.cs  LocalDatabaseReset.cs  LocalSecurity.cs  Program.cs
                                          UnifiedApiErrorMiddleware.cs  SmartSchoolTimetable.Api.http
                                          appsettings.json  appsettings.Development.json  Properties/launchSettings.json
  wwwroot/index.html  wwwroot/assets/index-CptQqE0G.js  wwwroot/assets/index-rVhQwXZz.css   (Vite build output, tracked)
tests/SmartSchoolTimetable.Tests/         LocalApiTests.cs  *.csproj
```

The directory `.kilo/` is on disk but ignored by git. It holds a worktree copy of the Phase 0 docs and is not part of the audited tree.

### 1.3 Lines of code per project (`.cs/.ts/.tsx/.css`; bin/obj/wwwroot excluded)

| Project | Files | Total lines | Non-blank | of which EF migrations |
|---|---:|---:|---:|---:|
| SmartSchoolTimetable.Domain | 2 | 94 | 85 | 0 |
| SmartSchoolTimetable.Application | 6 | 369 | 325 | 0 |
| SmartSchoolTimetable.Infrastructure | 12 | 710 | 599 | 435 |
| SmartSchoolTimetable.Api | 5 | 753 | 672 | 0 |
| SmartSchoolTimetable.Tests | 1 | 983 | 869 | 0 |
| frontend/src (incl. styles.css 339) | 14 | 1286 | 1171 | 0 |
| frontend/e2e | 1 | 235 | 218 | 0 |
| spikes/CSharpSpikes | 1 | 394 | 349 | 0 |

### 1.4 Ten largest source files (lines)

| Lines | File |
|---:|---|
| 983 | tests/SmartSchoolTimetable.Tests/LocalApiTests.cs |
| 573 | frontend/src/App.tsx |
| 394 | spikes/CSharpSpikes/Program.cs |
| 341 | src/SmartSchoolTimetable.Api/AuthEndpoints.cs |
| 339 | frontend/src/styles.css |
| 300 | src/SmartSchoolTimetable.Application/LocalAuthService.cs |
| 235 | frontend/e2e/auth-flow.spec.ts |
| 142 | src/SmartSchoolTimetable.Api/LocalSecurity.cs |
| 138 | src/SmartSchoolTimetable.Api/UnifiedApiErrorMiddleware.cs |
| 122 | src/SmartSchoolTimetable.Infrastructure/Migrations/20261003105756_InitialLocalSchema.Designer.cs |

---

## 2. Build and tests

### 2.1 .NET build

```
$ dotnet build SmartSchoolTimetable.sln -c Release --no-incremental
  (MSBuild target BuildReactFrontend runs: tsc --noEmit && vite build)
  vite v6.4.3: wwwroot/index.html 0.47 kB; assets/index-rVhQwXZz.css 10.47 kB; assets/index-CptQqE0G.js 333.67 kB (gzip 103.61 kB)
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:29.82
```

The Vite build regenerated `wwwroot` with byte-identical output; `git status` stayed clean.

Caveat: `TreatWarningsAsErrors`, `AnalysisLevel` and `.editorconfig` are not configured anywhere (no `Directory.Build.props`, nothing in any `.csproj`). "0 warnings" therefore reflects the default SDK analyzers only.

### 2.2 .NET tests

```
$ dotnet test SmartSchoolTimetable.sln -c Release --no-build --collect:"XPlat Code Coverage"
Total tests: 22
     Passed: 22
 Total time: 47.26 s
```

All 22 tests are in `tests/SmartSchoolTimetable.Tests/LocalApiTests.cs` (class `LocalApiTests`):

| # | Test | Line |
|---:|---|---:|
| 1 | SetupCreatesSingleOwnerWithOneTimeCodeHashedCredentialsWalAndAudit | 34 |
| 2 | PasswordHasherUsesPbkdf2Sha256AtOrAboveTheRequiredIterationCount | 103 |
| 3 | EfCommandLoggingIsWarningByDefaultAndSensitiveDataLoggingIsDisabled | 126 |
| 4 | EveryApiErrorCodeHasAnArabicDictionaryEntry | 150 |
| 5 | InactivityTimeoutSupportsNeverWithoutExpiringSessions | 171 |
| 6 | FrameworkAndValidationFailuresUseUnifiedApiErrorContract | 192 |
| 7 | PasswordAndRecoveryCodeAreNeverWrittenToApplicationLogs | 250 |
| 8 | LoginPasswordVisibilityToggleHasAccessibleLabelAndPressedState | 270 |
| 9 | LoginCookieHasRequiredAttributesAndOmitsSecureOnLoopbackHttp | 288 |
| 10 | RecoveryCodeResetsPasswordOnceAndIssuesReplacement | 313 |
| 11 | WrongPasswordsApplyFixedOneSecondDelayWithoutTemporaryLockout | 357 |
| 12 | RecoveryCodeCanBeRegeneratedOnlyWithCurrentPasswordAndMustBeAcknowledged | 375 |
| 13 | ChangePasswordRequiresCurrentPasswordAndInvalidatesSession | 425 |
| 14 | LogoutRevokesAuthenticatedSessionImmediately | 459 |
| 15 | InactivityTimeoutLocksAuthenticatedSessionAndProtectsPrivateRoutes | 476 |
| 16 | MaliciousCrossOriginAndDnsRebindingRequestsAreRejected | 496 |
| 17 | ListenerGuardRejectsAnyAddressOtherThanCanonicalLoopback | 572 |
| 18 | LiveKestrelStartupBindsOnlyToLoopback | 591 |
| 19 | ArchitectureDependenciesFlowInward | 677 |
| 20 | LocalDatabaseResetRequiresExactConfirmationAndRemovesOnlyDatabaseFiles | 695 |
| 21 | AuthRoutesExposeNoAlternatePasswordRecoveryEndpoint | 728 |
| 22 | LocalSessionStoreExpiresOnInactivityAndCanBeRevoked | 744 |

Tests 3, 4 and 8 are source/config-text assertions: they read `appsettings*.json`, `messages.ts` and `PasswordField.tsx` from disk and do not execute the code.

### 2.3 Code coverage (coverlet `XPlat Code Coverage`, cobertura)

Overall: line 90.73% (1067/1176), branch 62.53% (232/371).

| Assembly | Line | Branch |
|---|---:|---:|
| **SmartSchoolTimetable.Domain** | **96.36%** | 100% |
| **SmartSchoolTimetable.Application** | **92.93%** | 73.61% |
| SmartSchoolTimetable.Infrastructure (includes Migrations) | 88.42% | 67.85% |
| SmartSchoolTimetable.Api | 91.44% | 59.04% |

Lowest-covered classes: `RealLoginDelay` 0%, `LocalDbContextFactory` 0% (design-time only), `NoLoginDelay` 0%, migration `RemoveEscalatingLoginLockoutFields` 47.9% (Down not run), `UnifiedApiErrorMiddleware` 54.3% line / 38.0% branch (mostly the `StatusCodeFor`/`CodeForStatus` switch arms), `LocalApplicationOptions` 82.4% / 61.1%.

### 2.4 Frontend

| Check | Command | Result |
|---|---|---|
| TypeScript | `npx tsc --noEmit` | exit 0, no output |
| ESLint | `npx eslint .` | exit 0, 0 problems |
| Vitest | `npx vitest run` (v5.0.3) | 2 files, **4 passed** (4) |
| Playwright | `npx playwright test` (1.63.0, Chromium) | **2 passed** (18.2 s) |

Vitest tests:
- `src/i18n/errors.test.ts`: "maps unknown and missing codes to a generic Arabic message"; "localizes validation field and code without Latin text"; "keeps every visible error string Arabic".
- `src/components/PasswordField.test.tsx`: "toggles visibility with a labelled, pressed-state eye control".

Playwright tests (`e2e/auth-flow.spec.ts`, which starts the real Release API DLL on a free loopback port with a temp DB):
- L91 "setup, recovery confirmation, automatic entry, logout, login, and password recovery". This one runs against the real server.
- L140 "validation, not-found, server-stopped, and internal failures stay Arabic". The 422 validation, 404 bootstrap and 500 bootstrap UI checks use `page.route()` **mocked responses**. Only the `/api/v1/no-such-browser-route` 404 (via `page.request`, not the UI) and the server-stopped case hit the real process.

---

## 3. Dependencies

### 3.1 NuGet — vulnerable (`dotnet list package --vulnerable --include-transitive`)

```
SmartSchoolTimetable.Domain / Application / Infrastructure / Api / Tests: no vulnerable packages given the current sources.
CSharpSpikes (not in the .sln):                                          no vulnerable packages given the current sources.
```

### 3.2 NuGet — outdated (`dotnet list package --outdated`)

| Project | Package | Resolved | Latest |
|---|---|---|---|
| Infrastructure | Microsoft.EntityFrameworkCore.Design | 9.0.20 | 10.0.12 |
| Infrastructure | Microsoft.EntityFrameworkCore.Sqlite | 9.0.20 | 10.0.12 |
| Api | FluentValidation.DependencyInjectionExtensions | 12.0.0 | 12.1.1 |
| Api | Microsoft.AspNetCore.OpenApi | 9.0.20 | 10.0.12 |
| Api | Microsoft.EntityFrameworkCore.Design | 9.0.20 | 10.0.12 |
| Tests | coverlet.collector | 6.0.2 | 10.1.0 |
| Tests | Microsoft.AspNetCore.Mvc.Testing | 9.0.20 | 10.0.12 |
| Tests | Microsoft.NET.Test.Sdk | 17.12.0 | 18.10.1 |
| Tests | xunit | 2.9.2 | 2.9.3 |
| Tests | xunit.runner.visualstudio | 2.8.2 | 4.0.0 |

The 10.x versions are a major-version (net10) move; the project targets net9.0.

### 3.3 npm

```
$ npm audit            -> found 0 vulnerabilities
$ npm outdated
@testing-library/jest-dom 6.9.1 -> 7.0.1   @types/node 22.20.5 -> 26.6.4   @vitejs/plugin-react 4.7.0 -> 6.1.1
eslint 9.39.5 -> 10.12.0   eslint-plugin-react-hooks 5.2.0 -> 7.1.1   jsdom 25.0.1 -> 30.1.1
lucide-react 0.468.0 -> 1.51.0   typescript 5.9.3 -> 7.0.2   vite 6.4.3 -> 8.3.2
```
All outdated entries are beyond the declared semver range ("Wanted" equals "Current").

### 3.4 Licenses of direct dependencies

Sources: `.nuspec` in `~/.nuget/packages`, `license` field in `node_modules/*/package.json`.

| Dependency | Version | License | Flag |
|---|---|---|---|
| Microsoft.EntityFrameworkCore.Sqlite / .Design | 9.0.20 | MIT | |
| Microsoft.AspNetCore.OpenApi | 9.0.20 | MIT | |
| FluentValidation(.DependencyInjectionExtensions) | 12.0.0 | Apache-2.0 | |
| coverlet.collector | 6.0.2 | MIT | |
| Microsoft.AspNetCore.Mvc.Testing | 9.0.20 | MIT | |
| Microsoft.NET.Test.Sdk | 17.12.0 | MIT | |
| xunit / xunit.runner.visualstudio | 2.9.2 / 2.8.2 | Apache-2.0 | |
| Google.OrTools (spike only) | 9.15.6755 | Apache-2.0 | |
| **QuestPDF (spike only)** | 2026.9.1 | `LICENSE.md` file: **dual-licensed Community / Professional / Enterprise** (License Selection Guide v3.0, effective 6 July 2026) | **FLAG: not OSI-permissive.** Commercial tiers apply depending on organisation; the guide counts AI-generated code as written by a Developer of the organisation. Already recorded as an open pre-Phase-6 gate in README/DELIVERY_PLAN. |
| Noto Naskh Arabic font (spike) | — | OFL (`spikes/CSharpSpikes/fonts/OFL.txt`) | |
| react / react-dom | 19.3.0 | MIT | |
| @tanstack/react-query | 5.104.1 | MIT | |
| react-router-dom | 7.18.4 | MIT | |
| zustand | 5.0.15 | MIT | |
| lucide-react | 0.468.0 | ISC | |
| @playwright/test | 1.63.0 | Apache-2.0 | |
| typescript | 5.9.3 | Apache-2.0 | |
| vite 6.4.3, vitest 5.0.3, tailwindcss / @tailwindcss/vite 4.3.3, eslint 9.39.5, typescript-eslint 8.71.0, eslint-plugin-react-hooks 5.2.0, jsdom 25.0.1, @testing-library/react 16.3.3, @testing-library/jest-dom 6.9.1, @vitejs/plugin-react 4.7.0, @types/* | — | MIT | |

No production (shipped) dependency is non-permissive. QuestPDF is referenced only by the spike project, which is not in the solution.

---

## 4. Architecture

### 4.1 Actual project reference graph (from the `.csproj` files)

```
Domain          -> (none)                         [no PackageReference]
Application     -> Domain                         [no PackageReference]
Infrastructure  -> Application, Domain            [EFCore.Sqlite 9.0.20, EFCore.Design 9.0.20 (private)]
Api             -> Application, Infrastructure    [AspNetCore.OpenApi, FluentValidation.DI 12.0.0, EFCore.Design (private)]
Tests           -> Api                            [xunit, Mvc.Testing, coverlet, Test.Sdk]
CSharpSpikes    -> (none; not in .sln)            [Google.OrTools, QuestPDF]
```

### 4.2 Dependency rules

| Rule (MASTER §4 / CLAUDE.md) | Status | Evidence |
|---|---|---|
| Domain has no deps | Holds | Domain.csproj has no references |
| Application depends only on Domain | Holds | Application.csproj:4 |
| Infrastructure → Application + Domain | Holds | Infrastructure.csproj:4-5 |
| Api is composition root (refs Application + Infrastructure for DI only) | Holds | Api uses Infrastructure only in `Program.cs:7,46,51` (DI registration and DB init) |
| OR-Tools only behind `ISolver` in Infrastructure | N/A | No OR-Tools in `src/`; `ISolver` does not exist yet |
| Endpoints are thin and use `IMediator` only | **Violated** | No MediatR package. Endpoints inject `ILocalAuthService` directly (`AuthEndpoints.cs:28,51,76,...`). `ARCHITECTURE.md:10` still states "Endpoints dispatch through IMediator." No ADR records the change. |
| No DbContext/Domain entities in endpoints | Holds | `grep` for `DbContext|OwnerAccount|LocalAuditEntry|SmartSchoolTimetable.Domain` in `src/SmartSchoolTimetable.Api/*.cs` finds nothing |
| Architecture tests fail the build on violations | **Partial** | One test only (below); it does not use NetArchTest and checks no endpoint/DbContext/MediatR rule |

### 4.3 Architecture tests

- `ArchitectureDependenciesFlowInward` (`LocalApiTests.cs:677-692`). It uses reflection (`Assembly.GetReferencedAssemblies`) to assert that Domain does not reference Application/Infrastructure/Api, Application does not reference Infrastructure/Api, and Infrastructure does not reference Api.
- No NetArchTest. No test for "endpoints don't reference DbContext/entities", "OR-Tools not in Domain/Application", or "IMediator only".

### 4.4 Endpoints touching DbContext or entities directly

None found. Endpoints call `ILocalAuthService` (Application). `Program.cs:51` calls `LocalInfrastructureRegistration.InitializeLocalDatabaseAsync` at startup; that is a composition-root call, not an endpoint.

---

## 5. API

Source: `AuthEndpoints.cs:13-22`. Status codes were confirmed by a live probe against the Release build (temp DB, port 5197, Production environment).

Every route sits under `/api/v1`. **Global behaviour**, from `LocalRequestSecurityMiddleware` (`LocalSecurity.cs:96-136`):
- Host ≠ `127.0.0.1:{port}` returns **400 `INVALID_HOST`**.
- An Origin header that is present but not canonical returns **403 `INVALID_ORIGIN`**. A POST/PUT/PATCH/DELETE with no Origin also returns 403 `INVALID_ORIGIN`.
- A state-changing request without a matching `X-Local-Launch-Token` returns **403 `INVALID_LAUNCH_TOKEN`**.
- Malformed JSON returns **400 `INVALID_REQUEST`**. A wrong content type returns **415 `UNSUPPORTED_MEDIA_TYPE`**. A wrong method returns **405 `METHOD_NOT_ALLOWED`**. An unknown `/api` path returns **404 `NOT_FOUND`**. An unhandled exception returns **500 `INTERNAL_ERROR`**.
- Every error body is `{code, correlationId, errors[]}` plus an `X-Correlation-ID` header (`UnifiedApiErrorMiddleware.cs:37-56`).

| Method | Route | Auth required | Request | Success response | Error codes returned (observed / code path) |
|---|---|---|---|---|---|
| GET | `/bootstrap` | No | — | 200 `BootstrapResponse{setupRequired, authenticated, username, recoveryCodeAcknowledgementRequired, launchToken, inactivityTimeoutMinutes}`; refreshes cookie if authenticated | global only |
| POST | `/auth/setup` | No (only while no owner) | `SetupRequest{username, password, confirmPassword}` | 201 `SetupResponse{recoveryCode}` + session cookie | 422 `VALIDATION_FAILED` (field codes `REQUIRED`, `USERNAME_TOO_SHORT/LONG`, `PASSWORD_TOO_SHORT/LONG`, `PASSWORD_MISMATCH`); 422 `INVALID_USERNAME` (observed: `"  ab  "` passes validator, fails after trim); 409 `SETUP_ALREADY_COMPLETE` |
| POST | `/auth/login` | No | `LoginRequest{username, password}` | 204 + session cookie | 422 `VALIDATION_FAILED`; 401 `INVALID_CREDENTIALS`; **403** `SETUP_REQUIRED` (the endpoint sets 401, the middleware remaps to 403) |
| POST | `/auth/recovery` | No | `RecoveryRequest{recoveryCode, newPassword}` | 200 `RecoveryResponse{recoveryCode}` (replacement) + session cookie | 422 `VALIDATION_FAILED`; 401 `INVALID_RECOVERY_CODE`; 403 `SETUP_REQUIRED` |
| POST | `/auth/recovery-code/regenerate` | Yes (cookie) | `RecoveryCodeRequest{currentPassword}` | 200 `RecoveryResponse{recoveryCode}` | 401 `UNAUTHENTICATED`; 422 `VALIDATION_FAILED`; 401 `CURRENT_PASSWORD_INCORRECT`; 403 `SETUP_REQUIRED` |
| POST | `/auth/recovery-code/acknowledge` | Yes (cookie) | none (body ignored) | 204 | 401 `UNAUTHENTICATED`; 409 `RECOVERY_MISSING` (session did not issue a code); 403 `SETUP_REQUIRED` |
| POST | `/auth/logout` | No (revokes the cookie's session if present) | none | 204 + cookie deletion | global only |
| POST | `/auth/change-password` | Yes (cookie) | `ChangePasswordRequest{currentPassword, newPassword}` | 204 + cookie deletion (all sessions revoked) | 401 `UNAUTHENTICATED` (no cookie); 422 `VALIDATION_FAILED`; 401 `CURRENT_PASSWORD_INCORRECT`; **200 `{"code":"unauthenticated"}`** when the cookie is present but the session is invalid or expired — **defect D1** |
| GET | `/private/status` | Yes (cookie) | — | 200 `PrivateStatusResponse{status:"authenticated"}` + cookie refresh | 401 `UNAUTHENTICATED` |
| GET | `/openapi/v1.json` | No | — | OpenAPI doc, **Development environment only** (`Program.cs:56-57`) | — |
| any | non-`/api` path | No | — | SPA `index.html` (fallback, `Program.cs:96-97`) | — |

Live probe excerpt for D1:
```
--- POST /api/v1/auth/change-password (bogus cookie)
HTTP/1.1 200 OK
{"code":"unauthenticated","correlationId":"0HNP1BL74ASLO:00000001","errors":[]}
```
Cause: `LocalAuthService.cs:231` returns lowercase `"unauthenticated"`. `AuthEndpoints.cs:193` maps that to 422. `UnifiedApiErrorMiddleware.cs:42` calls `Response.Clear()`, which resets `StatusCode` to 200, and line 43 then uses that 200 as the fallback for any code that is not in `StatusCodeFor`. **Any error code missing from the `StatusCodeFor` switch is therefore sent as HTTP 200.** That currently means `unauthenticated`, `INVALID_SETUP`, `RECOVERY_CODE_REGENERATION_FAILED` and `PASSWORD_CHANGE_FAILED`; the last three are unreachable `??` fallbacks. The frontend treats 200 as success (`api.ts:35-46`), so `SettingsScreen` would show "تم تغيير كلمة المرور" (password changed) even though nothing changed.

There is no generated frontend client; `frontend/src/api.ts` is hand-written. `API.md` has no endpoint table, and its error model (`API.md:23-29`) lists "400 validation" (actual: 422) and "423 temporarily locked" (lockout removed in migration `20261003160000`).

---

## 6. Database

### 6.1 Migrations (EF Core, SQLite)

| Migration | Kind | Effect |
|---|---|---|
| `20261003105756_InitialLocalSchema` | Generated (has `.Designer.cs`) | Creates `AuditHistory` and `Users` (with `FailedLoginCount`, `NextLoginAllowedAt`, `LockoutUntil`) |
| `20261003152200_RecoveryCodeAcknowledgement` | **Hand-written**, no `.Designer.cs` | Adds `Users.RecoveryCodeAcknowledged INTEGER NOT NULL DEFAULT true`, so pre-existing owners are treated as already acknowledged |
| `20261003160000_RemoveEscalatingLoginLockoutFields` | **Hand-written raw SQL**, no `.Designer.cs` | Table rebuild: drops the 3 lockout columns and recreates both unique indexes. Down re-adds the columns. |

`dotnet ef` is not installed on the audit machine, so `has-pending-model-changes` could not be run. By manual comparison, `LocalDbContextModelSnapshot.cs` matches `LocalDbContext.OnModelCreating`.

### 6.2 Resulting schema

**Users** (`OwnerAccount`)

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | INTEGER | NOT NULL | PK `PK_Users`, AUTOINCREMENT |
| OwnerSlot | TEXT | NOT NULL | max 16 (EF), always `"owner"` |
| Username | TEXT | NOT NULL | max 64 (EF) |
| NormalizedUsername | TEXT | NOT NULL | max 64 (EF), `Trim().ToUpperInvariant()` |
| PasswordSalt | BLOB | NOT NULL | 16 bytes |
| PasswordHash | BLOB | NOT NULL | 32 bytes |
| PasswordIterations | INTEGER | NOT NULL | 600000 |
| RecoverySalt | BLOB | NOT NULL | 16 bytes |
| RecoveryCodeHash | BLOB | NOT NULL | 32 bytes (SHA-256) |
| CreatedAt / UpdatedAt | TEXT | NOT NULL | DateTimeOffset |
| RecoveryCodeAcknowledged | INTEGER | NOT NULL | bool |

Indexes: `IX_Users_OwnerSlot` UNIQUE (this is what enforces the single owner), `IX_Users_NormalizedUsername` UNIQUE.

**AuditHistory** (`LocalAuditEntry`): `Id` INTEGER PK AUTOINCREMENT, `OccurredAt` TEXT NOT NULL, `EventType` TEXT(64) NOT NULL, `Target` TEXT(128) NOT NULL, `Summary` TEXT(512) NOT NULL. Index: `IX_AuditHistory_OccurredAt`.

Constraints: PKs and the two unique indexes only. No foreign keys (no relations exist), no CHECK constraints, no concurrency/version columns. EF `HasMaxLength` is not enforced by SQLite TEXT.

Audit events actually written: `OwnerAccountCreated` (`LocalAuthService.cs:64`), `RecoveryCodeRegenerated` (`:137`), and `PasswordChanged` (`:207` for recovery, `:254` for change). Login, logout and acknowledgement are not audited.

### 6.3 SQLite settings

| Setting | Where | Scope |
|---|---|---|
| `Foreign Keys=True` | connection string, `LocalInfrastructureRegistration.cs:20` | every connection |
| `Default Timeout=30`, `Pooling=False` | `:21-22` | every connection |
| `PRAGMA journal_mode=WAL` | `:49`, executed once at startup after `MigrateAsync` | persistent in the DB file; verified by test #1 (`PRAGMA journal_mode` returns `wal`) |
| `PRAGMA synchronous=FULL` | `:49`, same single command | **connection-scoped only**. With `Pooling=False`, later connections use SQLite's compiled default. It is not re-applied per connection and no test checks it. |
| `EnableSensitiveDataLogging(false)` | `:28` | verified by test #3 |

Default DB path: `%LOCALAPPDATA%\SmartSchoolTimetable\timetable.db`, overridable by `Database:Path` (`Program.cs:19-23`).

### 6.4 Backup and reset

- **Backup: not implemented.** No code references the SQLite Online Backup API or any backup routine. Only `DATABASE.md` and ADR 0011 describe it, and they schedule it for Phase 6.
- **Reset:** `--reset-local-database` CLI flag (`Program.cs:9-29`) calls `LocalDatabaseReset.DeleteAfterConfirmation` (`LocalDatabaseReset.cs:5-21`). It prints the full path, requires the exact input `RESET`, then deletes `{db}`, `{db}-wal`, `{db}-shm` and `{db}-journal`. It does not check whether the app is running and writes no audit record (the DB is deleted). Covered by test #20.

---

## 7. Security evidence (per SECURITY.md item)

| SECURITY.md item | Implementation (file:line) | Proving test | Notes |
|---|---|---|---|
| Single owner, setup only when empty | `LocalAuthService.cs:42-46` (gate + existence check); unique index `IX_Users_OwnerSlot` (`LocalDbContext.cs:17`) | #1 (409 on second setup, `Assert.Single(Owners)`) | |
| Auto sign-in after setup | `LocalAuthService.cs:66`; `AuthEndpoints.cs:66` | #1 | |
| Recovery acknowledgement gates entry | API flag `RecoveryCodeAcknowledged`; UI gate `App.tsx:558-562` only if the code is still in memory | #12, Playwright L91 | **Deviation D2:** after a reload, the session cookie survives and the UI goes straight to Home (Playwright L102-103 asserts exactly this). SECURITY.md:11 says "the owner signs in". |
| Recovery code = 16 random bytes, 4×8 uppercase hex | `LocalAuthService.cs:16,287-291` | #1 (regex, length 35, 128 bits) | |
| Recovery code stored as salted SHA-256 only | `Pbkdf2CredentialHasher.cs:40-60` (16-byte salt, `SHA256.HashData(salt‖code)`) | #1 (hash ≠ plaintext, lengths) | Normalised to `[A-Z0-9]` before hashing (`LocalAuthService.cs:272-273`) |
| Recovery consumes code, resets password, issues replacement | `LocalAuthService.cs:175-218` | #10, Playwright L91 | All sessions revoked (`:203`) |
| Regenerate requires current password | `LocalAuthService.cs:110-146` | #12 | |
| Password change requires current password and invalidates session | `LocalAuthService.cs:220-262` (`RevokeAll` at `:250`) | #13 | Revokes **all** sessions, not only the current one |
| PBKDF2-HMAC-SHA256, 600,000 iterations, 16-byte salt, 32-byte key | `Pbkdf2CredentialHasher.cs:9-25` | #2 (independent derivation equality) | Rejects stored iterations below 600,000 (`:29`) |
| Algorithm and iterations stored with the hash | Iterations column only (`OwnerAccount.cs:15`) | #1 asserts 600000 | **No algorithm/version column**; SECURITY.md:17 and ADR 0009 say "algorithm … stored" |
| `FixedTimeEquals` comparisons | `Pbkdf2CredentialHasher.cs:37,51`; username `LocalAuthService.cs:293-299`; launch token `LocalSecurity.cs:16-23` | indirect (#2, #16) | |
| Fixed 1 s delay, no lockout | `LocalAuthService.cs:95-99`; `RealLoginDelay.cs:7-8` | #11 (6 failures, 6×1 s recorded, never locked) | `RealLoginDelay` 0% covered. The delay runs **inside** the global `SemaphoreSlim` (`:80`), so every other auth operation waits behind a failed login. |
| No username enumeration | Same `INVALID_CREDENTIALS` for both cases (`LocalAuthService.cs:95-98`); password is always verified first | not directly tested | Before setup, login returns `SETUP_REQUIRED` (by design) |
| EF command logging Warning by default, Information in Dev | `appsettings.json:6`, `appsettings.Development.json:6` | #3 (reads JSON) | |
| Sensitive-data logging off | `LocalInfrastructureRegistration.cs:28` | #3 (`IsSensitiveDataLoggingEnabled == false`) | |
| Never log passwords, codes, cookies, tokens | Middleware logs fixed strings only (`LocalSecurity.cs:107,118`; `UnifiedApiErrorMiddleware.cs:34`) | #7 (captured logs exclude 2 passwords + 2 codes) | #7 does not check cookie/session id/launch token. The live-probe server log (16 lines) contained no password, code or cookie. The unhandled-exception log omits the exception object (`UnifiedApiErrorMiddleware.cs:34`), so stack traces are not logged at all. |
| Cookie: HttpOnly, SameSite=Strict, Path=/, Secure only on HTTPS | `AuthEndpoints.cs:238-254` | #9 | Probe: `max-age=1800; path=/; samesite=strict; httponly`. With `Never`, there is no Max-Age (browser-session cookie). |
| Inactivity timeout 1–1440 or `Never`, default 30 | `LocalSecurity.cs:40-54`; `LocalSessionStore.cs:22-38` | #5, #15, #22 | Sessions are in memory (`ConcurrentDictionary`), so all are lost on restart. Client-side timer is in `App.tsx:420-441`. |
| Logout revokes session | `LocalAuthService.cs:264-268`; `AuthEndpoints.cs:163-168` | #14 | |
| Kestrel binds 127.0.0.1 only | `Program.cs:15-17` (`Listen(IPAddress.Loopback, port)`), post-start check `Program.cs:101-106` → `LocalSecurity.cs:70-87` | #17 (rejects 0.0.0.0, LAN, ::1, etc.), #18 (starts the real process, inspects bindings) | The post-start check is skipped in the `Testing` environment (`Program.cs:101`) |
| Exact Host check | `LocalSecurity.cs:105-111` | #16 (`localhost:5080`, `127.0.0.1.attacker.example`) | |
| Origin check; missing Origin rejected on state-changing requests | `LocalSecurity.cs:113-122` | #16 (no origin, `http://evil.example`, CORS probe) | |
| No wildcard CORS | No `AddCors`/`UseCors` anywhere in `src/` | #16 asserts no `Access-Control-Allow-Origin` | |
| Per-launch token, in-memory, header | `LocalSecurity.cs:7-24` (32 random bytes, base64url), check `:124-130`; client keeps it in Zustand memory (`state/session.ts`) and sends `X-Local-Launch-Token` (`api.ts:12-15`) | #16 (missing and stale token from a 2nd launch rejected) | The token is served by unauthenticated `GET /bootstrap`; cross-origin reads are blocked by the browser's same-origin policy plus the Host check |
| Security headers | `LocalSecurity.cs:98-103` (nosniff, DENY, no-referrer, CSP, CORP); duplicated in `UnifiedApiErrorMiddleware.cs:46-51` | probe shows CSP and X-Frame-Options; no test asserts the header values | |
| Error envelope never exposes ProblemDetails/prose | `UnifiedApiErrorMiddleware.cs:13-68` | #6 (404, 405, 415, 422, 500, no `title`/`detail`) | Defect D1 (HTTP 200 on unmapped codes) is not covered |
| No `alert`/`confirm`/`prompt`; forms `noValidate` | `grep` finds no `window.alert/confirm/prompt` in `frontend/src`; all 5 forms use `noValidate` (`App.tsx:117,157,296,388,395`) | none automated | |
| Local audit history | §6.2 | #1 (asserts `OwnerAccountCreated`, no secrets in summary) | Only 3 event types exist; nothing else is audited yet |
| SQLCipher NO-GO | none (baseline unencrypted) | n/a | Consistent with ADR 0010 |

---

## 8. Localization

### 8.1 English user-facing strings

UI (`frontend/src`): ESLint rule `localized-ui/no-hardcoded-ui-text` passes. A manual grep of `messages.ts` finds no Latin text in any value. `index.html` is `lang="ar" dir="rtl"` with an Arabic `<title>`.

UI strings **outside the dictionary** (Arabic, but hard-coded):
- `components/PasswordField.tsx:19`: `"إخفاء كلمة المرور"` and `"إظهار كلمة المرور"`. These pass lint because they are assigned through a variable.
- `App.tsx:205`: download filename `"رمز-الاسترداد.txt"`.
- `messages.ts:109`: `Locale = "ar" | "en"`. This is declared but no English dictionary exists.

API / host strings in English that can reach a person:
- `LocalDatabaseReset.cs:8,9,12,19`: the console prompts for the operator reset flow ("This permanently deletes…", "Type RESET to confirm:", "Reset cancelled…", "Local database reset completed.") are **English**.
- Startup exceptions (`LocalSecurity.cs:38,52-53,65,67,75,84-85`) are English and appear in the console or crash output.
- API JSON responses carry codes only, no prose (verified by probe).
- `SmartSchoolTimetable.Api.http` is the template leftover (`/weatherforecast/`, `localhost:5240`). It is a developer artefact.

### 8.2 Error codes and Arabic translations (`frontend/src/i18n/messages.ts:61-95`)

| Code | Arabic |
|---|---|
| CONFLICT | تعذر إتمام الطلب بسبب تعارض في البيانات. |
| CURRENT_PASSWORD_INCORRECT | كلمة المرور الحالية غير صحيحة. |
| INTERNAL_ERROR | حدث خطأ داخلي. أعد المحاولة. |
| INVALID_CREDENTIALS | اسم المستخدم أو كلمة المرور غير صحيحة. |
| INVALID_HOST | عنوان الطلب المحلي غير صالح. |
| INVALID_LAUNCH_TOKEN | انتهت صلاحية رمز الطلب. حدّث الصفحة ثم أعد المحاولة. |
| INVALID_ORIGIN | مصدر الطلب غير مسموح به. |
| INVALID_PASSWORD | كلمة المرور غير صالحة. |
| INVALID_RECOVERY_CODE | رمز الاسترداد غير صحيح أو لم يعد صالحاً. |
| INVALID_REQUEST | تعذر فهم الطلب. راجع البيانات ثم أعد المحاولة. |
| INVALID_SETUP | تعذر إعداد الحساب. راجع البيانات ثم أعد المحاولة. |
| INVALID_USERNAME | اسم المستخدم غير صالح. |
| METHOD_NOT_ALLOWED | هذا الإجراء غير متاح لهذا المسار. |
| NOT_FOUND | المطلوب غير موجود. |
| PASSWORD_MISMATCH | كلمتا المرور غير متطابقتين. |
| RECOVERY_CODE_REGENERATION_FAILED | تعذر إنشاء رمز استرداد جديد. |
| REQUEST_FORBIDDEN | الطلب غير مسموح به. |
| SETUP_ALREADY_COMPLETE | تم إعداد حساب المالك من قبل. |
| SETUP_REQUIRED | يلزم إعداد حساب المالك أولاً. |
| TOO_MANY_REQUESTS | تجاوزت عدد الطلبات المسموح به. انتظر ثم أعد المحاولة. |
| UNAUTHENTICATED | انتهت الجلسة. سجّل الدخول للمتابعة. |
| UNSUPPORTED_MEDIA_TYPE | صيغة البيانات المرسلة غير مدعومة. |
| VALIDATION_FAILED | تحقق من الحقول المحددة ثم أعد المحاولة. |
| RECOVERY_MISSING | رمز الاسترداد غير متوفر. أنشئ رمزاً جديداً. |
| REQUIRED | هذا الحقل مطلوب. |
| USERNAME_TOO_SHORT | اسم المستخدم أقصر من الحد المسموح. |
| USERNAME_TOO_LONG | اسم المستخدم أطول من الحد المسموح. |
| PASSWORD_TOO_SHORT | كلمة المرور أقصر من الحد المسموح. |
| PASSWORD_TOO_LONG | كلمة المرور أطول من الحد المسموح. |
| NETWORK_ERROR (client) | تعذر الاتصال بالتطبيق المحلي. تحقق من تشغيله ثم أعد المحاولة. |
| REQUEST_TIMEOUT (client) | استغرق الطلب وقتاً أطول من المتوقع. أعد المحاولة. |
| UNKNOWN_ERROR (client) | حدث خطأ غير متوقع. أعد المحاولة. |
| CLIPBOARD_FAILED (client) | تعذر نسخ الرمز. انسخه يدوياً من الشاشة. — **unused**; `App.tsx:195` uses `messages.app.codeCopyFailed` instead |

All 29 codes in `ApiErrorCodes.All` (`UnifiedApiErrorMiddleware.cs:104-137`) have an Arabic entry; test #4 enforces this.

### 8.3 Codes without a translation

| Code | Emitted at | In `ApiErrorCodes.All`? | UI result |
|---|---|---|---|
| `unauthenticated` (lowercase) | `LocalAuthService.cs:231` (reachable; see D1) | No | Sent as HTTP 200, so the UI treats it as **success** |
| `PASSWORD_CHANGE_FAILED` | `AuthEndpoints.cs:194` (`??` fallback, currently unreachable) | No | would map to `UNKNOWN_ERROR` |

Codes in the dictionary that the server never emits in the current code: `TOO_MANY_REQUESTS` (no rate limiter), `CONFLICT` and `REQUEST_FORBIDDEN` (status-only fallbacks), `INVALID_SETUP` and `RECOVERY_CODE_REGENERATION_FAILED` (unreachable `??`), `INVALID_PASSWORD` (the validator rejects the same lengths first), `PASSWORD_MISMATCH` as a top-level code (it appears only as a field code).

---

## 9. UI standards

### 9.1 Icons
- Library: `lucide-react` 0.468.0 only (`components.json` declares `iconLibrary: lucide`). No other icon import exists.
- Icons in use: AlertCircle, ArrowRight, Check, Clipboard, GraduationCap, House, KeyRound, LogOut, Printer, RefreshCw, Save, Settings, ShieldCheck, UserRound (`App.tsx:1-16`), Eye/EyeOff (`PasswordField.tsx:1`).
- Icon-only controls: (a) the password toggle `<button>` (`PasswordField.tsx:21-30`) has `aria-label`, `title` and `aria-pressed`, and its icon is `aria-hidden`; (b) none other. The brand `Link` (`App.tsx:446`) has an icon plus visible text plus `aria-label`.
- **No unlabeled icon-only buttons found.** Every shared `Button` has `icon` plus a visible label (lint-enforced). All icons pass `aria-hidden="true"`.
- `ArrowRight` is used for "back to login" (`App.tsx:307`). In RTL, "right" points to the start, so this is directionally correct. There is no CSS mirroring rule for directional icons.

### 9.2 RTL logical-property violations
`grep -E "\b(left|right)\b|margin-(left|right)|padding-(left|right)|border-(left|right)|\b(ml|mr|pl|pr)-[0-9]|float"` over `frontend/src` finds **0 matches**. `styles.css` uses `inline-size`, `block-size`, `padding-inline(-end)`, `margin-inline`, `inset-inline-end` and `border-inline-start`. `text-align` uses `start` (L109) and `center` (L201).

### 9.3 Accessibility labels
- All inputs have `<label htmlFor>` (`App.tsx:119,159,298`; `PasswordField.tsx:15`). The checkbox is wrapped in a `<label>` (`App.tsx:245-248`).
- Alerts use `role="alert"` and status messages use `role="status"`. The recovery code `<output>` has `aria-label` (`App.tsx:224`).
- Missing or questionable labels: the `Card` in `AuthLayout` has `aria-labelledby="page-title"` (`App.tsx:52`), but `Card` renders `<section>`, so this works. The 404 `<section role="alert">` (`App.tsx:489`) contains a nested `AlertMessage` that also has `role="alert"` (two nested alerts).
- No automated accessibility scan (axe or similar) exists in the test suites.

---

## 10. Code quality

| Metric | Count | Details |
|---|---:|---|
| TODO / FIXME / HACK / XXX | 0 | in `src`, `tests`, `frontend/src`, `frontend/e2e` |
| NotImplemented | 0 | |
| placeholder / mock / fake / stub / dummy | 0 code paths | the only hit is the literal attribute name `"placeholder"` in `eslint.config.js:24` |
| Test-only branches in production code | 2 | `Program.cs:48` (`IsEnvironment("Testing")` swaps in `NoLoginDelay`); `Program.cs:101` (skips bound-address check in Testing) |
| Empty catch blocks | 0 truly empty | `App.tsx:427` `.catch(() => undefined)` swallows logout failure on auto-lock; `UnifiedApiErrorMiddleware.cs:30` catches all exceptions and logs without the exception object; `tests/...:647` catch of `HttpRequestException` in a retry loop |
| `any` in TypeScript | 0 | `grep -w any` in `frontend/src` and `frontend/e2e` |
| Nullable suppressions (`!` null-forgiving), production C# | 17 | `AuthEndpoints.cs:37,59(×2),66,69,84(×2),88,104(×2),111,112,132,141,187,188,212`; `Program.cs:82,97`; `LocalInfrastructureRegistration.cs:16` |
| Nullable suppressions, tests | 5 | `LocalApiTests.cs:665,666,780,792,954` |
| Non-null assertions, TS | 3 | `App.tsx:432`, `main.tsx:16`, `e2e/auth-flow.spec.ts:130` |
| `#nullable disable` / `#pragma warning disable` | 4 / 2 | all in EF migration files (generator default) |

### 10.1 Methods or functions over 50 lines

| Lines | Location |
|---:|---|
| 95 | `frontend/src/App.tsx:407` `AppShell` |
| 82 | `frontend/src/App.tsx:179` `RecoveryScreen` |
| 82 | `frontend/src/App.tsx:324` `SettingsScreen` |
| 63 | `frontend/src/App.tsx:503` `Application` |
| 56 | `src/SmartSchoolTimetable.Api/UnifiedApiErrorMiddleware.cs:13` `InvokeAsync` |
| 51 | `frontend/src/App.tsx:80` `SetupScreen` |
| 51 | `frontend/src/App.tsx:262` `RecoveryForm` |
| 96 | `frontend/e2e/auth-flow.spec.ts:140` test body |
| 84 / 74 / 67 / 56 | `LocalApiTests.cs:591,496,34,192` (tests) |
| 157 / 52 | `spikes/CSharpSpikes/Program.cs:102,329` (spike) |

`Program.cs` top-level statements total 110 lines.

### 10.2 Classes or files over 300 lines
- `LocalApiTests` (tests, ~955 lines in one class; all 22 tests plus helpers).
- `AuthEndpoints.cs` file is 341 lines (the static class is ~258; records and validators share the file).
- `App.tsx` is 573 lines, with 11 components in one file.
- `LocalAuthService` is 294 lines (just under the limit).

### 10.3 Duplicated code hot spots
1. Security headers block duplicated: `LocalSecurity.cs:98-103` and `UnifiedApiErrorMiddleware.cs:46-51`.
2. Cookie options duplicated: `AuthEndpoints.cs:243-250` and `:257-263`.
3. Random base64url token generation duplicated: `LocalSecurity.cs:9-12` and `LocalSessionStore.cs:13-16`.
4. Password length rules (12–1024) repeated in 3 validators (`AuthEndpoints.cs:295-296,318-319,338-339`) and again in `LocalAuthService.cs:284-285`. Username rules appear in the validator and in the service.
5. "Gate → get owner → null ⇒ SETUP_REQUIRED → verify password" pattern repeated in 5 service methods (`LocalAuthService.cs:42-46,80-85,118-129,157-162,184-189,233-245`).
6. Frontend: `useMutation → setRecoveryCode → invalidate bootstrap` repeated 3× (`App.tsx:84-92,266-274,329-337`). The "new password ≠ confirm" check is repeated 2×.

### 10.4 Other observed items
- `context.Items["RecoveryCodeRegeneration"]` and `["RecoveryCodeAcknowledged"]` are written (`AuthEndpoints.cs:140,159`) but never read (dead writes).
- `RecoveryGate` (`App.tsx:314-322`) is a pass-through wrapper around `RecoveryScreen`.
- Server state (`bootstrap`) is stored in Zustand (`state/session.ts`) in addition to TanStack Query. The root CLAUDE.md says "Zustand for UI state only".

---

## 11. Deviations, defects and unfinished items

### 11.0 Phase 1.3 resolution status (updated 2026-10-03, tag `phase-1.3`)

This subsection records what Phase 1.3 did about each finding below. The original findings in 11.1–11.5 are kept unchanged as the historical record, except for the correction noted in 11.2.

Verification at `phase-1.3`:
- `dotnet build SmartSchoolTimetable.sln -c Release`: 0 warnings, 0 errors, with `TreatWarningsAsErrors` and `AnalysisLevel=latest-recommended`.
- `dotnet test SmartSchoolTimetable.sln -c Release --no-build`: **41/41**.
- `npx tsc --noEmit` and `npx eslint .`: clean.
- `npx vitest run`: **7/7**.
- `npx playwright test`: **2/2**.
- Coverage (coverlet line/branch): Domain 96.36%/100%, Application 94.70%/76.47%, Api 93.44%/82.53%, Infrastructure 88.72%/70.00%.

| Finding | Status | Evidence |
|---|---|---|
| D1 — unmapped error code returned as HTTP 200 | **DONE** | `UnifiedApiErrorMiddleware.ResolveError` captures the status before `Response.Clear()`; unregistered codes keep the error status or become 500 `INTERNAL_ERROR`. `"unauthenticated"` replaced by `ErrorCodes.Unauthenticated`; dead fallbacks removed. Tests: `ChangePasswordWithBogusOrExpiredSessionCookieReturns401Unauthenticated`, `MiddlewareNeverSendsAnErrorWithASuccessStatus` (9 cases), `ResolveErrorNeverReturnsANonErrorStatusForAnyInput`, `BackendSourceEmitsErrorCodesOnlyThroughRegisteredConstants` (mutation-checked: re-inserting the lowercase literal makes it fail), `EveryErrorCodeConstantIsRegisteredMappedToAnErrorStatusAndTranslated`. Client: `api.ts` rejects 2xx bodies with `code` (`api.test.ts`). |
| D2 — reload skipped the recovery-code gate | **DONE** | `features/auth/RecoveryPendingScreen.tsx` via `AppGate.tsx`. Playwright test 1 asserts that Home and `/settings` are unreachable after reload, a wrong current password is rejected in Arabic, and regeneration plus acknowledgement then enters the app. SECURITY.md, ADR 0009 (amended), DELIVERY_PLAN, TESTING.md and README updated. |
| D3 — `synchronous=FULL` only on startup connection | **DONE** | `SqlitePragmaInterceptor` registered in `AddLocalInfrastructure`; test `EveryEfCoreConnectionAppliesSynchronousFull` (it also proves the interceptor flips OFF→FULL). |
| D4 — failed-login delay inside global semaphore | **DONE** | `LocalAuthService.LoginAsync` awaits the delay after `Release()`; test `FailedLoginDelayRunsAfterTheOperationGateIsReleased`. |
| D5 — `CLIPBOARD_FAILED` unused, `PASSWORD_CHANGE_FAILED` unregistered, dead `context.Items` writes | **DONE** | All removed (`messages.ts`, `AuthEndpoints.cs`). |
| D6 — `SmartSchoolTimetable.Api.http` template | **DONE** | File deleted. |
| Password minimum 12 → 8 (owner request) | **DONE** | `CredentialRules.PasswordMinLength = 8` used by validators and service; `frontend/src/lib/credentialRules.ts`; test `PasswordMinimumIsEightCharacters`; the Playwright setup uses an 8-character password; docs updated. |
| Playwright mocked validation/404 | **DONE** | Validation: real 422 from `/auth/login`. 404: real API JSON and the real unknown-route page. Stopped server is now real (the server is killed after a real page load). Only the 500 stays mocked. |
| `App.tsx` monolith, `RecoveryGate`, nested `role="alert"`, hard-coded strings, `in` lookup | **DONE** | `App.tsx` is now 9 lines; one component per file under `features/auth`, `features/home`, `features/settings`, `layout/`, `components/`; hooks in `lib/` and `features/auth/use*.ts`. Playwright asserts exactly one alert on the not-found page. `showPassword`/`hidePassword`/`recoveryCodeFileName` are in the dictionary. `Object.hasOwn` is used in `i18n/errors.ts`. |
| Duplicated headers, cookie options, token generation, password rules | **DONE** | `LocalSecurityHeaders`, `SessionCookie` (`LocalHttpPolicies.cs`), `SecureToken` (Application), `CredentialRules` + `ValidPassword()`/`ValidUsername()` rule extensions (`AuthValidators.cs`). |
| Server state duplicated in Zustand | **DONE** | Bootstrap is read only from the TanStack Query cache (`useBootstrap`; `api.ts` reads the launch token via `queryClient.getQueryData`). `state/session.ts` holds only `recoveryCode` and `recoveryFormOpen`. |
| Also found while splitting: after recovery, logout showed the recovery form | **DONE** | `RecoveryForm` closes the flag after a successful recovery; Playwright test 1 asserts logout returns to the login screen. |
| No warnings-as-errors / analyzers / `.editorconfig` | **DONE** | `Directory.Build.props`, `.editorconfig`, `spikes/Directory.Build.props` (isolates the spike). Findings fixed: CA1848 (×3, via `LocalLog` source-generated logging) and CA1305 (×3 in tests). |
| Architecture tests missing endpoint/DbContext and OR-Tools rules | **DONE** | `ArchitectureTests.cs`: `ApiDoesNotReferenceDomainOrEntityFrameworkCore`, `EndpointsAndOtherApiTypesDoNotUseDbContextOrDomainEntities`, `OrToolsIsNotUsedByDomainOrApplication`. Reflection plus a source scan; NetArchTest was not added (no new dependency). |
| Reset console prompts in English | **DONE** | `LocalDatabaseReset` messages are Arabic constants and the console uses UTF-8. The typed confirmation word stays ASCII `RESET` by design. Test asserts Arabic and no English. |
| MediatR/`IMediator` absent; Serilog, health endpoints, CI, generated OpenAPI client absent | **NOT DONE (deferred by decision)** | `adr/0013-phase-1-scope-trimming.md` (Proposed, awaiting owner approval). ARCHITECTURE.md and OBSERVABILITY.md corrected to match. |
| ARCHITECTURE/API/DATABASE/SECURITY describe non-existent behaviour | **DONE** | API.md: endpoint table, 422 (not 400), no 423. DATABASE.md: real columns, pragmas, constraints. SECURITY.md: iteration count stored, algorithm fixed in code (text fixed; **no hash-version column added**). |
| CHANGELOG missing Phase 1/1.1/1.2 | **DONE** | Entries for 1, 1.1, 1.2 and 1.3 added. |
| No backup procedure | **DONE (interim)** | README "Interim database backup": one PowerShell command copying `timetable.db*` with the app stopped; verified on temp files. The Phase 6 online backup remains NOT DONE. |
| CLAUDE.md status | **DONE** | Updated to Phase 1.3. The audit's original claim about it was wrong; see the correction in 11.2. |
| Spikes folder not deleted after Phase 0 approval | **NOT DONE** | Not in the Phase 1.3 scope; still tracked. |
| Test #7 does not assert cookie/session id/launch token absence from logs | **NOT DONE** | Not in the Phase 1.3 scope. |
| English startup exception messages (`LocalSecurity.cs`) | **NOT DONE** | Operator/developer-facing; not in the Phase 1.3 scope. |
| Login/logout/acknowledge not audited; audit shape reduced vs MASTER §5 | **NOT DONE** | Not in the Phase 1.3 scope; covered by ADR 0008 local-history scope only informally. |
| QuestPDF licence gate, 40/54 solver risk, Phase 6 backup, SQLCipher NO-GO, WebView2 | **NOT DONE (open by plan)** | Unchanged; tracked in DELIVERY_PLAN/README. |
| Unrelated: untracked `temp_check/` directory appeared in the working tree during this session (17:52) | **Not touched** | Not created by this work; left uncommitted for the owner to review. |


### 11.1 Defects
- **D1 (High):** Error codes not listed in `UnifiedApiErrorMiddleware.StatusCodeFor` are returned with HTTP **200**, because `Response.Clear()` resets the status before the fallback is read (`UnifiedApiErrorMiddleware.cs:42-43`). Reachable path: change-password with an expired or invalid session cookie returns `200 {"code":"unauthenticated"}` (live probe), and the UI shows "password changed". The lowercase code (`LocalAuthService.cs:231`) is also absent from `ApiErrorCodes` and the Arabic dictionary. No test covers it.
- **D2 (Medium, spec mismatch):** A reload before acknowledging the recovery code skips the gate and lands on Home without a sign-in, because the session survives (Playwright `auth-flow.spec.ts:102-103` asserts this). SECURITY.md:11, ADR 0009 and the DELIVERY_PLAN Phase 1 acceptance criterion all say the owner must sign in. Settings shows a warning (`App.tsx:384-385`) but nothing blocks use.
- **D3 (Low):** `PRAGMA synchronous=FULL` applies only to the startup connection (`LocalInfrastructureRegistration.cs:49`, `Pooling=False`).
- **D4 (Low):** Failed-login 1 s delay is held inside the global semaphore (`LocalAuthService.cs:80-99`), which serializes all auth operations during the delay.
- **D5 (Low):** `CLIPBOARD_FAILED` is defined but unused. `PASSWORD_CHANGE_FAILED` is used but not registered. There are dead `context.Items` writes.
- **D6 (Low):** `SmartSchoolTimetable.Api.http` is a template leftover pointing at `/weatherforecast` on port 5240.

### 11.2 Deviations from project CLAUDE.md
- ~~`CLAUDE.md` says "Active phase: Phase 0 documentation only"…~~ **Audit error, corrected in Phase 1.3:** the committed `CLAUDE.md` at `1c032f3` already said "Phase 1.2 React migration and owner-authentication acceptance". The original claim quoted a stale context copy rather than the file. The real gap was smaller: its status line said "in progress". Owner `approved` messages are not recorded in the repo, so approval could not be verified.
- "Never log … cookies, session tokens": implemented. Test #7 covers only passwords and recovery codes.

### 11.3 Deviations from the root `Desktop/CLAUDE.md` and MASTER_EXECUTION_PROMPT_v2
Superseded by owner-accepted ADR 0008/0009 (documented scope change, not silent): multi-tenancy/TenantId/RLS, PostgreSQL, Redis, SignalR, worker container, JWT, refresh tokens, RBAC, rate limiting/lockout, Docker/Compose, PWA/offline sync, 2-tenant seed data.

**Not covered by any ADR:**
- MediatR/CQRS/`IMediator` endpoints are absent (§4.2); ARCHITECTURE.md:10 claims otherwise.
- "Warnings as errors, analyzers on, `.editorconfig`" is not configured.
- NetArchTest-based architecture rules are absent; the endpoint→DbContext/Domain rule is not tested.
- Serilog structured logging is absent (default `Microsoft.Extensions.Logging`).
- Health endpoints `/health/live` and `/health/ready` are absent.
- CI (GitHub Actions) is absent (no `.github/`).
- OpenAPI contract with a generated frontend client: OpenAPI is Development-only and the client is hand-written.
- Feature-based frontend folders: all screens are in `App.tsx`.
- The `/spikes` folder should be "deleted after approval" (MASTER §1). It is still tracked.
- The audit log shape in MASTER §5 (before/after JSON, correlation id) is reduced to `EventType/Target/Summary` (SECURITY.md describes the lighter shape, but no ADR records it).

### 11.4 Deviations from the ADRs and docs (documentation describing non-existent behaviour)
- `ARCHITECTURE.md:10`: "Endpoints dispatch through IMediator". Not true.
- `API.md:23-29`: "400 validation" (actual 422) and "423 temporarily locked" (lockout removed). No endpoint table.
- `DATABASE.md:20-21`: "recovery-code hash, nullable after use" (actual NOT NULL, replaced on use); "failed-login count, last failure time, temporary lockout-until" (columns dropped by `20261003160000`). `DATABASE.md:33` lists check constraints and optimistic version columns; none exist.
- `SECURITY.md:17` and ADR 0009: "algorithm … stored with the hash". Only iterations are stored.
- `SECURITY.md:11`, ADR 0009, DELIVERY_PLAN:20: reload ⇒ sign-in. Actual: no sign-in (D2).
- `DELIVERY_PLAN.md:42` cites the Playwright test as "validation, not-found, network, and internal failures stay Arabic". The actual title is "…server-stopped…".
- `CHANGELOG.md` has **no entries for Phase 1, 1.1 or 1.2**. The latest entry is Phase 0.4, and its line 10 reads "Phase 1 has not started". The Phase transition rule requires a CHANGELOG update.
- `TESTING.md:25` says Playwright "verifies … validation … unexpected 500". Those UI checks use mocked routes (§2.4).

### 11.5 Unfinished or known-open items (already recorded in repo docs)
- QuestPDF licensing gate (pre-Phase 6).
- 40-section/54-teacher solver risk (Phase 4).
- Backup/restore is not implemented (Phase 6).
- Audit history covers only 3 auth events.
- SQLCipher NO-GO.
- WebView2 not evaluated.

---

## 12. Spec status

Phases are judged against the repo's own `DELIVERY_PLAN.md`, which the owner-accepted ADR 0008 made the operative plan. Against MASTER_EXECUTION_PROMPT_v2's original Phase 1 (Docker, PostgreSQL, Redis, refresh tokens, RBAC, RLS, CI), Phase 1 is **NOT DONE by design**, because that scope is superseded.

| Phase | Status | Evidence |
|---|---|---|
| **0** (docs + spikes) | **DONE**, with one open item | All 11 MASTER §1 docs plus 12 ADRs exist. Spikes exist with results (`spikes/README.md`, `benchmark-results.txt`, `rtl_questpdf_sample.pdf/.png`). Tags `phase-0`, `phase-0.3`, `phase-0.4`. Open: spikes were not deleted after approval (MASTER §1). The owner's `approved` reply is not recorded in the repo. |
| **1** (local foundation + owner auth) | **PARTIAL** | Works and tested: build 0/0, 22/22 .NET tests, loopback binding, Host/Origin/token, cookie, PBKDF2, recovery, inactivity, migrations/WAL, audit (§2, §7). Gaps: CHANGELOG not updated (§11.4); acceptance criterion "reload ⇒ sign-in" not met (D2); "every failure path returns the unified error" broken by D1 (HTTP 200); warnings-as-errors/analyzers and IMediator not implemented (§11.3); API.md and DATABASE.md describe removed behaviour. Tag `phase-1` exists. |
| **1.1** (hardening: credential/recovery params, EF log levels, secret-log test, cookie test, password toggle) | **PARTIAL** | Every item listed in commit `4c50a90` has code and a passing test (#2, #3, #7, #9, #8 and the Vitest toggle test). Gap: no CHANGELOG entry. Test #7 does not cover cookie, session or token values. |
| **1.2** (React UI, Arabic error contract) | **PARTIAL** | React 19/Vite/TS strict UI served from `wwwroot`. tsc/eslint/vitest/playwright all pass (4 + 2 tests). 29/29 API codes have Arabic. RTL logical CSS has 0 violations. lucide-only icons with no unlabeled icon buttons. Gaps: D1 makes the UI report success on a failed password change; the lowercase `unauthenticated` code has no translation; Playwright validation, 404 and 500 UI paths are mocked; two hard-coded strings sit outside the dictionary; the English operator reset prompts remain; Zustand holds server state; no CHANGELOG entry. README:3 states "Phase 1.2 is under owner acceptance". |
| 2+ | NOT STARTED | No school-setup code exists. Correctly gated. |
