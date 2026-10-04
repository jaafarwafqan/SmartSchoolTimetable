# Testing Strategy

## Test layers
- .NET xUnit unit/integration tests cover credentials, auth lifecycle, recovery, session expiry, SQLite migrations/WAL, audit history, unified API errors, request security, loopback binding, and architecture.
- Vitest/Testing Library tests cover Arabic error mapping and accessible password visibility.
- Playwright tests exercise the real built React app and API in Chromium, including setup/recovery confirmation, automatic sign-in, logout/login, wrong password, validation, not-found, offline server, and unexpected 500 paths.
- Scheduling tests in future phases cover domain rules, property-based hard-constraint checks, and measured solver behavior.

## Required security and authentication scenarios
- Setup creates exactly one owner, signs them in automatically, and displays a one-time recovery code with clear store/print guidance. Acknowledgment gates entry.
- Reload before acknowledgment must neither strand the owner nor let them in: the signed-in owner sees a blocking screen (Home and `/settings` unreachable) that permits recovery-code regeneration only with the current password; the former code is invalidated and the new one is shown once and must be acknowledged.
- Password and recovery code are never stored in clear text or written to logs. PBKDF2-HMAC-SHA-256 uses at least 600,000 iterations, and comparisons use `FixedTimeEquals`.
- Recovery code resets the password once, is consumed, and is replaced with a one-time code. No other password-recovery route exists.
- Failed login has a fixed one-second delay only. The delay is injected in tests; no test waits a real second. Do not implement escalating delays or temporary lockout.
- Password change requires the current password and revokes the session. Logout revokes the session. Inactivity timeout locks the authenticated session; `Never` disables inactivity expiry.
- Cookie test asserts HttpOnly, SameSite=Strict, Path=/, and the intentional lack of Secure on loopback HTTP.
- Kestrel startup test inspects actual bound addresses and fails unless exactly `127.0.0.1`. Include rejection of wildcard, LAN, and non-canonical loopback binding configurations.
- Host/Origin protections reject malicious or missing Origin on state-changing requests, DNS-rebinding-style Host changes, absent/stale per-launch tokens, and cross-origin attempts. No wildcard CORS is allowed.
- EF Core database-command logs are Warning by default and Information only in Development; sensitive-data logging is off.

## Unified API errors and localization
- Backend test enumerates all `ApiErrorCodes` and fails if the Arabic dictionary is missing any entry.
- Framework-generated failures, model binding, validation, unsupported methods/media, Origin/token failures, and unhandled exceptions must return `{ code, correlationId, errors }`, with no default ProblemDetails title/detail or user-readable server text.
- FluentValidation errors use field/code pairs only. The UI maps every API code to Arabic, handles unknown/missing codes with a generic Arabic fallback, and localizes offline/network/timeout errors.
- Playwright verifies wrong password, validation, 404 route, stopped/unavailable server, and unexpected 500 alerts contain no Latin letters. Validation (real 422 from the login endpoint), the unknown API route (real 404 JSON), the unknown app route, and the stopped server use the real API process; only the unexpected 500 is mocked with `page.route()` because the real server cannot produce it on demand.
- Error codes: `ErrorContractTests` verifies every `ErrorCodes` constant is registered with an HTTP error status and an Arabic message, scans `src/` so codes are only emitted through those constants, and proves the middleware never returns 2xx/3xx for an error.
- Set `<html lang="ar" dir="rtl">`. Forms use `noValidate`; never use browser-native validation popups, `window.alert`, `window.confirm`, or `window.prompt`.
- Generated PDF/Excel, backup, and import-report user-facing text must be Arabic. Numbers and dates follow tenant preferences once implemented.

## Frontend design enforcement
- The only icon library is `lucide-react`. Every action/navigation item has an icon and an Arabic visible label. Password visibility/close controls are the only permitted icon-only actions and require Arabic `aria-label` and `title`.
- Password eye controls are inside the field at its logical end, use `aria-pressed`, and toggle visibility accessibly.
- ESLint rejects hard-coded JSX text/labels, requires icon plus label on shared `Button` components, and requires an accessible label on native buttons. Review new screens for RTL directional-icon mirroring, logical CSS properties, consistent stroke/size, and WCAG AA contrast.
- Review native browser APIs, form validation, and all translated setup/login/recovery/password/settings messages whenever a screen changes.

## Phase 1.4 test inventory
- .NET (45):
  - `LocalApiTests.cs` (25)
  - `ErrorContractTests.cs` (3 facts + 9 theory rows)
  - `ArchitectureTests.cs` (3)
  - `LocalAuthServiceTests.cs` (1)
  - `InactivityTimeoutSettingTests.cs` (4): runtime apply without restart, invalid/missing/unauthenticated changes, domain choices, and no pending EF model changes
- Vitest (32):
  - `i18n/errors.test.ts` (3)
  - `components/PasswordField.test.tsx` (1)
  - `api.test.ts` (3)
  - `styles/tokens.contrast.test.ts` (25): every pair documented in DESIGN_SYSTEM.md, plus `line-strong` on `surface` at 3:1
- Playwright (3):
  - `auth-flow.spec.ts` (2), which now also changes the inactivity auto-lock in Settings.
  - `design-quality.spec.ts` (1): axe (no serious or critical violations) on setup, recovery code, recovery pending, settings, login and the recovery form; screenshots of login and settings at 375/768/1024/1440 px with no horizontal scroll. Baselines are in `frontend/e2e/design-quality.spec.ts-snapshots/` (Windows/Chromium). Update them with `npx playwright test design-quality --update-snapshots` after an approved design change.
- Lint: `npm run lint` runs ESLint (localization, icon + label, design-system rules) and Stylelint (`color-no-hex`, logical properties, no `font-family` outside `tokens.css`, no `px` font sizes).

## Phase 2 test inventory (updated per checkpoint)
Checkpoint 2A:
- .NET (79): the Phase 1.4 suite plus:
  - `Phase2/SchoolSetupDomainTests.cs`: Arabic normalization, profile and year/term invariants, image signatures, domain-error mapping and list queries.
  - `Phase2/SchoolSetupServiceTests.cs`: Application services against an in-memory `FakeDataStore` (not found, stale versions, record in use, save conflicts, asset clean-up).
  - `Phase2/SchoolProfileApiTests.cs` and `Phase2/AcademicYearApiTests.cs`: every endpoint over the real HTTP pipeline and a temporary SQLite database. They cover the session requirement, validation, 409 conflicts, and uploads including SVG, type mismatch and 413.
  - `ArchitectureTests.FeatureFoldersRespectDependencyDirection`: a feature folder may depend only on shared folders.
- Coverage (Cobertura, `dotnet test --collect:"XPlat Code Coverage"`): Domain 98.9% and Application 97.4% of lines (target ≥ 90%).
- Vitest (40): adds `lib/format.test.ts` (numerals, dates, times, ranges) and `components/ui/menu.test.tsx`.
- Playwright (4): adds `phase2-school.spec.ts`, which covers:
  - profile validation and focus on the first invalid field;
  - logo upload and SVG rejection;
  - a stale edit between two pages (Arabic conflict message and reload);
  - creating a year and a term, the dashboard checklist and the lock screen;
  - axe on the dashboard, profile and years screens, and dashboard screenshots at 375/768/1024/1440 px.

Checkpoint 2B:
- .NET (85): adds domain rule coverage for ISO working days, period generation, ascending/nonoverlapping lesson and break rows, and bell options; API coverage for unauthenticated access, working-week validation, conflict versions, bell settings, shift periods, generated drafts and year structure copying.
- Vitest (41): adds Web Audio tone-preview cleanup and synthesized oscillator checks.
- Playwright (4): all Phase 1/2A end-to-end tests pass; no Phase 2B browser setup journey or axe screen coverage has been added yet.
- `dotnet ef migrations has-pending-model-changes` reports no pending changes after `20261003200446_Phase2BTimetableStructure`.

Checkpoint 2C (after the owner-change review):
- .NET (95): adds `Phase2/StagesSectionsTests.cs` (weekly capacity rule, archive/restore/copy, service rules for duplicates, archive, delete, `STAGE_ARCHIVED`, save-failure mapping, paging and search, year copy linking sections to their copied stage and shift, shift delete in use, protected routes without session, Origin or launch token).
- Coverage: Domain 97.7% and Application 94.1% of lines.
- Vitest (46): adds `periodRows.test.ts` (row-level API errors, lesson numbering, new rows), tone names matching the API, and `styles/customProperties.test.ts`, which fails when CSS uses an undefined `var(--x)`.
- Playwright (4): `phase2-school.spec.ts` now also creates a shift, checks generator validation, shows a row-level overlap error, saves generated periods, adds a stage and a section (capacity 30 = 5 days × 6 lessons), runs axe on both screens, and records period screenshots at 375/768/1024/1440.

## Later-phase acceptance suites
- Phase 4 infeasibility test must construct a conflict involving two teachers, a shared lab, and a blocked period; the diagnostic must identify the conflict groups and actionable correction, not merely report infeasible.
- Large solver-risk comparison uses the same independently verified feasible 40-section/54-teacher workload and records status, first-solution/total time, objective/bound, memory method, and independently checked hard constraints for baseline, two-stage, decomposition, and hints.
- Phase 6 SQLite backup tests verify online backup is a self-contained, integrity-checked single file in WAL mode. A copy-based backup must checkpoint/quiesce/close before copying; directly copying an active main `.db` is unsupported.
- Phase 8 reruns setup/login/recovery/auto-lock, loopback listener and localhost-attack tests against the packaged build, with external networking disabled.

## Quality expectations and scope
- Domain and scheduling logic: 90%+ coverage target.
- Do not add acceptance tests for multi-tenancy, RBAC, refresh-token rotation, remote sync, or multi-user concurrency; these are out of scope.
- Phase 0 validation is complete documentation and isolated spikes; current application tests are Phase 1/1.2 implementation validation, not Phase 2 work.
