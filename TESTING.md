# Testing Strategy

## Test layers
- Unit tests for domain rules, local authentication policy, recovery-code lifecycle, and validation
- Integration tests for SQLite persistence, migrations, local sessions, and loopback listener configuration
- E2E browser tests for setup/login, recovery, auto-lock, local timetable work, exports, backups, and imports
- Architecture tests for clean layering and ensuring solver types remain in Infrastructure
- Property-based tests for timetable hard constraints

## Required security scenarios
- First-run setup creates exactly one owner, clearly warns recovery code is the only reset path, and discloses the one-time recovery code only once with store/print instructions.
- Passwords and recovery codes are never stored in clear text; change password requires current password.
- Recovery code resets the password once, is consumed, and produces a replacement shown once; lost password plus lost code has no recovery route.
- Incremental failure delay and temporary lockout activate and expire as configured.
- Inactivity timeout invalidates the session and returns the browser to login.
- Kestrel actual bound address is exactly `127.0.0.1`; startup test fails for `0.0.0.0`, wildcard, or LAN binds.
- Malicious cross-origin page attempts are rejected: wildcard CORS is absent, invalid/missing Origin and Host fail, SameSite cookies are enforced, and state-changing requests without the current per-launch token fail.
- Verify canonical host/origin handling against DNS-rebinding-style Host changes and alternate loopback aliases.

## Phase 8 acceptance tests
Run the complete setup/login/recovery/auto-lock, listener-binding, and localhost-attack suites again against the packaged final application. Include offline operation without internet access, local backup/restore, and browser-based login. Optional WebView2 and SQLCipher features are separately gated and are not assumed to exist.

## Phase 4 solver diagnostic tests
Include an explicitly constructed infeasible case where two teachers/assignments contend for one shared lab in the same period and one of the assignments is additionally blocked in its only remaining period. Verify that the assumption/relaxation diagnostic names both teachers, the affected section/subject, the shared lab, the blocked-period rule, and an actionable correction. Assert the expected conflict groups rather than accepting a generic infeasible status.

For large-instance performance investigations, first construct and independently establish a feasible 40-section / 54-teacher workload/availability input (54 teachers is only an aggregate lower bound). Compare the two-stage, decomposition, and solver-hint approaches against that same input. Record solver status, first-solution and total timings, objective/bound, memory measurement method, and hard-constraint validation for each approach and baseline.

## SQLite backup tests
In WAL mode, verify online backup produces a self-contained single-file database that opens and passes integrity checks. Verify the quiesce/checkpoint/close/copy alternative. Ensure a direct copy of the active main database is not treated as a supported backup.

## Quality expectations
- Domain + scheduling logic: 90%+ coverage.
- Critical product flows: account setup, login, password change, recovery, timetable generation/edit/versioning, publish/rollback, print/export, backup/restore, and imports.
- Do not test multi-tenant isolation, RBAC, refresh-token rotation, remote sync, or multi-user behavior; these are out of scope.

## Phase 0 validation
Phase 0 contains documentation and isolated spikes only; it does not implement application authentication or production security controls.
