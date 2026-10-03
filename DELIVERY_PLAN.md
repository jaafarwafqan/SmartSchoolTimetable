# Delivery Plan

## Phase 0 - Documentation and de-risking
- Architecture and ADRs; isolated solver and Arabic PDF spikes
- Scope clarified to one local owner account, loopback-only UI/API, local SQLite, and no remote services
- Commit and tag phase documentation; no application code until exact owner approval

## Phase 1 - Local application foundation and owner authentication
- Single-machine ASP.NET Core application, browser-based setup/login screen, local SQLite, and clean architecture
- Exactly one owner account at runtime; no RBAC, permission matrix, refresh tokens, remote API, or multi-user concurrency support
- First-run owner setup with one-time recovery code; password hashing, incremental failure delay/temporary lockout, current-password change flow, local session, configurable inactivity auto-lock
- Localhost protections: Kestrel binds only to `127.0.0.1`; exact Host/Origin validation; no wildcard CORS; SameSite=Strict HttpOnly cookie; per-launch random token on every state-changing request
- Lightweight local audit history without user attribution

### Phase 1 acceptance criteria
- First-run setup creates exactly one owner account, warns the recovery code is the only reset path, and shows it once with store/print instructions.
- Password and recovery code are never stored in clear text; recovery code resets the password once, is consumed, and is replaced with a new one-time code; no other password-recovery path exists.
- Password change requires the current password. Login failure delay and temporary lockout, session invalidation, logout, and configurable inactivity auto-lock are tested.
- Authenticated application routes remain inaccessible until the login screen succeeds.
- An automated startup test inspects Kestrel's actual address and fails unless it is exactly `127.0.0.1`; demonstrate rejection of `0.0.0.0`, wildcard, and LAN binds.
- Browser security tests reject invalid Host/Origin, confirm no wildcard CORS, enforce SameSite cookies, and reject state-changing requests without the current per-launch token. Include a malicious cross-origin page and DNS-rebinding-style Host test.
- SQLite migrations apply and the local audit history records required events.

## Phase 2 - School setup
- School Profile, teachers, subjects, stages, sections, shifts, bell system, academic calendar

## Phase 3 - Workload and capacity
- Workload, resources, scheduling profiles, capacity analysis, deterministic validation

## Phase 4 - Local CP-SAT generation
- C# Google.OrTools, local background generation service, diagnostics, cancellation, deterministic mode
- No generation worker container, remote queue, or PostgreSQL advisory lock

## Phase 5 - Timetable lifecycle
- Timetable versioning, manual editor, approval, publication, rollback, and comparisons

## Phase 6 - Local reporting and data portability
1. First setup task: render the QuestPDF Arabic A4 landscape proof in the target packaged runtime using Cairo or Noto Naskh Arabic. Inspect rendered PNGs for glyph joining, RTL column order, clipping, and page layout; capture a visual regression baseline.
2. **Phase 0 result:** QuestPDF feasibility GO. .NET 9, QuestPDF 2026.9.1, and Noto Naskh Arabic produced a visually inspected, one-page RTL PDF.
3. **Production go/no-go:** GO only after the packaged-runtime render and visual regression pass. Otherwise NO-GO for QuestPDF release and open an ADR to assess a pinned local HTML-to-PDF renderer. No client-side PDF library.
- PDF/Excel export, `window.print()` path, local backup/restore, and import
- All core reporting runs without an internet connection.

## Phase 7 - School operations
- Attendance, monitoring, dashboard, and local lesson status

## Phase 8 - Offline/package security and release validation
- Repeat setup/login/recovery/auto-lock, loopback binding, and localhost-attack acceptance suites against the packaged release.
- Verify core workflows, generation, reporting, backup/restore, and imports work with external networking disabled.
- Browser-based login remains the baseline. Optional WebView2 standalone shell requires separate acceptance and must retain the same authentication/security checks.
- **SQLCipher is optional:** perform the go/no-go decision documented in ADR 0010. Default is NO-GO unless key management, recovery, packaging, performance, and backup implications are resolved and tested.
- No RBAC, refresh-token rotation, tenant isolation, remote sync, or multi-user acceptance tests.

## Approval gate
Owner must reply exactly `approved` before Phase 1 starts.
