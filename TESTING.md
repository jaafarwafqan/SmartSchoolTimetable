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

## Phase 1.3 test inventory
- .NET (41): `LocalApiTests.cs` (25 tests), `ErrorContractTests.cs` (12 test cases: 3 facts + 9 theory rows), `ArchitectureTests.cs` (3), `LocalAuthServiceTests.cs` (1). The per-criterion mapping is in [DELIVERY_PLAN.md](./DELIVERY_PLAN.md).
- Vitest (7): `src/i18n/errors.test.ts` (3), `src/components/PasswordField.test.tsx` (1), `src/api.test.ts` (3: 2xx-with-code is an error, unregistered code maps to the generic Arabic message, launch token is read from the bootstrap query cache).
- Playwright (2): `setup, blocked reload until a new code is confirmed, logout, login, and password recovery`; `real validation and not-found responses, mocked 500, and a stopped server stay Arabic`.

## Later-phase acceptance suites
- Phase 4 infeasibility test must construct a conflict involving two teachers, a shared lab, and a blocked period; the diagnostic must identify the conflict groups and actionable correction, not merely report infeasible.
- Large solver-risk comparison uses the same independently verified feasible 40-section/54-teacher workload and records status, first-solution/total time, objective/bound, memory method, and independently checked hard constraints for baseline, two-stage, decomposition, and hints.
- Phase 6 SQLite backup tests verify online backup is a self-contained, integrity-checked single file in WAL mode. A copy-based backup must checkpoint/quiesce/close before copying; directly copying an active main `.db` is unsupported.
- Phase 8 reruns setup/login/recovery/auto-lock, loopback listener and localhost-attack tests against the packaged build, with external networking disabled.

## Quality expectations and scope
- Domain and scheduling logic: 90%+ coverage target.
- Do not add acceptance tests for multi-tenancy, RBAC, refresh-token rotation, remote sync, or multi-user concurrency; these are out of scope.
- Phase 0 validation is complete documentation and isolated spikes; current application tests are Phase 1/1.2 implementation validation, not Phase 2 work.
