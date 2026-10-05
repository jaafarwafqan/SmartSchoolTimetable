# Security

## Security boundary
The product is a single-user local application: one local owner account, one application instance, no roles or permission matrix, no public API, and no remote service or sync. The local login is an access boundary against casual use, not a substitute for operating-system account security or full-disk encryption. A person with access to the machine and application data files may be able to inspect or alter unencrypted data.

## Owner account and recovery
- On first run, show a dedicated setup screen before any application page. It creates exactly one owner account with a unique username and password.
- Successful setup creates the account and signs the owner in automatically. Before entering the application, show the recovery code confirmation screen once and require acknowledgment that it was stored. Never force a password change; changing it is an optional Settings action and requires the current password.
- Generate the recovery code using `RandomNumberGenerator.GetBytes(16)`: 128 bits of entropy, represented as four groups of eight uppercase hexadecimal characters separated by hyphens (35 displayed characters, alphabet `0-9A-F` plus separators). Display it only during setup/replacement and tell the owner to store it securely or print it. Do not log it, persist it in clear text, or show it again.
- Store only a salted one-way SHA-256 hash of the recovery code. Its 128-bit random entropy prevents low-entropy password-style guessing. Successful use consumes the code, resets the password, and issues a replacement code shown once; require the owner to store it before continuing. The setup and recovery screens must clearly warn that the code is the **only** password-recovery path.
- If a reload (or anything else) interrupts confirmation, the session stays signed in but the UI shows a blocking "recovery code not confirmed" screen instead of any application page. The only actions there are generating a replacement code with the current password, and logging out. Generation invalidates the old code; the replacement is shown once and must be acknowledged before the application opens.
- If both password and recovery code are lost, there is no password reset path. Reinitialization is destructive and loses local application data unless the owner separately has a valid backup; it is not an alternate password-recovery mechanism.
- Changing a password requires the current password. A password change (and a recovery-code reset) revokes all local sessions and requires sign-in again.

## Password storage and failed login handling
- Never store or log clear-text passwords.
- Passwords are 8–1024 characters; usernames are 3–64 characters after trimming (`src/SmartSchoolTimetable.Application/CredentialRules.cs`, mirrored in `frontend/src/lib/credentialRules.ts`; the server always re-validates).
- The implementation uses PBKDF2-HMAC-SHA-256 with 600,000 iterations, a cryptographically random 16-byte salt, and a 32-byte derived key (`src/SmartSchoolTimetable.Infrastructure/Pbkdf2CredentialHasher.cs`). The per-account iteration count is stored with the hash (`Users.PasswordIterations`); the algorithm, salt size and key size are fixed in code and are not stored. Verification rejects stored iteration counts below 600,000. Changing the algorithm requires an ADR, a hash-version column, and a migration.
- Compare derived values using `CryptographicOperations.FixedTimeEquals`; do not use ordinary byte/character equality for secret verification.
- Apply one fixed one-second delay after a failed login. There is no escalating delay, temporary lockout, persisted failure counter, or multi-user/IP rate limiting. The delay runs after the auth operation lock is released, so a failed attempt does not block other operations.
- Do not expose whether a username exists in authentication error messages.

## Logging
- EF Core database-command logging is `Warning` by default and `Information` only under the Development environment configuration (`src/SmartSchoolTimetable.Api/appsettings.json` and `appsettings.Development.json`).
- EF Core sensitive-data logging is explicitly disabled. Never log passwords, recovery codes, cookies, session identifiers, or per-launch tokens.

## Session and inactivity lock
- Baseline: server-side local session cookie with `HttpOnly`, `SameSite=Strict`, and `Path=/`. Set `Secure` when HTTPS is used; it is intentionally omitted for baseline loopback HTTP because Kestrel binds only to `127.0.0.1`. No `Secure` attribute is required for the agreed local HTTP baseline. Do not use refresh tokens.
- Configure `Authentication:InactivityTimeoutMinutes` to an integer from 1 through 1440 or `Never` (default: 30). This is the default only. The owner can choose 5, 15, 30 or 60 minutes or never in Settings. The choice is stored per owner, applied by the server on the next request without a restart, and audited. On expiry, invalidate the session and return to the separate login screen; do not leave protected content accessible.
- Logout and password change invalidate the local session.

## Loopback-only listener and localhost attacks
- Kestrel MUST bind to `127.0.0.1` only. It MUST NOT bind to `0.0.0.0`, a LAN address, or a wildcard. A startup/integration test must inspect the actual bound address and fail if it is not exactly loopback.
- Only accept the canonical local origin/host for the current listener (for example `http://127.0.0.1:<port>`). Reject unexpected Host values and reject missing or unexpected Origin values on state-changing browser requests. Do not add wildcard CORS origins; the UI is same-origin.
- Use `SameSite=Strict` cookies and require a cryptographically random per-launch token in a dedicated header on every state-changing request. Keep the token in page memory, rotate it every launch, and never place it in a URL or persistent storage. Validate it server-side in addition to Origin/Host checks.
- The token, strict origin checks, and cookie settings mitigate malicious websites that attempt to make a visitor's browser call a service listening on localhost. A loopback-only listener is not sufficient by itself: browsers can still send requests to localhost, and DNS rebinding or forged Host/Origin handling can target local services.
- Do not expose remote administration, bind to external interfaces, or send application data/telemetry to network services.

## API error and UI localization boundary
- Every API failure returns a stable code and correlation identifier (plus field/code validation parameters where applicable), never user-readable text or default ASP.NET ProblemDetails titles/details. Codes come only from `ErrorCodes`; each has a mapped HTTP error status and an Arabic message, and an error is never returned with a 2xx status (see `API.md`).
- Model-binding, FluentValidation, unsupported route/method/media, Origin/launch-token rejection, database, and unhandled failures use the same error envelope. Unhandled API exceptions use `INTERNAL_ERROR` and a correlation identifier.
- FluentValidation emits error codes only. The React UI maps every known code to Arabic and uses a generic Arabic fallback for unknown/missing codes and network/offline/timeout failures. Do not render exception text, English framework messages, or API-provided prose.
- Browser forms disable native validation popups; use localized app validation instead. Do not call `window.alert`, `window.confirm`, or `window.prompt`.

## School logo and stamp uploads (Phase 2)
See [ADR 0016](./adr/0016-school-asset-storage.md).
- **Who can upload:** the owner session, Origin and the per-launch token are required. The upload endpoint opts out of the antiforgery middleware only because those checks already protect it.
- **Size:** a request whose `Content-Length` exceeds 3 MB is rejected before it is read (413 `PAYLOAD_TOO_LARGE`). The file itself must be ≤ 2 MB (`ASSET_TOO_LARGE`).
- **Type:** only PNG, JPEG and WebP are accepted, identified by magic bytes, never by the extension or the declared type alone. SVG and every other type is `ASSET_TYPE_NOT_ALLOWED`. A declared type that disagrees with the bytes is `ASSET_TYPE_MISMATCH`.
- **Storage:** files are written under a generated name (random 128-bit hex) inside the app data `assets` folder. The original file name is never used for storage or echoed back. Reads accept only names that match the strict stored-name pattern, which rules out path traversal.
- **Serving:** files go only to the owner session, with the detected content type, `X-Content-Type-Options: nosniff`, `Content-Security-Policy: default-src 'none'; sandbox` and `Cache-Control: no-store`.
- **Clean-up:** a replaced or removed file is deleted only after the database change commits. If the save fails, the newly written file is removed.

## Local audit history
From Phase 2, school-setup create, update, archive and delete events are audited (event names are in `API.md`). Keep a lightweight local history for timetable version changes, publish, rollback, backup/restore, password changes, and imports. Multi-user attribution is not needed. Record event type, local timestamp, affected entity/version, and a concise before/after summary where appropriate. Never record passwords, recovery codes, session cookies, or per-launch tokens.

## Optional final-phase encryption decision
Baseline SQLite is unencrypted. `adr/0010-sqlcipher-go-no-go.md` records a **NO-GO for the initial release** and a final-phase go/no-go evaluation for SQLCipher, including key storage, packaging, recovery, backup, performance, and licensing. Do not claim database encryption unless that decision changes and is implemented/tested.

## Optional WebView2
The standalone WebView2 window is optional. The browser-based login/setup screen is the baseline and must remain usable independently.
