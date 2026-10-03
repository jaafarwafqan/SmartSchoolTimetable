# ADR 0009: Local owner authentication and localhost protection

- Status: Accepted by owner scope clarification
- Date: 2026-10-03

## Context
The product has exactly one local owner account and no external identity provider, public endpoint, roles, permissions, or multi-user workflow. A local browser application is nevertheless exposed to requests initiated by malicious websites.

## Decision

### Initial setup and recovery
- On first run, a separate setup screen creates the owner account only if the local user table is empty.
- Generate a cryptographically random recovery code of at least 128 bits of entropy. Display it once with a clear instruction to store it securely or print it.
- Store only a one-way hash of the code. It is the sole password-reset mechanism.
- Successful recovery changes the password, consumes the presented code, and issues a replacement code shown once. There is no email, security-question, administrator, or support reset path.
- If both password and current recovery code are lost, there is no password recovery. Destructive reinitialization loses local data and is not recovery.

### Password and session
- Never store plaintext credentials.
- Preferred password hash: Argon2id with memory 64 MiB, 3 iterations, parallelism 1, random salt, and 32-byte output, using a vetted maintained implementation. If that dependency is not accepted, use .NET PBKDF2-HMAC-SHA-256 with random salt, at least 600,000 iterations, and 32-byte output. Store version/parameters and calibrate upward for supported hardware.
- Persist failed-attempt state. Escalate delays after consecutive failures (1, 2, 4, 8 seconds, capped at 30 seconds) and temporarily lock the account for 15 minutes after five consecutive failures. Successful authentication clears the failure counter.
- Baseline session is a server-side local session in an HttpOnly, SameSite=Strict cookie, rotated after login; no access/refresh token pair. Secure is required if the local origin uses HTTPS.
- A configurable inactivity timeout invalidates the session and returns the user to the login screen. Password changes require the current password and invalidate the session.

### Localhost request protection
- Bind Kestrel only to `127.0.0.1`; fail startup if the effective listener is not exactly loopback.
- Serve the UI and API from the same canonical origin. Validate exact Host and Origin against `127.0.0.1` and the active port; reject alternate or unexpected host/origin values. Do not use wildcard CORS; disable CORS for the same-origin baseline.
- Generate a cryptographically random per-launch anti-CSRF token, rotate it every launch, expose it only to the same-origin page in memory, and require it in a dedicated header on every state-changing request, including setup and login.
- Enforce SameSite=Strict session cookies. Never put the launch token in a URL, cookie readable by JavaScript, or persistent storage.

## Consequences
- This is local-account protection, not a substitute for OS login or disk encryption.
- Incremental delay/lockout is account-specific brute-force protection, not a multi-user or IP-based rate limiter.
- Loopback addresses can still be targeted by browser pages, so Host/Origin/token/cookie protections and corresponding adversarial tests are part of the security boundary.
- Resetting a lost password without the recovery code is intentionally unsupported.
- WebView2 is optional; it must reuse the same local authentication and request protections if later approved.
