# Security

## Security boundary
The product is a single-user local application: one local owner account, one application instance, no roles or permission matrix, no public API, and no remote service or sync. The local login is an access boundary against casual use, not a substitute for operating-system account security or full-disk encryption. A person with access to the machine and application data files may be able to inspect or alter unencrypted data.

## Owner account and recovery
- On first run, show a dedicated setup screen before any application page. It creates exactly one owner account with a unique username and password.
- Generate the recovery code using `RandomNumberGenerator.GetBytes(16)`: 128 bits of entropy, represented as four groups of eight uppercase hexadecimal characters separated by hyphens (35 displayed characters, alphabet `0-9A-F` plus separators). Display it only during setup/replacement and tell the owner to store it securely or print it. Do not log it, persist it in clear text, or show it again.
- Store only a salted one-way SHA-256 hash of the recovery code. Its 128-bit random entropy prevents low-entropy password-style guessing. Successful use consumes the code, resets the password, and issues a replacement code shown once; require the owner to store it before continuing. The setup and recovery screens must clearly warn that the code is the **only** password-recovery path.
- If both password and recovery code are lost, there is no password reset path. Reinitialization is destructive and loses local application data unless the owner separately has a valid backup; it is not an alternate password-recovery mechanism.
- Changing a password requires the current password. A password change invalidates the current session and requires sign-in again.

## Password storage and failed login handling
- Never store or log clear-text passwords.
- The Phase 1 implementation uses PBKDF2-HMAC-SHA-256 with 600,000 iterations, a cryptographically random 16-byte salt, and a 32-byte derived key. The algorithm and iteration count are stored with the hash (`src/SmartSchoolTimetable.Infrastructure/Pbkdf2CredentialHasher.cs`).
- Compare derived values using `CryptographicOperations.FixedTimeEquals`; do not use ordinary byte/character equality for secret verification.
- Incrementally delay failed login attempts and apply a temporary lockout after repeated failures. Persist failure/lockout state for the owner account. This is a local brute-force control, not a multi-user/IP rate-limiting system.
- Do not expose whether a username exists in authentication error messages.

## Logging
- EF Core database-command logging is `Warning` by default and `Information` only under the Development environment configuration (`src/SmartSchoolTimetable.Api/appsettings.json` and `appsettings.Development.json`).
- EF Core sensitive-data logging is explicitly disabled. Never log passwords, recovery codes, cookies, session identifiers, or per-launch tokens.

## Session and inactivity lock
- Baseline: server-side local session cookie with `HttpOnly`, `SameSite=Strict`, `Path=/`, and a bounded lifetime. Set `Secure` when HTTPS is used; it is intentionally omitted for the baseline HTTP listener because Kestrel binds only to `127.0.0.1` and the application is served over loopback HTTP. Do not use refresh tokens.
- Configure an inactivity timeout. On expiry, invalidate the session and return to the separate login screen; do not leave protected content accessible.
- Logout and password change invalidate the local session.

## Loopback-only listener and localhost attacks
- Kestrel MUST bind to `127.0.0.1` only. It MUST NOT bind to `0.0.0.0`, a LAN address, or a wildcard. A startup/integration test must inspect the actual bound address and fail if it is not exactly loopback.
- Only accept the canonical local origin/host for the current listener (for example `http://127.0.0.1:<port>`). Reject unexpected or missing `Host` and `Origin` values on browser requests. Do not add wildcard CORS origins; preferably disable CORS because the UI is same-origin.
- Use `SameSite=Strict` cookies and require a cryptographically random per-launch token in a dedicated header on every state-changing request. Keep the token in page memory, rotate it every launch, and never place it in a URL or persistent storage. Validate it server-side in addition to Origin/Host checks.
- The token, strict origin checks, and cookie settings mitigate malicious websites that attempt to make a visitor's browser call a service listening on localhost. A loopback-only listener is not sufficient by itself: browsers can still send requests to localhost, and DNS rebinding or forged Host/Origin handling can target local services.
- Do not expose remote administration, bind to external interfaces, or send application data/telemetry to network services.

## Local audit history
Keep a lightweight local history for timetable version changes, publish, rollback, backup/restore, password changes, and imports. Multi-user attribution is not needed. Record event type, local timestamp, affected entity/version, and a concise before/after summary where appropriate. Never record passwords, recovery codes, session cookies, or per-launch tokens.

## Optional final-phase encryption decision
Baseline SQLite is unencrypted. `adr/0010-sqlcipher-go-no-go.md` records a **NO-GO for the initial release** and a final-phase go/no-go evaluation for SQLCipher, including key storage, packaging, recovery, backup, performance, and licensing. Do not claim database encryption unless that decision changes and is implemented/tested.

## Optional WebView2
The standalone WebView2 window is optional. The browser-based login/setup screen is the baseline and must remain usable independently.
