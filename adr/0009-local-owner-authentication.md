# ADR 0009: Local owner authentication and localhost protection

- Status: Accepted by owner scope clarification; amended in Phase 1.3 (pending owner approval)
- Date: 2026-10-03

## Context
The product is a single-user local application with exactly one owner account. There is no external identity provider, public endpoint, role/permission system, or multi-user workflow. A browser can still be induced by a malicious website to send requests to localhost.

## Decision

### First-run setup and recovery
- A dedicated setup screen creates one owner account only when the local user table is empty. Successful setup also creates an authenticated session; do not require a second login.
- Before entering the application, show the recovery code once and require acknowledgment that it was stored. If the page reloads before confirmation, the session remains signed in but the UI shows a blocking screen (no application page is reachable). It explains that the code was not confirmed and offers only "generate a new recovery code" (current password required) and logout. Regeneration invalidates the previous code and shows the new one once; it must be acknowledged before entry. *(Phase 1.3 amendment: previously "sign in and regenerate from Settings", which the implementation did not enforce.)*
- Generate 16 random bytes with `RandomNumberGenerator` (128 bits), displayed as four eight-character uppercase hexadecimal groups. Store only a salted one-way hash.
- The recovery code is the sole password-reset mechanism. Successful recovery consumes the used code, changes the password, and issues a replacement once. If both password and recovery code are lost, there is no alternate recovery path.
- Do not force a password change after setup or recovery. A Settings password change requires the current password and invalidates the session.

### Password and failed-login behavior
- Use PBKDF2-HMAC-SHA-256 with a random 16-byte salt, 600,000 iterations, and a 32-byte output. Store the iteration count with the hash; the algorithm is fixed in code, and changing it requires a hash-version column and migration. Verify secrets using constant-time comparison.
- Passwords are 8–1024 characters *(Phase 1.3 amendment: minimum lowered from 12 to 8 by owner request)*.
- After a failed login, wait a fixed one second. Do not add an escalating delay, temporary lockout, or a multi-user/IP rate-limit system.
- Never store or log plaintext passwords, recovery codes, cookies, session identifiers, or launch tokens.

### Session and inactivity
- Use a server-side local session in an `HttpOnly`, `SameSite=Strict`, `Path=/` cookie. No access/refresh token pair.
- Set `Secure` when HTTPS is used. The baseline local listener is loopback HTTP, so the Secure attribute is intentionally omitted and not required for that baseline.
- Configure inactivity timeout from 1 to 1440 minutes or `Never` (default 30); expiry invalidates the session and returns the UI to login.
- Logout revokes the session immediately.

### Localhost request protection
- Bind Kestrel only to `127.0.0.1`; fail startup unless the effective address is exactly loopback.
- Serve UI/API from the same canonical origin. Validate exact Host and Origin; reject missing Origin on state-changing requests. Do not enable wildcard CORS.
- Create a cryptographically random token on every launch. Return it to the same-origin page and keep it in memory only. Every state-changing request requires it in a dedicated header in addition to a matching Origin and Host.
- Never place the launch token in a URL, persistent storage, or a JavaScript-readable cookie.
- Strict Origin/Host checks, SameSite cookies, and the launch token mitigate malicious web pages and DNS-rebinding-style attempts to call localhost. Loopback binding alone is insufficient.

## Consequences
- Local authentication protects against casual use; it is not a substitute for operating-system access control or disk encryption.
- Resetting the password without the recovery code is intentionally unsupported.
- Browser-based login is the baseline. WebView2 is optional and, if approved later, must retain the same authentication and request protections.
- No RBAC, refresh-token rotation, remote sync, multi-user concurrency, or multi-user-specific rate limiting is introduced.
